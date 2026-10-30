using System.Net.Http.Headers;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using VanDao.Data;

namespace VanDao.Online;

public static class OnlineGateway
{
    public static void MapSession(WebApplication app, OnlineOptions options)
    {
        app.MapGet("/api/session/config", () => new { online = options.IsServer, mode = options.Mode });
        if (!options.IsServer) return;
        app.MapGet("/api/session/google", async (HttpContext ctx, SupabaseIdentity identity) => {
            ctx.Response.Headers.CacheControl = "no-store";
            try {
                return Results.Json(new { enabled = await identity.GoogleEnabledAsync(ctx.RequestAborted),
                    authorizeUrl = options.AuthUrl.TrimEnd('/') + "/auth/v1/authorize" });
            } catch (Exception e) when (e is HttpRequestException or TaskCanceledException) {
                return Results.Json(new { error = "Không kết nối được dịch vụ đăng nhập Google." }, statusCode: 503);
            }
        });
        app.MapPost("/api/session/logout",async(HttpContext ctx,SupabaseIdentity identity)=>{
            ctx.Response.Headers.CacheControl="no-store";
            var auth=ctx.Request.Headers.Authorization.ToString();
            if(!auth.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase))return Results.Unauthorized();
            try{return Results.StatusCode(await identity.LogoutAsync(auth[7..].Trim(),ctx.RequestAborted));}
            catch(Exception e)when(e is HttpRequestException or TaskCanceledException){return Results.StatusCode(503);}
        });
        app.MapPost("/api/session/{action}", async (string action, SessionRequest body, SupabaseIdentity identity, HttpContext ctx) => {
            if (action is not ("signin" or "signup" or "refresh" or "google")) return Results.NotFound();
            ctx.Response.Headers.CacheControl = "no-store";
            try {
                var result = await identity.SessionAsync(action, body, ctx.RequestAborted);
                return Results.Content(result.Json, "application/json", statusCode: result.Status);
            } catch (Exception e) when (e is HttpRequestException or TaskCanceledException) {
                return Results.Json(new { error = "Dịch vụ đăng nhập tạm thời không khả dụng." }, statusCode: 503);
            }
        }).RequireRateLimiting(AuthLimit);
        // Host health check: reachable database, no game data in the response.
        app.MapGet("/healthz", async (GameDbContext db, HttpContext ctx) =>
            await db.Database.CanConnectAsync(ctx.RequestAborted) ? Results.Ok(new { status = "ok" }) : Results.Json(new { status = "database unavailable" }, statusCode: 503));
    }

    public const string AuthLimit = "online-auth";

    /// <summary>Per-IP limit on sign-in/sign-up/refresh so the backend cannot be used to brute-force Supabase Auth.</summary>
    public static void AddAuthRateLimit(IServiceCollection services) => services.AddRateLimiter(o => {
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        o.OnRejected = (context, ct) => new(context.HttpContext.Response.WriteAsJsonAsync(new { error = "Thao tác đăng nhập quá nhanh. Hãy chờ một phút rồi thử lại." }, ct));
        o.AddPolicy(AuthLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
    });

    public static void ConfigureClient(WebApplication app, OnlineOptions options)
    {
        var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(60) };
        app.UseDefaultFiles(); app.UseStaticFiles();
        app.Run(async ctx => {
            if (!ctx.Request.Path.StartsWithSegments("/api")) { ctx.Response.StatusCode = 404; return; }
            var target = options.BackendUrl.TrimEnd('/') + ctx.Request.Path + ctx.Request.QueryString;
            using var request = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), target);
            foreach (var name in new[] { "Authorization", "Idempotency-Key", "Accept" })
                if (ctx.Request.Headers.TryGetValue(name, out var value)) request.Headers.TryAddWithoutValidation(name, value.ToArray());
            if (ctx.Request.ContentLength > 0 || ctx.Request.Headers.ContainsKey("Transfer-Encoding")) {
                request.Content = new StreamContent(ctx.Request.Body);
                if (ctx.Request.ContentType is { } type) request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(type);
            }
            try {
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
                ctx.Response.StatusCode = (int)response.StatusCode;
                ctx.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
                ctx.Response.Headers.CacheControl = "no-store";
                if (response.Headers.TryGetValues("X-Idempotent-Replay", out var replay)) ctx.Response.Headers["X-Idempotent-Replay"] = replay.First();
                await response.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
            } catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ctx.RequestAborted.IsCancellationRequested) {
                ctx.Response.StatusCode = 503; await ctx.Response.WriteAsJsonAsync(new { error = "Không kết nối được máy chủ online. Save local không được dùng thay thế dữ liệu online." });
            }
        });
    }
}
