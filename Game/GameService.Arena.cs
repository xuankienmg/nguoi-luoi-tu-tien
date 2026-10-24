using VanDao.Data;

namespace VanDao.Game;

/// <summary>Lôi đài: gửi chỉ số mới nhất lên bảng xếp hạng, rồi để Supabase (ArenaService) tính trận và đổi hạng.</summary>
public partial class GameService
{
    private string? ArenaBlocked(Player p) =>
        lb is not null && lb.Excluded(p) ? "Nhân vật đã dùng admin mode nên không vào được Lôi đài. Tu lại từ đầu (không bật cờ admin) để tham gia."
        : p.LbSecret.Length == 0 ? "Nhân vật chưa có khóa bảng xếp hạng."
        : null;


    /// <summary>Ảnh nhân vật hiện trên Lôi đài: trang phục đang mặc, nữ chưa chọn trang phục thì theo trang phục nữ của cảnh giới (khớp skinArt ở core.js).</summary>
    private static (string hero, string stand) PvpArt(Player p)
    {
        var skin = GameData.SkinById.GetValueOrDefault(CurrentSkin(p))
            ?? (CurrentGender(p) == "female" ? GameData.Skins.Where(s => s.Gender == "female" && s.MinRealm <= p.Realm && SkinWearable(p, s)).OrderByDescending(s => s.MinRealm).FirstOrDefault() : null);
        var r = Realm(p);
        return (skin?.Hero ?? r.Hero, skin?.HeroStand ?? r.HeroStand);
    }

    /// <summary>Ảnh chụp mọi thứ tu sĩ mang vào Lôi đài, tính giống BattleAt (đòn thường + tuyệt kỹ, bị động, hiệu ứng trang bị, thần thông).</summary>
    public static PvpFighter PvpFighterOf(Player p)
    {
        var st = Stats(p);
        var fx = GearEffects(p);
        var (skillPct, pen, burnPct) = SkillExtra(p);
        var lv = SkillLevels(p);
        var manual = CultivationManuals.Active(p);
        var basicId = p.Slots()[SkillData.BasicSlot];
        if (!(SkillData.ById.TryGetValue(basicId, out var bdef) && bdef.Tree == "basic")) { basicId = "hoa_cau"; bdef = SkillData.ById[basicId]; }
        var skills = p.Slots().Skip(1).Where(id => id.Length > 0 && lv.GetValueOrDefault(id) > 0 && SkillData.ById.ContainsKey(id))
            .Select(id => new PvpSkill(id, lv[id])).ToList();
        double Wood(Func<SkillDef, double> value) => lv.Sum(kv => SkillData.ById.TryGetValue(kv.Key, out var def) && def.Element == "moc" ? value(def) * def.Factor(kv.Value) * CultivationManuals.Rate(manual, "moc") : 0);
        var (hero, stand) = PvpArt(p);
        return new PvpFighter(p.Name, p.Realm, p.Stage, hero, stand,
            st.Atk, st.Hp, st.Def, st.Crit, st.Speed, .5 + PetTalent(p, "critdmg") / 100,
            basicId, bdef!.DmgBase, bdef.Crit, skills, manual,
            skillPct + fx.SkillPct, Math.Min(80, pen + fx.Pen + PetTalent(p, "pen")), burnPct > 0,
            Math.Min(60, fx.Guard + PetTalent(p, "guard")), CombatDodge.Chance(st.Speed, fx.Dodge, 0), Math.Min(40, SkillBonus(p).monAtk + fx.MonAtkDown),
            fx.Shield, fx.FirstHit, fx.Lightning, fx.LightningPct,
            fx.Regen + PetTalent(p, "regen"), fx.Reflect, Math.Min(30, fx.ExtraHit + PetTalent(p, "extra")), fx.Rage,
            Wood(s => s.HealOnCastPct), Wood(s => s.EmergencyHealPct), DivineAbility.For(p).Tier);
    }

    private ArenaService Arena(Player p)
    {
        if (_arena is not { Enabled: true } || lb is null) throw new GameException("Lôi đài chưa được cấu hình (appsettings.json → Leaderboard).");
        if (ArenaBlocked(p) is { } why) throw new GameException(why);
        return _arena;
    }

