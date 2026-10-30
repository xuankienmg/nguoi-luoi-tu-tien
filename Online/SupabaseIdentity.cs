using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace VanDao.Online;

public sealed class SupabaseIdentity(OnlineOptions options)
{
    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(10) };
    public async Task<Guid?> UserAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 8192 || token.Split('.').Length != 3) return null;
        using var request = new HttpRequestMessage(HttpMethod.Get, options.AuthUrl.TrimEnd('/') + "/auth/v1/user");
        request.Headers.Add("apikey", options.PublishableKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await Http.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest) return null;
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.TryGetProperty("id", out var id) && Guid.TryParse(id.GetString(), out var user) ? user : null;
    }

    public async Task<(int Status, string Json)> SessionAsync(string action, SessionRequest body, CancellationToken ct)
    {
        var path = action switch { "signin" => "/token?grant_type=password", "signup" => "/signup", "refresh" => "/token?grant_type=refresh_token", "google" => "/token?grant_type=pkce", _ => throw new ArgumentException("Unsupported auth action.") };
        object payload;
        if (action == "google") {
            if (string.IsNullOrWhiteSpace(body.AuthCode) || body.AuthCode.Length > 2048 || body.CodeVerifier is null
                || body.CodeVerifier.Length is < 43 or > 128 || body.CodeVerifier.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.' or '~')))
                return (400, "{\"error\":\"Mã đăng nhập Google không hợp lệ.\"}");
            payload = new { auth_code = body.AuthCode, code_verifier = body.CodeVerifier };
        } else if (action == "refresh") {
            if (string.IsNullOrWhiteSpace(body.RefreshToken) || body.RefreshToken.Length > 8192) return (400, "{\"error\":\"Phiên đăng nhập không hợp lệ.\"}");
            payload = new { refresh_token = body.RefreshToken };
        } else {
            if (string.IsNullOrWhiteSpace(body.Email) || body.Email.Length > 254 || body.Password is null || body.Password.Length is < 8 or > 256)
                return (400, "{\"error\":\"Nhập email và mật khẩu từ 8 ký tự.\"}");
            payload = new { email = body.Email, password = body.Password };
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, options.AuthUrl.TrimEnd('/') + "/auth/v1" + path);
        request.Headers.Add("apikey", options.PublishableKey);
        request.Content = JsonContent.Create(payload);
        using var response = await Http.SendAsync(request, ct);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));
    }

    public async Task<bool> GoogleEnabledAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, options.AuthUrl.TrimEnd('/') + "/auth/v1/settings");
        request.Headers.Add("apikey", options.PublishableKey);
        using var response = await Http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.TryGetProperty("external", out var external)
            && external.TryGetProperty("google", out var google) && google.ValueKind == JsonValueKind.True;
    }

    public async Task<int> LogoutAsync(string token, CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Post,options.AuthUrl.TrimEnd('/')+"/auth/v1/logout?scope=local");
        request.Headers.Add("apikey",options.PublishableKey);
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        using var response=await Http.SendAsync(request,ct);return (int)response.StatusCode;
    }
}

public sealed record SessionRequest(string? Email, string? Password, string? RefreshToken, string? AuthCode = null, string? CodeVerifier = null);
