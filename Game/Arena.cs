using System.Net.Http.Json;
using System.Text.Json;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>Danh hiệu của người đứng hạng 1 một bảng xếp hạng (view titles_public).</summary>
public record TitleDto(string Id, string Name, string Desc);

public static class Titles
{
    // thứ tự = thứ tự hiện cạnh tên khi một người giữ nhiều danh hiệu
    public static readonly TitleDto[] All =
    [
        new("arena", "Lôi Đài Bá Chủ", "Đệ nhất Lôi đài"),
        new("power", "Chiến Lực Vô Song", "Đệ nhất bảng Lực chiến"),
        new("realm", "Cảnh Giới Chí Tôn", "Đệ nhất bảng Cảnh giới"),
        new("progress", "Phá Thiên Giả", "Đệ nhất bảng Vượt ải"),
        new("pet", "Vạn Thú Chi Vương", "Đệ nhất bảng Linh thú"),
    ];
    public static readonly Dictionary<string, TitleDto> ById = All.ToDictionary(t => t.Id);
}

public record ArenaRowDto(long Rank, Guid Id, string Name, int Realm, string RealmName, string StageName, long Power, int Wins, int Losses,
    bool Me, bool CanFight, List<TitleDto>? Titles, bool Practice = false);   // Practice: xếp dưới mình, đánh là tỷ thí (không đổi hạng)
public record ArenaHistoryDto(bool Attacker, string Other, bool Win, long From, long To, DateTime At, bool Practice = false);
public record ArenaDto(bool Enabled, string? Blocked, string? Error, ArenaRowDto? Me, List<ArenaRowDto> Top, List<ArenaRowDto> Targets,
    long Window, int Used, int Daily, long ReadyInMs, long Total, List<ArenaHistoryDto> History, bool Mock = false);
/// <summary>Tóm tắt trận cho tab Lôi đài; trận diễn ở màn hình chính qua StateDto.Arena (ArenaReplayDto).</summary>
public record ArenaFightDto(bool Win, bool Ko, string Foe, long OldRank, long NewRank, int Turns, bool Practice = false);
/// <summary>Trận Lôi đài vừa đánh, để màn hình chính (dải / cửa sổ chính) diễn lại một lần: Battle như trận đánh quái, Pvp là phần riêng của đối thủ.</summary>
public record ArenaReplayDto(long Id, BattleDto Battle, PvpBattleDto Pvp, long OldRank, long NewRank, DateTime At, bool Practice = false);
public record ArenaFightResponse(StateDto State, string? Message, string? Kind, ArenaFightDto Fight);

/// <summary>
/// Lôi đài PK giữa người chơi trên Supabase (docs/arena/supabase.sql), dùng chung cấu hình "Leaderboard".
/// Supabase giữ bậc thang hạng, lượt và seed; game tính trận có đủ kỹ năng (PvpCombat) rồi báo kết quả (arena_begin → arena_finish).
/// </summary>
public sealed class ArenaService(LeaderboardOptions opt, ILogger<ArenaService> log, IConfiguration? cfg = null, IHostEnvironment? env = null)
{
    // Chỉ để hiển thị; luật thật nằm ở arena_rules() trong SQL — sửa thì sửa cả hai.
    public const int Daily = 15, CooldownSec = 20;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private readonly HttpClient _http = Client(opt);

    /// <summary>Lôi đài giả (ArenaMock): chỉ khi chạy Development và bật "Arena:Mock" (vd. dotnet run -- --Arena:Mock=true). Không gọi Supabase.</summary>
    public bool Mock { get; } = env?.IsDevelopment() == true && cfg?.GetValue<bool>("Arena:Mock") == true;
    private readonly Lazy<ArenaMock> _mock = new(() => new ArenaMock());
    public bool Enabled => opt.Enabled || Mock;

    public Task<StateRow> StateAsync(Player p) =>
        Mock ? Task.FromResult(_mock.Value.State(p)) : Rpc<StateRow>("arena_state", new { p_id = p.Id, p_secret = p.LbSecret });

