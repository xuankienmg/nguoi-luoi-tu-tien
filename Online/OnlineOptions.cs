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
    /// <summary>Server: public HTTPS address of this backend, used for links in Auth emails (password reset). Empty = the request's own address.</summary>
    public string PublicUrl { get; set; } = "";
    /// <summary>Desktop: backend used when the player picks "Chơi online" in the menu. Empty = online play not offered.</summary>
    public string DefaultBackendUrl { get; set; } = "";
    /// <summary>Server: user id Supabase (uuid) được dùng admin mode, cách nhau bởi dấu phẩy. Rỗng = không ai.</summary>
    public string AdminUsers { get; set; } = "";
    /// <summary>Server: giữ cây nhân vật trong RAM (PlayerCache) để mỗi lần đồng bộ không đọc lại cả nhân vật từ database. false = tắt.</summary>
    public bool PlayerCache { get; set; } = true;
    public bool IsAdminUser(Guid user) => AdminUsers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Any(x => Guid.TryParse(x, out var id) && id == user);
    public bool IsServer =>Mode.Equals("Server", StringComparison.OrdinalIgnoreCase);
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
            if (PublicUrl.Length > 0) SecureUrl(PublicUrl);
            if (AdminUsers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(x => !Guid.TryParse(x, out _)))
                throw new InvalidOperationException("Online:AdminUsers must be a comma-separated list of Supabase user ids.");
        }
        if (DefaultBackendUrl.Length > 0) SecureUrl(DefaultBackendUrl);
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
