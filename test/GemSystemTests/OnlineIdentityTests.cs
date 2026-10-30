using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VanDao.Online;

namespace GemSystemTests;

public class OnlineIdentityTests
{
    private const string AuthUrl = "https://auth.example.test";
    private static readonly Guid User = Guid.Parse("2154fbb6-776a-42ae-868f-f0b31edf5e65");

    /// <summary>Plays Supabase Auth: serves JWKS and counts fallback /auth/v1/user calls.</summary>
    private sealed class FakeAuth(ECDsaSecurityKey key, bool jwksDown = false) : HttpMessageHandler
    {
        public int UserCalls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/auth/v1/.well-known/jwks.json") {
                if (jwksDown) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                var jwk = JsonWebKeyConverter.ConvertFromECDsaSecurityKey(key);
                var json = JsonSerializer.Serialize(new { keys = new[] { new { kty = jwk.Kty, crv = jwk.Crv, x = jwk.X, y = jwk.Y, kid = jwk.Kid, alg = "ES256", use = "sig" } } });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
            }
            if (path == "/auth/v1/user") {
                UserCalls++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($$"""{"id":"{{User}}"}""", Encoding.UTF8, "application/json") });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private static ECDsaSecurityKey NewKey(string kid) => new(ECDsa.Create(ECCurve.NamedCurves.nistP256)) { KeyId = kid };

    private static string Token(SecurityKey key, string alg, DateTime? expires = null, string issuer = AuthUrl + "/auth/v1") =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor {
            Issuer = issuer, Audience = "authenticated",
            Claims = new Dictionary<string, object> { ["sub"] = User.ToString() },
            NotBefore = (expires ?? DateTime.UtcNow.AddHours(1)).AddHours(-2), Expires = expires ?? DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(key, alg),
        });

    private static SupabaseIdentity Identity(FakeAuth auth) =>
        new(new OnlineOptions { Mode = "Server", AuthUrl = AuthUrl, PublishableKey = "test" }, new HttpClient(auth));

    [Fact]
    public async Task ValidTokenIsVerifiedLocallyWithoutCallingAuth()
    {
        var key = NewKey("k1"); var auth = new FakeAuth(key);
        Assert.Equal(User, await Identity(auth).UserAsync(Token(key, SecurityAlgorithms.EcdsaSha256), default));
        Assert.Equal(0, auth.UserCalls);
    }

    [Fact]
    public async Task TokenSignedByAnotherKeyIsRejected()
    {
        var auth = new FakeAuth(NewKey("k1"));
        Assert.Null(await Identity(auth).UserAsync(Token(NewKey("k1"), SecurityAlgorithms.EcdsaSha256), default));
        Assert.Equal(0, auth.UserCalls);
    }

    [Fact]
    public async Task ExpiredTokenAndForeignIssuerAreRejected()
    {
        var key = NewKey("k1"); var identity = Identity(new FakeAuth(key));
        Assert.Null(await identity.UserAsync(Token(key, SecurityAlgorithms.EcdsaSha256, DateTime.UtcNow.AddMinutes(-5)), default));
        Assert.Null(await identity.UserAsync(Token(key, SecurityAlgorithms.EcdsaSha256, issuer: "https://evil.test/auth/v1"), default));
    }

    [Fact]
    public async Task FallsBackToAuthWhenJwksIsUnavailable()
    {
        var key = NewKey("k1"); var auth = new FakeAuth(key, jwksDown: true);
        Assert.Equal(User, await Identity(auth).UserAsync(Token(key, SecurityAlgorithms.EcdsaSha256), default));
        Assert.Equal(1, auth.UserCalls);
    }

    [Fact]
    public async Task LegacySymmetricTokensStillUseAuth()
    {
        var auth = new FakeAuth(NewKey("k1"));
        var hs = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        Assert.Equal(User, await Identity(auth).UserAsync(Token(hs, SecurityAlgorithms.HmacSha256), default));
        Assert.Equal(1, auth.UserCalls);
    }
}
