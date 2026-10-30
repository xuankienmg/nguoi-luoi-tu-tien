namespace VanDao.Online;

public sealed class OnlineOptions
{
    public string Mode { get; set; } = "Offline";
    public string AuthUrl { get; set; } = "";
    public string PublishableKey { get; set; } = "";
    public string BackendUrl { get; set; } = "";
    public int LocalPort { get; set; }
    /// <summary>Set only when the backend runs behind the host's HTTPS proxy, so rate limits see the player's IP.</summary>
    public bool TrustProxy { get; set; }
    public bool IsServer => Mode.Equals("Server", StringComparison.OrdinalIgnoreCase);
    public bool IsClient => Mode.Equals("Client", StringComparison.OrdinalIgnoreCase);
    public void Validate(IConfiguration config)
    {
        if (!new[] { "offline", "server", "client" }.Contains(Mode.ToLowerInvariant()))
            throw new InvalidOperationException("Online:Mode must be Offline, Server or Client.");
        if (IsServer) {
            SecureUrl(AuthUrl);
            if (string.IsNullOrWhiteSpace(PublishableKey) || string.IsNullOrWhiteSpace(config.GetConnectionString("Game")))
                throw new InvalidOperationException("Online server requires AuthUrl, PublishableKey and a server-only Game connection string.");
            var database = new Npgsql.NpgsqlConnectionStringBuilder(config.GetConnectionString("Game"));
            var localDatabase = database.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || System.Net.IPAddress.TryParse(database.Host, out var address) && System.Net.IPAddress.IsLoopback(address);
            if (!localDatabase && database.SslMode != Npgsql.SslMode.VerifyFull)
                throw new InvalidOperationException("Remote online databases require SSL Mode=VerifyFull.");
        }
        if (IsClient) {
            SecureUrl(BackendUrl);
            if (LocalPort is < 0 or > 65535) throw new InvalidOperationException("Online:LocalPort is invalid.");
        }
    }
    private static void SecureUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo)
            || uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback))
            throw new InvalidOperationException("Online URLs require HTTPS; HTTP is allowed only on loopback for local tests.");
    }
}