    /// <summary>Bắt đầu trận: Supabase kiểm tra lượt / tầm, cấp seed và trả ảnh chụp kỹ năng của đối thủ.</summary>
    public Task<BeginRow> BeginAsync(Player p, Guid target, PvpFighter me) =>
        Mock ? Task.FromResult(_mock.Value.Begin(p, target)) : Rpc<BeginRow>("arena_begin", new { p_id = p.Id, p_secret = p.LbSecret, p_target = target, p_loadout = me });

    /// <summary>Báo kết quả trận vừa bắt đầu (một lần, trong 3 phút); thắng thì Supabase đổi hạng.</summary>
    public Task<FinishRow> FinishAsync(Player p, long fight, bool win) =>
        Mock ? Task.FromResult(_mock.Value.Finish(p, fight, win)) : Rpc<FinishRow>("arena_finish", new { p_id = p.Id, p_secret = p.LbSecret, p_fight = fight, p_win = win });

    private static readonly TimeSpan LoadoutGap = TimeSpan.FromSeconds(30), LoadoutRefresh = TimeSpan.FromMinutes(10);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, (DateTime at, string key)> _loadouts = new();

    /// <summary>Gọi sau mỗi lần lưu: gửi nền ảnh chụp kỹ năng khi đổi (tối đa 30 giây / lần) để người khác đánh với bản mới nhất.
    /// Chưa vào Lôi đài thì Supabase bỏ qua, 10 phút sau mới thử lại.</summary>
    public void TrackLoadout(Player p, bool excluded)
    {
        if (Mock || !opt.Enabled || excluded || p.LbSecret.Length == 0) return;
        var me = GameService.PvpFighterOf(p);
        var key = JsonSerializer.Serialize(me);
        var now = DateTime.UtcNow;
        if (_loadouts.TryGetValue(p.Id, out var last) && now - last.at < (last.key == key || last.key == "" ? LoadoutRefresh : LoadoutGap)) return;
        _loadouts[p.Id] = (now, key);
        var (id, secret) = (p.Id, p.LbSecret);
        _ = Task.Run(async () =>
        {
            try { if (!await Rpc<bool>("arena_set_loadout", new { p_id = id, p_secret = secret, p_loadout = me })) _loadouts[id] = (now, ""); }
            catch (GameException) { _loadouts[id] = (now, ""); }     // chưa chạy SQL / mất mạng: thử lại sau
        });
    }

    /// <summary>Ảnh chụp đối thủ do máy khác gửi: kẹp các giá trị về giới hạn của game, chỉ số gốc lấy theo bảng xếp hạng.</summary>
    public static PvpFighter Sanitize(PvpFighter f, string name, int realm, Dictionary<string, double>? stats)
    {
        double S(string k, double fallback) => stats is not null && stats.TryGetValue(k, out var v) ? v : fallback;
        var r = GameData.Realms[Math.Clamp(realm, 0, GameData.Realms.Count - 1)];
        var basic = SkillData.ById.TryGetValue(f.Basic ?? "", out var b) && b.Tree == "basic" ? b : SkillData.ById["hoa_cau"];
        var skills = (f.Skills ?? []).Where(k => SkillData.ById.TryGetValue(k.Id ?? "", out var s) && s.Tree == "active").Take(SkillData.SkillSlotCount - 1)
            .Select(k => k with { Lv = Math.Clamp(k.Lv, 1, SkillData.ById[k.Id].MaxLv) }).ToList();
        static double C(double v, double max) => double.IsFinite(v) ? Math.Clamp(v, 0, max) : 0;
        return f with
        {
            Name = name, Realm = realm, Stage = Math.Clamp(f.Stage, 0, r.Stages.Length - 1),
            Hero = f.Hero?.StartsWith("img/") == true ? f.Hero : r.Hero, HeroStand = f.HeroStand?.StartsWith("img/") == true ? f.HeroStand : r.HeroStand,
            Atk = (int)Math.Max(1, S("atk", f.Atk)), Hp = (int)Math.Max(1, S("hp", f.Hp)), Def = (int)Math.Max(0, S("def", f.Def)),
            Crit = C(S("crit", f.Crit), 100), Speed = C(S("speed", f.Speed), 60), CritMult = C(f.CritMult, 3),
            Basic = basic.Id, BasicPct = basic.DmgBase, BasicCrit = basic.Crit, Skills = skills, Manual = f.Manual ?? "",
            SkillPct = C(f.SkillPct, 500), Pen = C(f.Pen, 80), Shield = C(f.Shield, 60), Dodge = C(f.Dodge, CombatDodge.MaxChance), AtkDown = C(f.AtkDown, 40),
            GuardPct = C(f.GuardPct, 100), FirstHitPct = C(f.FirstHitPct, 1000), Lightning = C(f.Lightning, 100), LightningPct = C(f.LightningPct, 1000),
            Regen = C(f.Regen, 20), Reflect = C(f.Reflect, 100), ExtraHit = C(f.ExtraHit, 50), Rage = C(f.Rage, SkillData.RageMax),
            WoodHeal = C(f.WoodHeal, 50), WoodEmergency = C(f.WoodEmergency, 100), Divine = f.Divine is >= 6 and <= 9 ? f.Divine : 0,
        };
    }

