using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VanDao.Desktop;

namespace VanDao.Launcher;

/// <summary>Đăng nhập online trong Launcher: gọi /api/session/* của backend game rồi trao phiên cho game
/// qua file mã hoá DPAPI (OnlineSessionStore, dùng chung mã nguồn với game). Mật khẩu không được lưu.</summary>
internal static class LauncherOnline
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>Địa chỉ backend như game dùng: biến môi trường Online__DefaultBackendUrl, rồi appsettings.json cạnh game. Rỗng = chưa có máy chủ.</summary>
    public static string BackendUrl(string gamePath)
    {
        var url = Environment.GetEnvironmentVariable("Online__DefaultBackendUrl");
        if (string.IsNullOrWhiteSpace(url))
        {
            try
            {
                var file = Path.Combine(gamePath, "appsettings.json");
                if (File.Exists(file)) url = JsonNode.Parse(File.ReadAllText(file))?["Online"]?["DefaultBackendUrl"]?.GetValue<string>();
            }
            catch (Exception e) when (e is IOException or JsonException or InvalidOperationException) { url = null; }
        }
        // giống OnlineOptions của game: chỉ HTTPS, HTTP chỉ cho máy chủ thử trên chính máy này
        return Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) && string.IsNullOrEmpty(uri.UserInfo)
            && (uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback) ? uri.AbsoluteUri.TrimEnd('/') : "";
    }

    public static string PlayMode => LauncherSettings.ReadGameSetting("PlayMode") == "Online" ? "Online" : "Offline";

    public static void SetPlayMode(string mode) => LauncherSettings.WriteGameSetting("PlayMode", mode == "Online" ? "Online" : "Offline");

    /// <summary>Email của phiên đang trao cho game (null = chưa đăng nhập).</summary>
    public static string? SignedInEmail()
    {
        var json = OnlineSessionStore.Default.Load() ?? OnlineSessionStore.Handoff.Load();
        if (json is null) return null;
        try { return JsonNode.Parse(json)?["user"]?["email"]?.GetValue<string>() ?? ""; }
        catch (Exception e) when (e is JsonException or InvalidOperationException) { return ""; }
    }

    /// <summary>signin | signup | recover | resend. Trả mã HTTP và JSON của backend để giao diện hiện lỗi;
    /// có access_token thì lưu phiên: ghi nhớ → file lâu dài, không ghi nhớ → file game đọc một lần.</summary>
    public static async Task<(int Status, JsonNode? Body)> SessionAsync(string backend, string action, string email, string? password, bool remember)
    {
        if (backend.Length == 0) return (503, new JsonObject { ["error"] = LauncherSettings.L("Chưa có máy chủ online.", "No online server yet.") });
        var body = new JsonObject { ["email"] = email };
        if (password is not null) body["password"] = password;
        try
        {
            using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            using var res = await Http.PostAsync(backend + "/api/session/" + action, content);
            JsonNode? result;
            try { result = JsonNode.Parse(await res.Content.ReadAsStringAsync()); }
            catch (JsonException) { result = null; }
            if (res.IsSuccessStatusCode && result?["access_token"]?.GetValue<string>() is { Length: > 0 })
            {
                result["expires_at"] ??= DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (result["expires_in"]?.GetValue<long>() ?? 3600);
                var json = result.ToJsonString();
                (remember ? OnlineSessionStore.Default : OnlineSessionStore.Handoff).Save(json);
                (remember ? OnlineSessionStore.Handoff : OnlineSessionStore.Default).Clear();
                // trả cho giao diện chỉ email, không đưa token vào trang
                return ((int)res.StatusCode, new JsonObject { ["signedIn"] = true, ["email"] = result["user"]?["email"]?.GetValue<string>() ?? email });
            }
            return ((int)res.StatusCode, result);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return (503, new JsonObject { ["error"] = LauncherSettings.L("Không kết nối được máy chủ online.", "Couldn't reach the online server.") });
        }
    }

    /// <summary>Xoá phiên trên máy; báo backend thu hồi refresh token nếu kết nối được.</summary>
    public static async Task SignOutAsync(string backend)
    {
        var json = OnlineSessionStore.Default.Load() ?? OnlineSessionStore.Handoff.Load();
        OnlineSessionStore.Default.Clear();
        OnlineSessionStore.Handoff.Clear();
        if (json is null || backend.Length == 0) return;
        try
        {
            var token = JsonNode.Parse(json)?["access_token"]?.GetValue<string>();
            if (string.IsNullOrEmpty(token)) return;
            using var req = new HttpRequestMessage(HttpMethod.Post, backend + "/api/session/logout");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var _ = await Http.SendAsync(req);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException) { }
    }
}
