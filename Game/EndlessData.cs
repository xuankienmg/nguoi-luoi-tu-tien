namespace VanDao.Game;

/// <summary>Mức Vô Tận (Diff 5): tầng tăng dần thay cho ải, không có chương. Mở khi hạ Boss Địa Ngục 2 chương 10.
/// Ảnh quái / Boss riêng: 5 chủ đề trong EndlessThemes (wwwroot/img/endless). Player.Chapter luôn 0, Player.Area = tầng đang đánh,
/// tầng cao nhất đã mở lưu ở DiffProg "5.0" (tầng cao nhất đã hạ + 1, cùng quy ước các mức khác). Chưa có bảng xếp hạng tầng (Leaderboard để sau).</summary>
public static class EndlessData
{
    public const int Diff = 5;
    /// <summary>Trần tầng. Chỉ số quái tăng mũ nhỏ, kiểm tra không chạm CombatNumber.Max trong dải này (test EndlessDataTests).</summary>
    public const int MaxFloor = 300;
    /// <summary>Mỗi tầng 3 đợt như ải thường; mỗi 5 tầng (lẻ bội 5) có tinh anh ở đợt cuối, mỗi 10 tầng là tầng Boss (1 đợt Boss);
    /// mỗi 25 tầng đổi chủ đề quái.</summary>
    public const int Waves = GameData.Waves, EliteEvery = 5, BossEvery = 10, ThemeSpan = 25;
    /// <summary>Chỉ số quái = mốc quái ải 15 / Boss Địa Ngục 2 chương 10 × StartScale × GrowthPerFloor^(tầng−1).
    /// Mốc đã nhân GameData.MonsterEase (Địa Ngục 2 giảm 30%), nên tầng 1 bằng quái cuối Địa Ngục 2 sau khi giảm.</summary>
    public const double StartScale = 1, GrowthPerFloor = 1.03;
    /// <summary>Linh thạch mỗi quái +RewardPerFloor mỗi tầng (tuyến tính, chậm hơn nhiều so với chỉ số quái → chống treo vô hạn).</summary>
    public const double RewardPerFloor = .02;

    public static int ClampFloor(int floor) => Math.Clamp(floor, 1, MaxFloor);
    public static bool IsBoss(int floor) => floor >= 1 && floor % BossEvery == 0;
    public static bool IsElite(int floor) => floor >= 1 && floor % EliteEvery == 0 && !IsBoss(floor);
    public static int Cycle(int floor) => (ClampFloor(floor) - 1) / ThemeSpan;
    public static EndlessThemes.Theme ThemeAt(int floor) => EndlessThemes.All[Cycle(floor) % EndlessThemes.All.Count];
    public static string ThemeName(int floor) => ThemeAt(floor).Name;
    public static string PlaceOf(int floor) => ThemeName(floor);   // nhãn "tầng N" do nơi gọi ghép thêm

    /// <summary>Hệ số chỉ số quái ở tầng floor (tầng 1 = StartScale).</summary>
    public static double StatScale(int floor) => StartScale * Math.Pow(GrowthPerFloor, ClampFloor(floor) - 1);
    /// <summary>Hệ số linh thạch ở tầng floor (tầng 1 = 1).</summary>
    public static double RewardMult(int floor) => 1 + RewardPerFloor * (ClampFloor(floor) - 1);

    // Mốc neo: quái thường / tinh anh ải 15 và Boss chương 10 Địa Ngục 2. Máu quái Địa Ngục đã nhân 3/5 vì 5 đợt → trả về đủ cho 3 đợt.
    private static readonly Lazy<(MonsterDef Normal, MonsterDef Elite, MonsterDef Boss)> Anchor = new(() =>
    {
        var ch = HellMapData.Chapter(4, 9);
        var stage = HellMapData.AreaWaves(ch, GameData.Areas, 4);
        var boss = HellMapData.AreaWaves(ch, GameData.BossAreaFor(4), 4)[0];
        MonsterDef Ease(MonsterDef m) => m with { Hp = CombatNumber.Scale(m.Hp, GameData.MonsterEase), Atk = CombatNumber.Scale(m.Atk, GameData.MonsterEase), Def = CombatNumber.Scale(m.Def, GameData.MonsterEase) };
        MonsterDef Full(MonsterDef m) => Ease(m with { Hp = CombatNumber.Scale(m.Hp, 1 / HellMapData.WaveShare) });
        return (Full(stage[0]), Full(stage[^1]), Ease(boss));
    });

    private static MonsterDef Make(MonsterDef anchor, string name, string image, string tier, string id, double k) =>
        MonsterCombatRatings.Apply(anchor with {
            Name = name, Image = image, Tier = tier, Id = id,
            Hp = CombatNumber.Scale(anchor.Hp, k), Atk = CombatNumber.Scale(anchor.Atk, k), Def = CombatNumber.Scale(anchor.Def, k)
        }, Diff);

    /// <summary>Các đợt quái của tầng: Boss 1 đợt; tầng thường 3 đợt (hai loài trong chủ đề, tầng tinh anh đợt cuối là tinh anh).</summary>
    public static MonsterDef[] WavesAt(int floor)
    {
        floor = ClampFloor(floor);
        var k = StatScale(floor); var a = Anchor.Value;
        if (IsBoss(floor))
        {
            var boss = ThemeAt(floor).Boss;   // mỗi chủ đề một Boss, dùng ở mọi tầng Boss của chủ đề đó
            return [Make(a.Boss, boss.Name, boss.Image, "boss", "", k)];
        }
        var theme = ThemeAt(floor);
        var kind = (floor - 1) % ThemeSpan / EliteEvery;                 // 0..4: loài chính của đoạn 5 tầng
        var next = Math.Min(4, kind + 1);
        var waves = new MonsterDef[Waves];
        for (var i = 0; i < Waves; i++)
        {
            var last = i == Waves - 1;
            if (last && IsElite(floor)) { var e = theme.Elites[floor / BossEvery % theme.Elites.Length]; waves[i] = Make(a.Elite, e.Name, e.Image, "elite", "", k); }
            else { var m = theme.Commons[i == 1 ? next : kind]; waves[i] = Make(a.Normal, m.Name, m.Image, "normal", "", k); }
        }
        return waves;
    }

    /// <summary>Tầng cao nhất đã hạ (0 = chưa hạ tầng nào), từ tầng cao nhất đã mở.</summary>
    public static int Cleared(int unlockedFloor) => Math.Clamp(unlockedFloor - 1, 0, MaxFloor);
}
