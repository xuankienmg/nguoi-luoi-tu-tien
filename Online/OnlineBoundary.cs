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
        db.Timing = new DbTiming(); var clock = new StepClock(db.Timing);   // header Server-Timing: thời gian + số lượt đi về DB từng bước
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
        clock.Mark("auth");
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
            // Có id nhân vật trong đường dẫn: kiểm tra nhân vật thuộc tài khoản trong cùng lệnh (bớt một lượt đi về DB).
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            Guid? playerId = segments.Length >= 3 && Guid.TryParse(segments[2], out var id) ? id : null;
            var owns = await LockAsync(db, owner.Value, playerId, ctx.RequestAborted);
            clock.Mark("lock");   // gồm mở kết nối + BEGIN + chờ request khác cùng tài khoản (+ kiểm tra nhân vật)
            if (mutation) {
                // idempotency + giới hạn tốc độ trong một lệnh; đọc SAU khi giữ khóa để thấy request trước đã commit
                var (previous, recent) = await RecentRequestsAsync(db, owner.Value, requestId, DateTime.UtcNow.AddSeconds(-1), ctx.RequestAborted);
                if (previous is not null) {
                    if (previous.Fingerprint != fingerprint) { await Error(ctx, 409, "Mã yêu cầu đã dùng cho hành động khác."); return; }
                    ctx.Response.StatusCode = previous.Status; ctx.Response.ContentType = "application/json; charset=utf-8";
                    ctx.Response.Headers["X-Idempotent-Replay"] = "true";
                    await ctx.Response.WriteAsync(OnlineRequestBody.Unpack(previous.Response), ctx.RequestAborted); return;
                }
                if (recent >= 10) {
                    await Error(ctx, 429, "Thao tác quá nhanh. Hãy chờ một giây."); return;
                }
            }
            if (!owns) {
                await Error(ctx, 404, "Không tìm thấy tu sĩ thuộc tài khoản này."); return;
            }
            if (mutation && path.TrimEnd('/') == "/api/players" && await db.Players.AnyAsync(ctx.RequestAborted)) {
                await Error(ctx, 409, "Tài khoản đã có nhân vật. Hãy dùng nhân vật hiện tại."); return;
            }
            clock.Mark("guard");  // idempotency + giới hạn tốc độ + kiểm tra nhân vật
            ctx.Response.Body = buffer;
            await next(ctx);
            clock.Mark("app");
            if (ctx.Response.StatusCode >= 400) {
                await transaction.RollbackAsync(ctx.RequestAborted);
            } else {
                // SaveAsync may have run inside the endpoint, but nothing commits before this transaction.
                if (mutation) db.OnlineRequests.Add(new OnlineRequest {
                    OwnerId = owner.Value, RequestId = requestId, Fingerprint = fingerprint,
                    Response = OnlineRequestBody.Pack(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)), Status = ctx.Response.StatusCode
                });
                await db.SaveChangesAsync(ctx.RequestAborted);
                await transaction.CommitAsync(ctx.RequestAborted);
                committed = true;
                players?.Committed(db);
            }
            clock.Mark("commit"); // lưu OnlineRequests + SaveChanges + COMMIT
            var timing = db.Timing.Header([.. clock.Steps, ("total", clock.Total, -1)]);
            ctx.Response.Headers["Server-Timing"] = timing;
            if (clock.Total > 1000) logger.LogWarning("Slow online request {Method} {Path} {Status}: {Timing}", ctx.Request.Method, ctx.Request.Path, ctx.Response.StatusCode, timing);
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
        // Yêu Triều: báo kết quả ván để nhận thưởng — chỉ { kills: {loại quái: số}, seconds: số }; server tự kiểm tra hợp lệ (SwarmData.ValidateRun).
        if (p.StartsWith("/api/players/", StringComparison.Ordinal) && p.Count(c => c == '/') == 5 && p.EndsWith("/swarm/claim", StringComparison.Ordinal))
            return root.EnumerateObject().All(x =>
                x.Name.Equals("seconds", StringComparison.OrdinalIgnoreCase) ? x.Value.ValueKind == JsonValueKind.Number
                : x.Name.Equals("kills", StringComparison.OrdinalIgnoreCase)
                    && x.Value.ValueKind == JsonValueKind.Object && x.Value.EnumerateObject().All(k => k.Value.ValueKind == JsonValueKind.Number));
        var field = p == "/api/players" ? "name"
            : p.StartsWith("/api/players/", StringComparison.Ordinal) && p.EndsWith("/chat", StringComparison.Ordinal) && p.Count(c => c == '/') == 4 ? "text"
            : p.StartsWith("/api/players/", StringComparison.Ordinal) && p.Count(c => c == '/') == 5
              && p.EndsWith("/admin/announcement", StringComparison.Ordinal) ? "text"
            // Tông Môn: tên tông, thông báo, chat tông — chỉ một trường text
            : p.StartsWith("/api/players/", StringComparison.Ordinal) && p.Count(c => c == '/') == 5
              && (p.EndsWith("/sect/create", StringComparison.Ordinal) || p.EndsWith("/sect/notice", StringComparison.Ordinal) || p.EndsWith("/sect/chat", StringComparison.Ordinal)) ? "text"
            : null;
        return root.EnumerateObject().All(x => field is not null && x.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Khóa advisory theo tài khoản (giữ tới khi commit). Có playerId: cùng lệnh trả về nhân vật có thuộc tài khoản không.
    /// Câu kiểm tra dùng snapshot đầu lệnh (trước khi chờ khóa) — đủ đúng vì nhân vật đã tồn tại trước khi client biết id của nó.</summary>
    public static async Task<bool> LockAsync(GameDbContext db, Guid owner, Guid? playerId, CancellationToken ct)
    {
        var key = owner.ToString();
        if (playerId is not { } id) {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0));", ct);
            return true;
        }
        return await db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM game."Players" p WHERE p."Id" = {id} AND p."OwnerId" = {owner}) AS "Value"
            FROM (SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))) AS l
            """).SingleAsync(ct);
    }

    /// <summary>Một lệnh: yêu cầu cùng mã đã lưu (kèm phản hồi để trả lại) + số yêu cầu của tài khoản sau mốc since (giới hạn tốc độ).
    /// Chỉ lấy cột Response của dòng trùng mã; các dòng gần đây khác chỉ để đếm.</summary>
    public static async Task<(OnlineRequest? Previous, int Recent)> RecentRequestsAsync(GameDbContext db, Guid owner, Guid requestId, DateTime since, CancellationToken ct)
    {
        var rows = await db.OnlineRequests.AsNoTracking()
            .Where(r => r.OwnerId == owner && (r.RequestId == requestId || r.CreatedAt > since))
            .Select(r => new OnlineRequest {
                OwnerId = r.OwnerId, RequestId = r.RequestId, CreatedAt = r.CreatedAt, Fingerprint = r.Fingerprint, Status = r.Status,
                Response = r.RequestId == requestId ? r.Response : "" })
            .ToListAsync(ct);
        return (rows.FirstOrDefault(r => r.RequestId == requestId), rows.Count(r => r.CreatedAt > since));
    }

    private static async Task Error(HttpContext ctx, int code, string message) {
        ctx.Response.StatusCode = code; await ctx.Response.WriteAsJsonAsync(new { error = message });
    }
}
