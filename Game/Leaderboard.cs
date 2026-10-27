using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>Cấu hình "Leaderboard" trong appsettings.json: URL project Supabase + anon key. Bỏ trống = tắt bảng xếp hạng.</summary>
public sealed class LeaderboardOptions
{
    public string Url { get; set; } = "";
    public string AnonKey { get; set; } = "";
    public bool Enabled => Url.Length > 0 && AnonKey.Length > 0;
}

public record LbPetDto(string Name, string Image, int Level, int Stars);
public record LbRowDto(long Rank, string Name, int Realm, int Stage, string RealmName, string StageName, long Power, bool Me, Guid Id = default, bool Flagged = false, long Progress = 0, long PetPower = 0, LbPetDto? Pet = null, List<TitleDto>? Titles = null);
/// <summary>Hồ sơ một người trên bảng: trang bị đang mặc (ô → mã vật phẩm) và chỉ số chiến đấu lúc gửi điểm gần nhất.</summary>
public record LbProfileDto(string Name, int Realm, int Stage, string RealmName, string StageName, long Power,
    Dictionary<string, string> Equipment, Dictionary<string, double> Stats, DateTime UpdatedAt, bool Me, List<TitleDto>? Titles = null);
public record LeaderboardDto(bool Enabled, string By, List<LbRowDto> Rows, LbRowDto? Me, bool Tainted, string Name, string? Error = null, bool Flagged = false);

/// <summary>
/// Bảng xếp hạng online (Supabase, xem docs/leaderboard/supabase.sql).
/// Gửi điểm chạy nền, có giới hạn tần suất; lỗi mạng không bao giờ làm hỏng lượt chơi.
/// </summary>
public sealed class LeaderboardService(LeaderboardOptions opt, ILogger<LeaderboardService> log, IHostEnvironment? env = null)
{
    /// <summary>Môi trường dev (ASPNETCORE_ENVIRONMENT=Development): bỏ qua cờ admin (Tainted), nhân vật thử nghiệm vẫn lên bảng.</summary>
    private bool Dev => env?.IsDevelopment() == true;
    public bool Excluded(Player p) => p.Tainted && !Dev;

    public const int Top = 50;
    private static readonly TimeSpan MinGap = TimeSpan.FromSeconds(15);     // đổi cảnh giới / lực chiến / tên: gửi sớm nhất sau 15 giây
    private static readonly TimeSpan Refresh = TimeSpan.FromMinutes(3);     // không đổi gì: vẫn cập nhật tu vi mỗi 3 phút
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(20);
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private static readonly string Version = typeof(LeaderboardService).Assembly.GetName().Version?.ToString(3) ?? "";

    private readonly HttpClient _http = CreateClient(opt);
    private readonly ConcurrentDictionary<Guid, (DateTime at, string key)> _sent = new();
    private readonly ConcurrentDictionary<string, (DateTime at, List<Row> rows)> _cache = new();

    public bool Enabled => opt.Enabled;

    public Task ClearDataFlagAsync(Player p) => ClearDataFlagAsync(p.Id, false);

