namespace VanDao.Game;

/// <summary>Phiên bản game (thẻ &lt;Version&gt; trong VanDao.csproj).</summary>
public static class AppVersion
{
    public static readonly string Current = typeof(AppVersion).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>"0.6.0" / "0.6" / "v0.6.0-beta" → 0.6.0; sai định dạng = 0.0.0.</summary>
    public static Version Parse(string? s)
    {
        var parts = (s ?? "").Trim().TrimStart('v', 'V').Split('-')[0].Split('.');
        int P(int i) => i < parts.Length && int.TryParse(parts[i], out var n) ? n : 0;
        return new Version(P(0), P(1), P(2));
    }
}

/// <param name="Status">ok · update (có bản mới, vẫn chơi được) · required (bản quá cũ) · unknown (không kiểm tra được / chưa cấu hình)</param>
/// <param name="Blocked">true = chặn chơi (required, trừ môi trường dev).</param>
public record VersionDto(string Current, string? Latest, string? Min, string Url, string Message, string Status, bool Blocked);

/// <summary>
/// Đọc bảng app_config trên Supabase (docs/version/supabase.sql) để biết bản mới nhất / bản tối thiểu.
/// Mất mạng → "unknown": vẫn cho chơi offline; Chợ và Bảng xếp hạng thì Supabase tự từ chối bản cũ.
/// </summary>
public sealed class VersionService(LeaderboardOptions opt, ILogger<VersionService> log, IHostEnvironment? env = null)
{
    private sealed record Row(string MinVersion, string LatestVersion, string DownloadUrl, string Message);
    private static readonly System.Text.Json.JsonSerializerOptions Json = new() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower };

    private readonly HttpClient _http = Client(opt);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private VersionDto? _last;
    private DateTime _at;
    private bool Dev => env?.IsDevelopment() == true;
    /// <summary>Máy chủ online (Program.cs bật): ServerNow lấy đồng hồ của chính máy chủ, vì máy chủ không đọc app_config.</summary>
    public bool UseOwnClock { get; init; }

    private static HttpClient Client(LeaderboardOptions o)
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        if (!o.Enabled) return h;
        h.BaseAddress = new Uri(o.Url.TrimEnd('/') + "/rest/v1/");
        h.DefaultRequestHeaders.Add("apikey", o.AnonKey);
        h.DefaultRequestHeaders.Add("Authorization", $"Bearer {o.AnonKey}");
        return h;
    }

    // giờ máy chủ Supabase (header Date của lần kiểm tra gần nhất) + thời gian máy đã chạy từ đó: không phụ thuộc đồng hồ máy người chơi
    private DateTimeOffset _serverDate; private long _serverTick = -1;
    /// <summary>Giờ máy chủ hiện tại; null khi chưa kết nối được hoặc lần kết nối gần nhất đã quá 20 phút (coi như offline).
    /// UseOwnClock (máy chủ online): dùng đồng hồ của chính máy chủ, người chơi không chỉnh được.</summary>
    public DateTimeOffset? ServerNow
    {
        get
        {
            if (UseOwnClock) return DateTimeOffset.UtcNow;
            if (_serverTick < 0) return null;
            var elapsed = Environment.TickCount64 - _serverTick;
            return elapsed is < 0 or > 20 * 60 * 1000 ? null : _serverDate + TimeSpan.FromMilliseconds(elapsed);
        }
    }

    private bool Fresh => _last is not null && DateTime.UtcNow - _at < (_last.Status == "unknown" ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(15));

    /// <summary>Kết quả gần nhất, không chờ mạng (hết hạn thì làm mới chạy nền). Chưa kiểm tra lần nào thì chờ lần đầu.</summary>
    public async ValueTask<VersionDto> PeekAsync()
    {
        if (_last is null) return await GetAsync();
        if (!Fresh) _ = Task.Run(() => GetAsync());
        return _last;
    }

    public async Task<VersionDto> GetAsync(bool force = false)
    {
        if (!force && Fresh) return _last!;
        await _lock.WaitAsync();
        try
        {
            if (!force && Fresh) return _last!;
            _last = await CheckAsync();
            _at = DateTime.UtcNow;
            return _last;
        }
        finally { _lock.Release(); }
    }

    private async Task<VersionDto> CheckAsync()
    {
        var cur = AppVersion.Current;
        if (!opt.Enabled) return new VersionDto(cur, null, null, "", "", "unknown", false);
        try
        {
            using var res = await _http.GetAsync("app_config?select=min_version,latest_version,download_url,message&id=eq.1");
            res.EnsureSuccessStatusCode();
            if (res.Headers.Date is { } date) { _serverDate = date; _serverTick = Environment.TickCount64; }
            var r = (await res.Content.ReadFromJsonAsync<List<Row>>(Json))?.FirstOrDefault();
            if (r is null) return new VersionDto(cur, null, null, "", "", "unknown", false);
            var v = AppVersion.Parse(cur);
            var status = v < AppVersion.Parse(r.MinVersion) ? "required" : v < AppVersion.Parse(r.LatestVersion) ? "update" : "ok";
            return new VersionDto(cur, r.LatestVersion, r.MinVersion, r.DownloadUrl, r.Message, status, status == "required" && !Dev);
        }
        catch (Exception ex)
        {
            log.LogInformation("Version check: {Error}", ex.Message);
            return new VersionDto(cur, null, null, "", "", "unknown", false);
        }
    }
}
