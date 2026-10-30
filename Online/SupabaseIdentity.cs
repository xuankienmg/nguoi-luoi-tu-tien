using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace VanDao.Online;

public sealed class SupabaseIdentity(OnlineOptions options, HttpClient? http = null)
{
    private static readonly HttpClient Shared = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly TimeSpan KeysLifetime = TimeSpan.FromMinutes(10);
    private readonly HttpClient Http = http ?? Shared;
    private readonly JsonWebTokenHandler _jwt = new();
    private readonly SemaphoreSlim _keysLock = new(1, 1);
    private IList<SecurityKey>? _keys;
    private DateTime _keysAt;

    public async Task<Guid?> UserAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 8192 || token.Split('.').Length != 3) return null;
        var (verified, user) = await LocalUserAsync(token, ct);
        return verified ? user : await RemoteUserAsync(token, ct);
    }

    /// <summary>Checks asymmetric Supabase JWTs against the project JWKS, so a request costs no Auth round-trip.
    /// Not verified (HS256 legacy secret, JWKS unreachable, unknown key) → the caller asks Auth instead.</summary>
    private async Task<(bool Verified, Guid? User)> LocalUserAsync(string token, CancellationToken ct)
    {
        JsonWebToken jwt;
        try { jwt = _jwt.ReadJsonWebToken(token); } catch (ArgumentException) { return (true, null); }
        if (jwt.Alg is null || jwt.Alg.StartsWith("HS", StringComparison.OrdinalIgnoreCase)) return (false, null);
        IList<SecurityKey> keys;
        try {
            keys = await KeysAsync(refresh: false, ct);
            if (!keys.Any(k => k.KeyId == jwt.Kid)) keys = await KeysAsync(refresh: true, ct);   // signing key rotated
        } catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or ArgumentException) {
            return (false, null);
        }
        if (!keys.Any(k => k.KeyId == jwt.Kid)) return (false, null);
        var result = await _jwt.ValidateTokenAsync(token, new TokenValidationParameters {
            ValidIssuer = options.AuthUrl.TrimEnd('/') + "/auth/v1", ValidAudience = "authenticated",
            IssuerSigningKeys = keys, ClockSkew = TimeSpan.FromSeconds(30),
        });
        return (true, result.IsValid && result.ClaimsIdentity.FindFirst("sub")?.Value is { } sub && Guid.TryParse(sub, out var user) ? user : null);
    }

    private async Task<IList<SecurityKey>> KeysAsync(bool refresh, CancellationToken ct)
    {
        if (!refresh && _keys is not null && DateTime.UtcNow - _keysAt < KeysLifetime) return _keys;
        await _keysLock.WaitAsync(ct);
        try {
            if (_keys is not null && DateTime.UtcNow - _keysAt < (refresh ? TimeSpan.FromSeconds(30) : KeysLifetime)) return _keys;
            using var request = new HttpRequestMessage(HttpMethod.Get, options.AuthUrl.TrimEnd('/') + "/auth/v1/.well-known/jwks.json");
            request.Headers.Add("apikey", options.PublishableKey);
            using var response = await Http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            _keys = new JsonWebKeySet(await response.Content.ReadAsStringAsync(ct)).GetSigningKeys();
            _keysAt = DateTime.UtcNow;
            return _keys;
        } finally { _keysLock.Release(); }
    }

    private async Task<Guid?> RemoteUserAsync(string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, options.AuthUrl.TrimEnd('/') + "/auth/v1/user");
        request.Headers.Add("apikey", options.PublishableKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await Http.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest) return null;
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.TryGetProperty("id", out var id) && Guid.TryParse(id.GetString(), out var user) ? user : null;
    }

    public async Task<(int Status, string Json)> SessionAsync(string action, SessionRequest body, CancellationToken ct, string? redirectTo = null)
    {
        var path = action switch {
            "signin" => "/token?grant_type=password", "signup" => "/signup", "refresh" => "/token?grant_type=refresh_token", "google" => "/token?grant_type=pkce",
            "recover" => "/recover?redirect_to=" + Uri.EscapeDataString(redirectTo ?? ""), "resend" => "/resend",
            _ => throw new ArgumentException("Unsupported auth action.") };
        object payload;
        if (action is "recover" or "resend") {
            if (string.IsNullOrWhiteSpace(body.Email) || body.Email.Length > 254 || !body.Email.Contains('@')) return (400, "{\"error\":\"Nhập email hợp lệ.\"}");
            payload = action == "recover" ? new { email = body.Email } : new { type = "signup", email = body.Email };
        } else if (action == "google") {
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
        if (action is "recover" or "resend") {
            // Same answer whether or not the address has an account, so the form cannot be used to list players.
            if (response.StatusCode == HttpStatusCode.TooManyRequests) return (429, "{\"error\":\"Đã gửi email gần đây. Hãy chờ vài phút rồi thử lại.\"}");
            if ((int)response.StatusCode >= 500) return (503, "{\"error\":\"Chưa gửi được email. Hãy thử lại sau.\"}");
            return (200, action == "recover"
                ? "{\"message\":\"Nếu email này có tài khoản, thư đặt lại mật khẩu đã được gửi. Hãy kiểm tra hộp thư (cả mục Spam).\"}"
                : "{\"message\":\"Nếu email này đang chờ xác nhận, thư xác nhận mới đã được gửi. Hãy kiểm tra hộp thư (cả mục Spam).\"}");
        }
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Sets a new password with the access token from a recovery link.</summary>
    public async Task<(int Status, string Json)> UpdatePasswordAsync(string token, string? password, CancellationToken ct)
    {
        if (password is null || password.Length is < 8 or > 256) return (400, "{\"error\":\"Mật khẩu mới cần từ 8 ký tự.\"}");
        using var request = new HttpRequestMessage(HttpMethod.Put, options.AuthUrl.TrimEnd('/') + "/auth/v1/user");
        request.Headers.Add("apikey", options.PublishableKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { password });
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
public sealed record PasswordRequest(string? Password);