    public async Task ClearDataFlagAsync(Guid playerId, bool requireExisting = true)
    {
        if (!Dev) throw new GameException("Chỉ admin trong môi trường phát triển được gỡ cờ dữ liệu.");
        if (!Enabled) { if (requireExisting) throw new GameException("Chưa bật bảng xếp hạng online."); return; }
        var key = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY");
        if (string.IsNullOrWhiteSpace(key))
            throw new GameException("Cần cấu hình SUPABASE_SERVICE_ROLE_KEY trong môi trường server dev để gỡ cờ trên Supabase.");
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            opt.Url.TrimEnd('/') + "/rest/v1/leaderboard?select=player_id&player_id=eq." + playerId);
        request.Headers.Add("Prefer", "return=representation");
        request.Headers.Add("apikey", key);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        request.Content = JsonContent.Create(new { flagged = false });
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode) throw new GameException("Không gỡ được cờ trên Supabase. Kiểm tra quyền admin server và thử lại.");
        var changed = await response.Content.ReadFromJsonAsync<List<Dictionary<string, System.Text.Json.JsonElement>>>();
        if (requireExisting && changed?.Count != 1) throw new GameException("Không tìm thấy người chơi trên bảng xếp hạng.");
        _sent.TryRemove(playerId, out _);
        _cache.Clear();
    }

    private static HttpClient CreateClient(LeaderboardOptions o)
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        if (!o.Enabled) return h;
        h.BaseAddress = new Uri(o.Url.TrimEnd('/') + "/rest/v1/");
        h.DefaultRequestHeaders.Add("apikey", o.AnonKey);
        h.DefaultRequestHeaders.Add("Authorization", $"Bearer {o.AnonKey}");
        h.DefaultRequestHeaders.Add("X-Game-Version", AppVersion.Current);   // Supabase chặn bản cũ (require_version)
        return h;
    }

    /// <summary>Gọi sau mỗi lần lưu nhân vật: gửi điểm nếu đến lúc (chạy nền, không chờ).</summary>
    public void Track(Player p, bool force = false)
    {
        if (!Enabled || p.LbSecret.Length == 0) return;
        if (Excluded(p))
        {
            if (_sent.TryGetValue(p.Id, out var s) && s.key == "removed") return;
            _sent[p.Id] = (DateTime.UtcNow, "removed");
            var (rid, rsec) = (p.Id, p.LbSecret);
            _ = Task.Run(() => Post("rpc/remove_score", new { p_id = rid, p_secret = rsec }));
            return;
        }
        var (body, oldBody, key) = Score(p);
        var now = DateTime.UtcNow;
        if (!force && _sent.TryGetValue(p.Id, out var last) && now - last.at < (last.key == key ? Refresh : MinGap)) return;
        _sent[p.Id] = (now, key);
        _ = Task.Run(() => Submit(body, oldBody));
    }

    /// <summary>Gửi điểm ngay và chờ xong: Lôi đài tính trận từ chỉ số trên Supabase nên cần bản mới nhất trước khi đánh.</summary>
    public async Task<bool> SubmitNowAsync(Player p)
    {
        if (!Enabled || p.LbSecret.Length == 0 || Excluded(p)) return false;
        var (body, oldBody, key) = Score(p);
        _sent[p.Id] = (DateTime.UtcNow, key);
        return await Submit(body, oldBody);
    }

    private async Task<bool> Submit(object body, object oldBody)
    {
        var ok = await Post("rpc/submit_score", body, quietNotFound: true);
        if (!ok && _oldSchema) ok = await Post("rpc/submit_score", oldBody);
        _cache.Clear();                     // để người chơi thấy ngay hạng mới của mình
        return ok;
    }

    /// <summary>Thân yêu cầu submit_score (bản mới + bản cho SQL cũ chưa có p_flagged) và khóa so sánh "có gì đổi không".</summary>
    private (object body, object oldBody, string key) Score(Player p)
    {
        var r = GameService.Realm(p);
        var power = GameService.Power(p);
        var progress = GameService.LeaderboardProgress(p);
        var petPower = GameService.LeaderboardPetPower(p);
        var bestPet = GameService.LeaderboardBestPet(p);
        var equipment = p.Equipment.Where(e => e.ItemId.Length > 0).OrderBy(e => e.Slot).ToDictionary(e => e.Slot, e => e.ItemId);
        var st = GameService.Stats(p);
        var key = $"{p.Name}|{p.Realm}|{p.Stage}|{power}|{progress}|{petPower}|{bestPet?.DefId}|{bestPet?.Level}|{bestPet?.StarPts}|{bestPet?.AppearanceForm}|{string.Join(',', equipment.Values)}";
        var stats = new Dictionary<string, double> { ["progress"] = progress, ["pet_power"] = petPower, ["atk"] = st.Atk, ["def"] = st.Def, ["hp"] = st.Hp, ["crit"] = Math.Round(st.Crit, 1), ["speed"] = Math.Round(st.Speed, 1) };
        if (bestPet is not null)
        {
            stats["pet_id:" + bestPet.DefId] = bestPet.Level;
            stats["pet_stars"] = PetData.StarsOf(bestPet);
            stats["pet_form"] = GameService.PetAppearance(bestPet);
        }
        var body = new
        {
            p_id = p.Id, p_secret = p.LbSecret, p_name = p.Name, p_realm = p.Realm, p_stage = p.Stage,
            p_level = GameService.Level(p), p_qi = Math.Round(p.Qi, 1), p_realm_name = r.Name, p_stage_name = r.Stages[p.Stage],
            p_power = power, p_version = Version, p_equipment = equipment, p_flagged = SaveGuard.DataFlagged(p, Dev),
            p_stats = stats,
        };
        return (body, new { body.p_id, body.p_secret, body.p_name, body.p_realm, body.p_stage, body.p_level, body.p_qi, body.p_realm_name, body.p_stage_name, body.p_power, body.p_version, body.p_equipment, body.p_stats }, key);
    }

    /// <summary>Supabase chưa chạy docs/leaderboard/supabase.sql bản có cột flagged: đọc / gửi theo kiểu cũ (không có dấu *).</summary>
    private volatile bool _oldSchema;

    private async Task<bool> Post(string path, object body, bool quietNotFound = false)
    {
        try
        {
            using var res = await _http.PostAsync(path, new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"));
            if (res.IsSuccessStatusCode) return true;
            var text = await res.Content.ReadAsStringAsync();
            if (quietNotFound && text.Contains("p_flagged")) { _oldSchema = true; return false; }
            log.LogWarning("Leaderboard {Path}: {Status} {Body}", path, (int)res.StatusCode, text);
        }
        catch (Exception ex) { log.LogWarning("Leaderboard {Path}: {Error}", path, ex.Message); }
        return false;
    }

    private sealed record Row(Guid PlayerId, string Name, int Realm, int Stage, string RealmName, string StageName, long Power, long RealmRank, long PowerRank, bool Flagged = false, long Progress = 0, long PetPower = 0, long ProgressRank = 0, long PetRank = 0, Dictionary<string, double>? Stats = null);

    private static LbPetDto? RankedPet(Dictionary<string, double>? stats)
    {
        if (stats is null) return null;
        var marker = stats.FirstOrDefault(x => x.Key.StartsWith("pet_id:", StringComparison.Ordinal));
        if (marker.Key is null || !PetData.ById.TryGetValue(marker.Key[7..], out var pet)) return null;
        var form = (int)Math.Clamp(stats.GetValueOrDefault("pet_form"), 0, pet.Forms.Length - 1);
        return new(pet.Name, pet.Forms[form].Image, (int)Math.Clamp(marker.Value, 1, 100),
            (int)Math.Clamp(stats.GetValueOrDefault("pet_stars"), 0, PetData.MaxStars));
    }

    private const string Cols = "player_id,name,realm,stage,realm_name,stage_name,power,realm_rank,power_rank,flagged";
    private const string ExtendedCols = Cols + ",progress,pet_power,progress_rank,pet_rank,stats";
    private volatile bool _rankSchemaMissing;
    private const string OldCols = "player_id,name,realm,stage,realm_name,stage_name,power,realm_rank,power_rank";

    /// <summary>Đọc view leaderboard_public; view chưa có cột flagged (chưa chạy SQL mới) thì đọc kiểu cũ.</summary>
    private async Task<List<Row>> ReadRows(string query)
    {
        if (!_rankSchemaMissing)
        {
            using var extended = await _http.GetAsync($"leaderboard_public?select={ExtendedCols}&{query}");
            if (extended.IsSuccessStatusCode) return await extended.Content.ReadFromJsonAsync<List<Row>>(Json) ?? [];
            var error = await extended.Content.ReadAsStringAsync();
            if (!error.Contains("progress") && !error.Contains("pet_power") && !error.Contains("pet_rank")) extended.EnsureSuccessStatusCode();
            _rankSchemaMissing = true;
        }
        if (query.Contains("progress_rank") || query.Contains("pet_rank"))
            throw new GameException("Cần chạy docs/leaderboard/supabase.sql trên Supabase để mở bảng Vượt ải và Linh thú.");
        if (!_oldSchema)
        {
            using var res = await _http.GetAsync($"leaderboard_public?select={Cols}&{query}");
            if (res.IsSuccessStatusCode) return await res.Content.ReadFromJsonAsync<List<Row>>(Json) ?? [];
            if (!(await res.Content.ReadAsStringAsync()).Contains("flagged")) res.EnsureSuccessStatusCode();
            _oldSchema = true;
        }
        return await _http.GetFromJsonAsync<List<Row>>($"leaderboard_public?select={OldCols}&{query}", Json) ?? [];
    }

    /// <param name="by">"realm" (cảnh giới) hoặc "power" (lực chiến).</param>
    public async Task<LeaderboardDto> GetAsync(Player p, string? by)
    {
        by = by is "power" or "progress" or "pet" ? by : "realm";
        if (!Enabled) return new LeaderboardDto(false, by, [], null, Excluded(p), p.Name);
        try
        {
            if (!_cache.TryGetValue(by, out var c) || DateTime.UtcNow - c.at > CacheFor)
            {
                var order = by switch { "power" => "power_rank.asc,realm_rank.asc", "progress" => "progress_rank.asc,power_rank.asc", "pet" => "pet_rank.asc,player_id.asc", _ => "realm_rank.asc,power_rank.asc" };
                c = (DateTime.UtcNow, await ReadRows($"order={order}&limit={Top}"));
                _cache[by] = c;
            }
            var mine = c.rows.FirstOrDefault(x => x.PlayerId == p.Id)
                       ?? (await ReadRows($"player_id=eq.{p.Id}")).FirstOrDefault();
            var titles = await TitlesAsync();
            LbRowDto Dto(Row x) => new(by switch { "power" => x.PowerRank, "progress" => x.ProgressRank, "pet" => x.PetRank, _ => x.RealmRank }, x.Name, x.Realm, x.Stage, x.RealmName, x.StageName, x.Power, x.PlayerId == p.Id, x.PlayerId,
                x.Flagged && (x.PlayerId != p.Id || !SaveGuard.AdminDataExempt(p, Dev)), (x.Stats is not null && x.Stats.TryGetValue("progress", out var savedProgress) && double.IsFinite(savedProgress) && savedProgress >= 0 && savedProgress <= int.MaxValue ? (long)savedProgress : x.Progress), x.PetPower, RankedPet(x.Stats), titles.GetValueOrDefault(x.PlayerId));
            return new LeaderboardDto(true, by, c.rows.Select(Dto).ToList(), Excluded(p) || mine is null ? null : Dto(mine), Excluded(p), p.Name, Flagged: SaveGuard.DataFlagged(p, Dev));
        }
        catch (Exception ex)
        {
            log.LogWarning("Leaderboard get: {Error}", ex.Message);
            return new LeaderboardDto(true, by, [], null, Excluded(p), p.Name, ex is GameException ? ex.Message : "Không kết nối được bảng xếp hạng. Kiểm tra mạng rồi thử lại.");
        }
    }

    private sealed record ProfileRow(Guid PlayerId, string Name, int Realm, int Stage, string RealmName, string StageName, long Power,
        Dictionary<string, string>? Equipment, Dictionary<string, double>? Stats, DateTime UpdatedAt);
    private readonly ConcurrentDictionary<Guid, (DateTime at, ProfileRow row)> _profiles = new();

    /// <summary>Trang bị + chỉ số của một người trên bảng (bấm vào dòng). Null = không có trên bảng.</summary>
    public async Task<LbProfileDto?> ProfileAsync(Player me, Guid playerId)
    {
        if (!Enabled) throw new GameException("Bảng xếp hạng online chưa được cấu hình.");
        if (!_profiles.TryGetValue(playerId, out var c) || DateTime.UtcNow - c.at > CacheFor)
        {
            List<ProfileRow>? rows;
            try { rows = await _http.GetFromJsonAsync<List<ProfileRow>>($"leaderboard_public?select=player_id,name,realm,stage,realm_name,stage_name,power,equipment,stats,updated_at&player_id=eq.{playerId}", Json); }
            catch (Exception ex) { log.LogWarning("Leaderboard profile: {Error}", ex.Message); throw new GameException("Không kết nối được bảng xếp hạng. Kiểm tra mạng rồi thử lại."); }
            if (rows is not { Count: > 0 }) return null;
            c = (DateTime.UtcNow, rows[0]); _profiles[playerId] = c;
        }
        var r = c.row;
        // trang bị bản game khác (đã đổi mã, vd. Thương → Kiếm) quy về mã hiện tại; món lạ giữ nguyên để giao diện hiện "?"
        var eq = (r.Equipment ?? new()).ToDictionary(kv => kv.Key, kv => GameData.ReplaceSpear(kv.Value) ?? GameData.UpgradeGear(kv.Value) ?? kv.Value);
        return new LbProfileDto(r.Name, r.Realm, r.Stage, r.RealmName, r.StageName, r.Power, eq, r.Stats ?? new(), DateTime.SpecifyKind(r.UpdatedAt, DateTimeKind.Utc), r.PlayerId == me.Id, (await TitlesAsync()).GetValueOrDefault(r.PlayerId));
    }

    private sealed record TitleRow(string Category, Guid PlayerId);
    private (DateTime at, Dictionary<Guid, List<TitleDto>> map) _titles = (DateTime.MinValue, new());
    private volatile bool _titlesMissing;

    /// <summary>Danh hiệu Đệ nhất (view titles_public, docs/arena/supabase.sql): người chơi → các danh hiệu đang giữ.
    /// Lỗi mạng hoặc chưa chạy SQL thì trả về rỗng, bảng xếp hạng vẫn hiện bình thường.</summary>
    public async Task<Dictionary<Guid, List<TitleDto>>> TitlesAsync()
    {
        if (!Enabled || _titlesMissing) return new();
        if (DateTime.UtcNow - _titles.at < CacheFor) return _titles.map;
        try
        {
            using var res = await _http.GetAsync("titles_public?select=category,player_id");
            if (!res.IsSuccessStatusCode)
            {
                if (res.StatusCode == System.Net.HttpStatusCode.NotFound) _titlesMissing = true;   // chưa chạy docs/arena/supabase.sql
                return _titles.map;
            }
            var rows = await res.Content.ReadFromJsonAsync<List<TitleRow>>(Json) ?? [];
            var map = rows.Where(x => Titles.ById.ContainsKey(x.Category)).GroupBy(x => x.PlayerId)
                .ToDictionary(g => g.Key, g => g.Select(x => Titles.ById[x.Category]).OrderBy(t => Array.IndexOf(Titles.All, t)).ToList());
            _titles = (DateTime.UtcNow, map);
        }
        catch (Exception ex) { log.LogWarning("Leaderboard titles: {Error}", ex.Message); }
        return _titles.map;
    }

    /// <summary>Bỏ cache danh hiệu (sau trận Lôi đài đổi hạng 1).</summary>
    public void ForgetTitles() => _titles = (DateTime.MinValue, _titles.map);
}
