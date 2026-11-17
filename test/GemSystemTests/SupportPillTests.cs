using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class SupportPillTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    private async Task<(GameService Game, Player Player)> Setup()
    {
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        var game = new GameService(db);
        var p = await game.CreateAsync("Đan hỗ trợ");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        p.ExploreReadyAt = DateTime.UtcNow.AddHours(1);
        return (game, p);
    }

    [Fact]
    public void EveryPillHasThreeQualitiesAndAnIcon()
    {
        foreach (var d in SupportPills.All)
        {
            Assert.Equal("elixir", GameData.ItemById[d.Id].Type);
            Assert.True(GameData.ItemById.ContainsKey(d.Id + "_trung"));
            Assert.True(GameData.ItemById.ContainsKey(d.Id + "_thuong"));
            Assert.Contains(GameData.Brews, b => b.Out == d.Id);
            Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot", "img", "dan", d.Id + ".png"))
                || File.Exists(Path.Combine("..", "..", "..", "..", "..", "wwwroot", "img", "dan", d.Id + ".png")));
        }
    }

    [Fact]
    public async Task SameQualityExtendsUpToTheCap()
    {
        var (game, p) = await Setup();
        p.Add(SupportPills.TuLinh, 20);
        var now = DateTime.UtcNow;
        SupportPills.Use(p, SupportPills.TuLinh, 2, now);
        Assert.Equal(3600, (SupportPills.Active(p, SupportPills.TuLinh, now)!.Value.Until - now).TotalSeconds, 0);
        SupportPills.Use(p, SupportPills.TuLinh, 0, now);
        Assert.Equal(4 * 3600, (SupportPills.Active(p, SupportPills.TuLinh, now)!.Value.Until - now).TotalSeconds, 0);
        Assert.Equal(12, p.Count(SupportPills.TuLinh));
        Assert.Throws<GameException>(() => SupportPills.Use(p, SupportPills.TuLinh, 1, now));
    }

    [Fact]
    public async Task HigherQualityUpgradesAndLowerIsRefused()
    {
        var (game, p) = await Setup();
        var now = DateTime.UtcNow;
        p.Add(SupportPills.TatHanh, 1); p.Add(SupportPills.TatHanh + "_thuong", 1);
        SupportPills.Use(p, SupportPills.TatHanh, 1, now);
        SupportPills.Use(p, SupportPills.TatHanh + "_thuong", 1, now);
        var b = SupportPills.Active(p, SupportPills.TatHanh, now)!.Value;
        Assert.Equal(2, b.Q);
        Assert.Equal(3600, (b.Until - now).TotalSeconds, 0);
        p.Add(SupportPills.TatHanh, 1);
        Assert.Throws<GameException>(() => SupportPills.Use(p, SupportPills.TatHanh, 1, now));
        Assert.Equal(.35, SupportPills.Value(p, SupportPills.TatHanh, now, 0), 6);
        Assert.Equal(0, SupportPills.Value(p, SupportPills.TatHanh, b.Until.AddSeconds(1), 0), 6);
    }

    [Fact]
    public async Task OverlapStopsAtExpiry()
    {
        var (game, p) = await Setup();
        var now = DateTime.UtcNow;
        p.Add(SupportPills.BeQuan, 1);
        SupportPills.Use(p, SupportPills.BeQuan, 1, now);
        Assert.Equal(3600, SupportPills.Overlap(p, SupportPills.BeQuan, now, now.AddHours(1)), 0);
        Assert.Equal(8 * 3600, SupportPills.Overlap(p, SupportPills.BeQuan, now, now.AddHours(20)), 0);
    }

    [Fact]
    public async Task DanTamMultipliesAlchemyExp()
    {
        var (game, p) = await Setup();
        p.Add("tu_linh_thao", 100); p.Add("bach_diep_thao", 50); p.Add("linh_thach", 10_000);
        p.Add(SupportPills.DanTam + "_trung", 1);
        game.UseItem(p, SupportPills.DanTam + "_trung");
        var (_, _, craft) = game.Brew(p, "tu_khi_dan", 10, new Random(1));
        var ok = (int)craft.Outputs!.Sum(o => o.Qty);                 // lò hỏng chỉ nhận FailExpRate EXP
        Assert.Equal((int)Math.Round(2 * (ok + (10 - ok) * Alchemy.FailExpRate) * Alchemy.ExpPerPill(1)), p.AlchemyExp);
    }

    [Fact]
    public async Task UsingFromBagGoesThroughUseItem()
    {
        var (game, p) = await Setup();
        p.Add(SupportPills.ThuLinh, 1);
        game.UseItem(p, SupportPills.ThuLinh);
        Assert.Equal(0, p.Count(SupportPills.ThuLinh));
        Assert.Equal(1.5, SupportPills.Value(p, SupportPills.ThuLinh, DateTime.UtcNow, 1), 6);
        Assert.Single(SupportPills.State(p, DateTime.UtcNow));
    }

    [Fact]
    public async Task DailyCapLimitsHoursPerKind()
    {
        var (_, p) = await Setup();
        var now = new DateTime(2026, 11, 16, 18, 0, 0, DateTimeKind.Utc);   // 1h sáng UTC+7: các mốc +16h vẫn trong cùng ngày
        p.Add(SupportPills.TuLinh, 40);
        SupportPills.Use(p, SupportPills.TuLinh, 0, now);   // đầy 4h
        SupportPills.Use(p, SupportPills.TuLinh, 0, now.AddHours(4));   // 4h nữa: chạm 8h/ngày
        Assert.Equal(24, p.Count(SupportPills.TuLinh));
        Assert.Equal(SupportPills.DailySeconds, SupportPills.UsedToday(p, SupportPills.TuLinh, now.AddHours(4)), 0);
        Assert.Throws<GameException>(() => SupportPills.Use(p, SupportPills.TuLinh, 1, now.AddHours(8)));
        Assert.Equal(0, SupportPills.UsedToday(p, SupportPills.TuLinh, now.AddDays(1)), 0);   // sang ngày mới làm lại
    }

    [Fact]
    public async Task DailyCapTrimsTheLastPill()
    {
        var (_, p) = await Setup();
        var now = new DateTime(2026, 11, 16, 18, 0, 0, DateTimeKind.Utc);   // 1h sáng UTC+7: các mốc +16h vẫn trong cùng ngày
        p.Add(SupportPills.BeQuan, 5);
        SupportPills.Use(p, SupportPills.BeQuan, 2, now);   // 16h
        SupportPills.Use(p, SupportPills.BeQuan, 0, now.AddHours(15));   // còn 1h + nạp 8h (đủ 24h/ngày)
        Assert.Equal(2, p.Count(SupportPills.BeQuan));   // chỉ tiêu 1 viên, không ăn thêm viên khi đã chạm trần ngày
        Assert.Equal(9 * 3600, (SupportPills.Active(p, SupportPills.BeQuan, now.AddHours(15))!.Value.Until - now.AddHours(15)).TotalSeconds, 0);
        Assert.Throws<GameException>(() => SupportPills.Use(p, SupportPills.BeQuan, 1, now.AddHours(16)));
    }

    [Fact]
    public async Task AtMostThreeKindsAtOnce()
    {
        var (_, p) = await Setup();
        var now = DateTime.UtcNow;
        foreach (var id in new[] { SupportPills.TuLinh, SupportPills.DanTam, SupportPills.TatHanh, SupportPills.ThuLinh }) p.Add(id, 2);
        SupportPills.Use(p, SupportPills.TuLinh, 1, now);
        SupportPills.Use(p, SupportPills.DanTam, 1, now);
        SupportPills.Use(p, SupportPills.TatHanh, 1, now);
        Assert.Throws<GameException>(() => SupportPills.Use(p, SupportPills.ThuLinh, 1, now));
        SupportPills.Use(p, SupportPills.TuLinh, 1, now);   // loại đang có vẫn cộng được
        SupportPills.Use(p, SupportPills.ThuLinh, 1, now.AddMinutes(31));   // Đan Tâm, Tật Hành hết hạn
        Assert.Equal(1, p.Count(SupportPills.ThuLinh));
    }

    /// <summary>Tu vi nhận trong 1 giờ vắng mặt (không tính thám hiểm).</summary>
    private static async Task<decimal> HourGain(GameService game, Player p)
    {
        p.Qi = 0; p.LastTickAt = DateTime.UtcNow.AddHours(-1); p.ExploreReadyAt = DateTime.UtcNow.AddHours(1);
        await game.LoadAsync(p.Id);
        return p.Qi;
    }

    [Fact]
    public async Task TuLinhAndBeQuanBoostIdleQiButDoNotStack()
    {
        var (game, p) = await Setup();
        p.Realm = 5; p.Stage = 0;
        var plain = await HourGain(game, p);
        Assert.True(plain > 0);
        p.Add(SupportPills.TuLinh, 1); SupportPills.Use(p, SupportPills.TuLinh, 1, DateTime.UtcNow.AddHours(-1));
        Assert.Equal(1.15, (double)(await HourGain(game, p) / plain), 2);   // 1 viên = 30 phút: chỉ nửa giờ được +30%
        p.Add(SupportPills.TuLinh, 2); SupportPills.Use(p, SupportPills.TuLinh, 2, DateTime.UtcNow.AddHours(-1));
        Assert.Equal(1.3, (double)(await HourGain(game, p) / plain), 2);
        p.Add(SupportPills.BeQuan + "_thuong", 1); SupportPills.Use(p, SupportPills.BeQuan + "_thuong", 1, DateTime.UtcNow.AddHours(-1));
        Assert.Equal(2.0, (double)(await HourGain(game, p) / plain), 2);   // lấy mức có lợi hơn (Bế Quan ×2), không cộng Tụ Linh
    }

    public void Dispose() => db.Dispose();
}