    public async Task<ArenaDto> ArenaAsync(Player p)
    {
        var empty = new ArenaDto(_arena is { Enabled: true } && lb is not null, ArenaBlocked(p), null, null, [], [], 0, 0, ArenaService.Daily, 0, 0, [], _arena?.Mock == true);
        if (!empty.Enabled || empty.Blocked is not null) return empty;
        try
        {
            var a = Arena(p);
            if (!a.Mock) await lb!.SubmitNowAsync(p);     // lực chiến / cảnh giới trên Lôi đài khớp nhân vật hiện tại
            var s = await a.StateAsync(p);
            var titles = a.Mock ? MockTitles(s) : await lb!.TitlesAsync();
            var myRank = s.Me?.Rank ?? long.MaxValue;
            ArenaRowDto Dto(ArenaService.Row x) => new(x.Rank, x.PlayerId, x.Name, x.Realm, x.RealmName, x.StageName, x.Power, x.Wins, x.Losses,
                x.PlayerId == p.Id, x.Rank > myRank || x.Rank < myRank && myRank - x.Rank <= s.Window, titles.GetValueOrDefault(x.PlayerId), x.Rank > myRank);
            var readyIn = s.ReadyAt is { } at ? Math.Max(0, (long)(at.ToUniversalTime() - DateTime.UtcNow).TotalMilliseconds) : 0;
            return empty with
            {
                Me = s.Me is null ? null : Dto(s.Me), Top = s.Top.Select(Dto).ToList(), Targets = s.Targets.Select(Dto).ToList(),
                Window = s.Window, Used = s.Used, Daily = s.Daily, ReadyInMs = readyIn, Total = s.Total,
                History = s.History.Select(h => new ArenaHistoryDto(h.Attacker, h.OtherName, h.Win, h.FromRank, h.ToRank, DateTime.SpecifyKind(h.CreatedAt.ToUniversalTime(), DateTimeKind.Utc), h.Practice)).ToList(),
            };
        }
        catch (GameException ex) { return empty with { Error = ex.Message }; }
    }

    /// <summary>Lôi đài giả: chỉ có danh hiệu Lôi Đài Bá Chủ cho hạng 1 giả (không đọc danh hiệu thật trên Supabase).</summary>
    private static Dictionary<Guid, List<TitleDto>> MockTitles(ArenaService.StateRow s) =>
        s.Top.FirstOrDefault(x => x.Rank == 1) is { } top ? new() { [top.PlayerId] = [Titles.ById["arena"]] } : new();

    /// <summary>Trận Lôi đài vừa đánh của mỗi người chơi, để màn hình chính lấy qua StateDto và diễn lại (giữ 2 phút).</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, ArenaReplayDto> ArenaReplays = new();
    private static ArenaReplayDto? ArenaReplay(Player p) =>
        ArenaReplays.TryGetValue(p.Id, out var r) && DateTime.UtcNow - r.At < TimeSpan.FromMinutes(2) ? r : null;

    public async Task<(string msg, string kind, ArenaFightDto fight)> ArenaFightAsync(Player p, Guid target)
    {
        var a = Arena(p);
        // lực chiến / danh hiệu trên bảng khớp nhân vật hiện tại; kỹ năng của mình gửi kèm arena_begin
        if (!a.Mock && !await lb!.SubmitNowAsync(p)) throw new GameException("Không gửi được chỉ số lên bảng xếp hạng. Kiểm tra mạng rồi thử lại.");
        var me = PvpFighterOf(p);
        var b = await a.BeginAsync(p, target, me);
        PvpFighter? sent = null;
        try { sent = b.FoeLoadout is { ValueKind: System.Text.Json.JsonValueKind.Object } el ? System.Text.Json.JsonSerializer.Deserialize<PvpFighter>(el) : null; }
        catch (System.Text.Json.JsonException) { }             // ảnh chụp bản game khác: đánh bằng chỉ số
        var foe = sent is null ? PvpFighter.MirrorOf(me, b.FoeName, b.FoeRealm, b.FoeStats) : ArenaService.Sanitize(sent, b.FoeName, b.FoeRealm, b.FoeStats);
        var mirrored = sent is null;                                   // đối thủ chưa có bộ kỹ năng: mang bộ kỹ năng giống mình
        var r = PvpCombat.Run(me, foe, (int)(Math.Abs(b.Seed) % int.MaxValue));
        var done = await a.FinishAsync(p, b.Id, r.Win);
        lb?.ForgetTitles();
        long oldRank = done.OldRank ?? b.OldRank, newRank = done.NewRank ?? oldRank;
        var (battle, pvp) = PvpCombat.ToBattle(r);
        ArenaReplays[p.Id] = new ArenaReplayDto(b.Id, battle, pvp, oldRank, newRank, DateTime.UtcNow, b.Practice);
        var note = mirrored ? $" {b.FoeName} chưa có bộ kỹ năng trên Lôi đài (bản game chưa cập nhật) nên mang bộ kỹ năng giống bạn." : "";
        var (msg, kind) = b.Practice
            ? Log(p, $"Lôi đài (tỷ thí): {(r.Win ? "đánh bại" : "bại dưới tay")} {b.FoeName}, không đổi hạng.{note}", r.Win ? "good" : "bad")
            : r.Win
            ? Log(p, newRank < oldRank
                ? $"Lôi đài: đánh bại {b.FoeName}, chiếm hạng #{newRank}{(newRank == 1 ? " — Lôi Đài Bá Chủ!" : "")}.{note}"
                : $"Lôi đài: đánh bại {b.FoeName}.{note}", newRank == 1 ? "gold" : "good")
            : Log(p, $"Lôi đài: bại dưới tay {b.FoeName}, giữ hạng #{oldRank}.{note}", "bad");
        return (msg, kind, new ArenaFightDto(r.Win, r.Ko, b.FoeName, oldRank, newRank, pvp.Turns, b.Practice));
    }
}
