using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;

namespace VanDao.Online;

/// <summary>Authentication, ownership and one atomic, idempotent state transition per request.</summary>
public sealed class OnlineBoundary(RequestDelegate next, SupabaseIdentity identity, ILogger<OnlineBoundary> logger)
{
    public async Task InvokeAsync(HttpContext ctx, GameDbContext db, OnlineOptions options)
    {
        var env = ctx.Request.Path.Equals("/api/env", StringComparison.OrdinalIgnoreCase);
        if (!env && !ctx.Request.Path.StartsWithSegments("/api/players")) { await next(ctx); return; }
        ctx.Response.Headers.CacheControl = "no-store";
        var auth = ctx.Request.Headers.Authorization.ToString();
        if (env) {
            // giao diện chỉ hiện admin mode cho tài khoản trong Online:AdminUsers; lỗi xác minh = không có admin
            Guid? user = null;
            try { if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) user = await identity.UserAsync(auth[7..].Trim(), ctx.RequestAborted); }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { }
            await ctx.Response.WriteAsJsonAsync(new { dev = user is { } u && options.IsAdminUser(u) }, ctx.RequestAborted);
            return;
        }
        Guid? owner;
        try {
            owner = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? await identity.UserAsync(auth[7..].Trim(), ctx.RequestAborted) : null;
        } catch (Exception e) when (e is HttpRequestException or TaskCanceledException) {
            await Error(ctx, 503, "Không xác minh được phiên đăng nhập. Hãy thử lại."); return;
        }
        if (owner is null) { await Error(ctx, 401, "Cần đăng nhập tài khoản online."); return; }
        db.RequestOwnerId = owner;
        var path = ctx.Request.Path.Value ?? "";
        var admin = options.IsAdminUser(owner.Value);
        if (!admin && path.Contains("/admin/", StringComparison.OrdinalIgnoreCase)) {
            await Error(ctx, 403, "Admin không khả dụng trên nhân vật online."); return;
        }
        Player.SetRequestAdmin(admin);
        var mutation = HttpMethods.IsPost(ctx.Request.Method);
        Guid requestId = default;
        string fingerprint = "";
        if (mutation) {
            if (!Guid.TryParse(ctx.Request.Headers["Idempotency-Key"], out requestId) || requestId == Guid.Empty) {
                await Error(ctx, 400, "Thiếu mã yêu cầu Idempotency-Key hợp lệ."); return;
            }
            if (ctx.Request.ContentLength > 4096) { await Error(ctx, 413, "Nội dung yêu cầu quá lớn."); return; }
            ctx.Request.EnableBuffering(4096, 4096);
            string body;
            try {
                using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
                body = await reader.ReadToEndAsync(ctx.RequestAborted); ctx.Request.Body.Position = 0;
                if (!string.IsNullOrWhiteSpace(body)) {
                    using var json = JsonDocument.Parse(body);
                    if (!BodyAllowed(path, json.RootElement)) {
                        await Error(ctx, 400, "Client chỉ được gửi hành động, không được tải dữ liệu nhân vật lên."); return;
                    }
                }
            } catch (Exception e) when (e is JsonException or IOException) {
                await Error(ctx, 400, "Nội dung yêu cầu không hợp lệ."); return;
            }
            fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ctx.Request.Method + path + ctx.Request.QueryString + "\n" + body)));
        }
        var original = ctx.Response.Body;
        var players = ctx.RequestServices.GetService<VanDao.Game.PlayerCache>();
        var committed = false;
        await using var buffer = new MemoryStream();
        await using var transaction = await db.Database.BeginTransactionAsync(ctx.RequestAborted);
        try {
            // Cross-process/replica lock on the authenticated account; held until database commit.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({owner.ToString()}, 0));", ctx.RequestAborted);
            if (mutation) {
                var previous = await db.OnlineRequests.FindAsync([owner.Value, requestId], ctx.RequestAborted);
                if (previous is not null) {
                    if (previous.Fingerprint != fingerprint) { await Error(ctx, 409, "Mã yêu cầu đã dùng cho hành động khác."); return; }
                    ctx.Response.StatusCode = previous.Status; ctx.Response.ContentType = "application/json; charset=utf-8";
                    ctx.Response.Headers["X-Idempotent-Replay"] = "true";
                    await ctx.Response.WriteAsync(previous.Response, ctx.RequestAborted); return;
                }
                var since = DateTime.UtcNow.AddSeconds(-1);
                if (await db.OnlineRequests.CountAsync(r => r.OwnerId == owner && r.CreatedAt > since, ctx.RequestAborted) >= 10) {
                    await Error(ctx, 429, "Thao tác quá nhanh. Hãy chờ một giây."); return;
                }
            }
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 3 && Guid.TryParse(segments[2], out var id)
                && !await db.Players.AnyAsync(p => p.Id == id, ctx.RequestAborted)) {
                await Error(ctx, 404, "Không tìm thấy tu sĩ thuộc tài khoản này."); return;
            }
            if (mutation && path.TrimEnd('/') == "/api/players" && await db.Players.AnyAsync(ctx.RequestAborted)) {
                await Error(ctx, 409, "Tài khoản đã có nhân vật. Hãy dùng nhân vật hiện tại."); return;
            }
            ctx.Response.Body = buffer;
            await next(ctx);
            if (ctx.Response.StatusCode >= 400) {
                await transaction.RollbackAsync(ctx.RequestAborted);
            } else {
                // SaveAsync may have run inside the endpoint, but nothing commits before this transaction.
                if (mutation) db.OnlineRequests.Add(new OnlineRequest {
                    OwnerId = owner.Value, RequestId = requestId, Fingerprint = fingerprint,
                    Response = Encoding.UTF8.GetString(buffer.ToArray()), Status = ctx.Response.StatusCode
                });
                await db.SaveChangesAsync(ctx.RequestAborted);
                await transaction.CommitAsync(ctx.RequestAborted);
                committed = true;
                players?.Committed(db);
            }
            ctx.Response.Body = original; buffer.Position = 0;
            await buffer.CopyToAsync(original, ctx.RequestAborted);
        } catch (Exception e) when (!ctx.RequestAborted.IsCancellationRequested) {
            await transaction.RollbackAsync(CancellationToken.None);
            ctx.Response.Body = original; ctx.Response.ContentLength = null;
            logger.LogError(e, "Online player transaction failed for {TraceId}", ctx.TraceIdentifier);
            await Error(ctx, 503, "Không lưu được hành động. Dữ liệu chưa được xác nhận; hãy thử lại với cùng mã yêu cầu.");
        } finally {
            ctx.Response.Body = original;
            // rollback / lỗi / request bị hủy: cây nhân vật trong RAM có thể đã bị sửa mà database không đổi → bỏ khỏi cache
            if (!committed && players is not null && db.TouchedPlayers.Count > 0) {
                players.Abandoned(db);
                var staged = ctx.RequestServices.GetService<VanDao.Game.ServerLeaderboardCache>();
                foreach (var id in db.TouchedPlayers) staged?.Staged.TryRemove(id, out _);
            }
        }
    }
    /// <summary>
    /// Body JSON chỉ được chứa đúng trường của hành động, không bao giờ dữ liệu nhân vật:
    /// tạo nhân vật (POST /api/players → name) và chat Thế giới (POST /api/players/{id}/chat → text).
    /// </summary>
    public static bool BodyAllowed(string path, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return false;
        var p = path.TrimEnd('/');
        var field = p == "/api/players" ? "name"
            : p.StartsWith("/api/players/", StringComparison.Ordinal) && p.EndsWith("/chat", StringComparison.Ordinal) && p.Count(c => c == '/') == 4 ? "text"
            // Tông Môn: tên tông, thông báo, chat tông — chỉ một trường text
            : p.StartsWith("/api/players/", StringComparison.Ordinal) && p.Count(c => c == '/') == 5
              && (p.EndsWith("/sect/create", StringComparison.Ordinal) || p.EndsWith("/sect/notice", StringComparison.Ordinal) || p.EndsWith("/sect/chat", StringComparison.Ordinal)) ? "text"
            : null;
        return root.EnumerateObject().All(x => field is not null && x.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task Error(HttpContext ctx, int code, string message) {
        ctx.Response.StatusCode = code; await ctx.Response.WriteAsJsonAsync(new { error = message });
    }
}