    private static HttpClient Client(LeaderboardOptions o)
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        if (!o.Enabled) return h;
        h.BaseAddress = new Uri(o.Url.TrimEnd('/') + "/rest/v1/");
        h.DefaultRequestHeaders.Add("apikey", o.AnonKey);
        h.DefaultRequestHeaders.Add("Authorization", $"Bearer {o.AnonKey}");
        h.DefaultRequestHeaders.Add("X-Game-Version", AppVersion.Current);   // Supabase chặn bản cũ (require_version)
        return h;
    }

    public sealed record Row(Guid PlayerId, string Name, int Realm, int Stage, string RealmName, string StageName, long Power, int Wins, int Losses, long Rank);
    public sealed record HistoryRow(long Id, bool Attacker, string OtherName, bool Win, long FromRank, long ToRank, DateTime CreatedAt, bool Practice = false);
    public sealed record StateRow(Row? Me, List<Row> Top, List<Row> Targets, long Window, int Used, int Daily, DateTime? ReadyAt, long Total, List<HistoryRow> History);
    public sealed record BeginRow(long Id, long Seed, string FoeName, int FoeRealm, JsonElement? FoeLoadout, Dictionary<string, double>? FoeStats, long OldRank, long FoeRank, bool Practice = false);
    public sealed record FinishRow(long? OldRank, long? NewRank);

    /// <summary>Gọi hàm SQL; lỗi do luật Lôi đài (RAISE EXCEPTION) trả về đúng câu tiếng Việt cho người chơi.</summary>
    public async Task<T> Rpc<T>(string fn, object body)
    {
        HttpResponseMessage res;
        try { res = await _http.PostAsync($"rpc/{fn}", new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning("Arena {Fn}: {Error}", fn, ex.Message);
            throw new GameException("Không kết nối được Lôi đài. Kiểm tra mạng rồi thử lại.");
        }
        using (res)
        {
            var text = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                string? msg = null;
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    var code = doc.RootElement.TryGetProperty("code", out var c) ? c.GetString() : null;
                    if (code == "P0001") msg = doc.RootElement.GetProperty("message").GetString();
                    else if (code is "PGRST202" or "42883") msg = "Cần chạy docs/arena/supabase.sql trên Supabase để mở Lôi đài.";
                }
                catch (JsonException) { }
                if (msg is null) log.LogWarning("Arena {Fn}: {Status} {Body}", fn, (int)res.StatusCode, text);
                throw new GameException(msg ?? "Lôi đài đang gặp lỗi, thử lại sau.");
            }
            try { return JsonSerializer.Deserialize<T>(text, Json)!; }
            catch (JsonException ex)
            {
                log.LogWarning("Arena {Fn}: {Error} {Body}", fn, ex.Message, text);
                throw new GameException("Lôi đài trả dữ liệu lạ, thử lại sau.");
            }
        }
    }
}
