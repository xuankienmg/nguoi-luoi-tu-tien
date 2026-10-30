using System.Net;
using System.Text;
using System.Text.Json;
using VanDao.Online;

namespace GemSystemTests;

public class OnlineAccountFlowTests
{
    /// <summary>Records what the backend sends to Supabase Auth and answers with a fixed status.</summary>
    private sealed class RecordingAuth(HttpStatusCode status, string body = "{}") : HttpMessageHandler
    {
        public readonly List<(HttpMethod Method, string Url, string Body, string? Bearer)> Calls = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls.Add((request.Method, request.RequestUri!.ToString(), request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct), request.Headers.Authorization?.Parameter));
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static SupabaseIdentity Identity(RecordingAuth auth) =>
        new(new OnlineOptions { Mode = "Server", AuthUrl = "https://auth.example.test", PublishableKey = "test" }, new HttpClient(auth));

    private static string Error(string json) => JsonDocument.Parse(json).RootElement.GetProperty("message").GetString()!;

    [Fact]
    public async Task RecoverSendsResetLinkToTheGameResetPage()
    {
        var auth = new RecordingAuth(HttpStatusCode.OK);
        var result = await Identity(auth).SessionAsync("recover", new SessionRequest("a@example.com", null, null), default, "https://game.example.test/online-reset.html");
        var call = Assert.Single(auth.Calls);
        Assert.Equal("https://auth.example.test/auth/v1/recover?redirect_to=https%3A%2F%2Fgame.example.test%2Fonline-reset.html", call.Url);
        Assert.Equal("a@example.com", JsonDocument.Parse(call.Body).RootElement.GetProperty("email").GetString());
        Assert.Equal(200, result.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task RecoverAndResendAnswerTheSameWhetherOrNotTheEmailExists(HttpStatusCode upstream)
    {
        foreach (var action in new[] { "recover", "resend" }) {
            var result = await Identity(new RecordingAuth(upstream)).SessionAsync(action, new SessionRequest("a@example.com", null, null), default, "https://game.example.test/online-reset.html");
            Assert.Equal(200, result.Status);
            Assert.Contains("Nếu email", Error(result.Json));
        }
    }

    [Fact]
    public async Task RecoverReportsRateLimitAndOutage()
    {
        Assert.Equal(429, (await Identity(new RecordingAuth(HttpStatusCode.TooManyRequests)).SessionAsync("recover", new SessionRequest("a@example.com", null, null), default, "https://g.test/r")).Status);
        Assert.Equal(503, (await Identity(new RecordingAuth(HttpStatusCode.InternalServerError)).SessionAsync("recover", new SessionRequest("a@example.com", null, null), default, "https://g.test/r")).Status);
    }

    [Fact]
    public async Task ResendAsksForAnotherSignupConfirmation()
    {
        var auth = new RecordingAuth(HttpStatusCode.OK);
        await Identity(auth).SessionAsync("resend", new SessionRequest("a@example.com", null, null), default);
        var call = Assert.Single(auth.Calls);
        Assert.Equal("https://auth.example.test/auth/v1/resend", call.Url);
        var body = JsonDocument.Parse(call.Body).RootElement;
        Assert.Equal("signup", body.GetProperty("type").GetString());
        Assert.Equal("a@example.com", body.GetProperty("email").GetString());
    }

    [Fact]
    public async Task InvalidEmailIsRejectedWithoutCallingAuth()
    {
        var auth = new RecordingAuth(HttpStatusCode.OK);
        Assert.Equal(400, (await Identity(auth).SessionAsync("recover", new SessionRequest("", null, null), default, "https://g.test/r")).Status);
        Assert.Empty(auth.Calls);
    }

    [Fact]
    public async Task PasswordChangeUsesTheRecoveryTokenAndChecksLength()
    {
        var auth = new RecordingAuth(HttpStatusCode.OK, """{"id":"u"}""");
        var identity = Identity(auth);
        Assert.Equal(400, (await identity.UpdatePasswordAsync("a.b.c", "short", default)).Status);
        Assert.Empty(auth.Calls);
        Assert.Equal(200, (await identity.UpdatePasswordAsync("a.b.c", "Long-enough-1", default)).Status);
        var call = Assert.Single(auth.Calls);
        Assert.Equal(HttpMethod.Put, call.Method);
        Assert.Equal("https://auth.example.test/auth/v1/user", call.Url);
        Assert.Equal("a.b.c", call.Bearer);
        Assert.Equal("Long-enough-1", JsonDocument.Parse(call.Body).RootElement.GetProperty("password").GetString());
    }
}
