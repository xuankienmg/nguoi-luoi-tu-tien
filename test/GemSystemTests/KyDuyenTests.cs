using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class KyDuyenTests : IDisposable
{
    private readonly GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
    public void Dispose() => db.Dispose();

    private async Task<(GameService Game, Player Player)> Setup()
    {
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        var game = new GameService(db);
        var p = await game.CreateAsync("Kỳ duyên");
        game.ChooseStarter(p, "hoa", PetData.StarterEgg);
        p.Add("linh_thach", 1_000_000);
        return (game, p);
    }

    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);
    private static DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0, Vn);

    [Fact]
    public void SlotsOpenAtFixedHoursForTenMinutes()
    {
        Assert.Equal("2026-11-03:08", KyDuyen.At(At(3, 8))!.Key);
        Assert.Equal("2026-11-03:08", KyDuyen.At(new DateTimeOffset(2026, 11, 3, 8, 9, 59, Vn))!.Key);
        Assert.Null(KyDuyen.At(At(3, 8, 10)));
        Assert.Null(KyDuyen.At(At(3, 7, 59)));
        Assert.Equal("2026-11-03:12", KyDuyen.At(At(3, 12, 5))!.Key);
        Assert.Equal("2026-11-03:19", KyDuyen.At(At(3, 19, 1))!.Key);
        Assert.Equal("2026-11-03:22", KyDuyen.At(At(3, 22, 9))!.Key);
        Assert.Null(KyDuyen.At(At(3, 22, 45)));
        Assert.Equal(At(3, 8, 10), KyDuyen.At(At(3, 8, 4))!.End);
    }

    [Fact]
    public void SlotsFollowVietnamTimeWhateverTheOffset()
    {
        // 01:05 UTC = 08:05 giờ Việt Nam
        Assert.Equal("2026-11-03:08", KyDuyen.At(new DateTimeOffset(2026, 11, 3, 1, 5, 0, TimeSpan.Zero))!.Key);
        // 15:05 UTC = 22:05 giờ Việt Nam cùng ngày
        Assert.Equal("2026-11-03:22", KyDuyen.At(new DateTimeOffset(2026, 11, 3, 15, 5, 0, TimeSpan.Zero))!.Key);
        // 23:30 UTC ngày 2 = 06:30 giờ Việt Nam ngày 3: ngoài khung, khung kế là 08:00 ngày 3
        var early = new DateTimeOffset(2026, 11, 2, 23, 30, 0, TimeSpan.Zero);
        Assert.Null(KyDuyen.At(early));
        Assert.Equal(At(3, 8), KyDuyen.Next(early).Start);
    }

    [Fact]
    public void RollIsDeterministicAndSureWhenChanceIsOne()
    {
        var id = Guid.Parse("60000000-0000-0000-0000-000000000001");
        var slot = KyDuyen.At(At(3, 8))!;
        Assert.True(KyDuyen.Roll(id, slot, 0, 1));
        Assert.True(KyDuyen.Roll(id, slot, 1, 1));
        Assert.Equal(KyDuyen.Roll(id, slot, 0, 0.5), KyDuyen.Roll(id, slot, 0, 0.5));
        // Ghim kết quả: phải ổn định qua các lần khởi động máy chủ
        Assert.False(KyDuyen.Roll(id, slot, 0, 0.5));
    }

    [Fact]
    public void NextSlotIsTheFollowingFixedHour()
    {
        Assert.Equal(At(3, 12), KyDuyen.Next(At(3, 9)).Start);
        Assert.Equal(At(3, 12), KyDuyen.Next(At(3, 8, 30)).Start);
        Assert.Equal(At(4, 8), KyDuyen.Next(At(3, 22, 30)).Start);
        Assert.Equal(At(3, 8), KyDuyen.Next(At(3, 0)).Start);
    }

    [Fact]
    public void DataHasTenBooksAndValidEncounters()
    {
        Assert.Equal(10, KyDuyen.Books.Length);
        Assert.True(KyDuyen.Books.Length <= GameData.Realms.Count);   // Thiên Thư 10 quyển; cảnh giới sau Chân Tiên (Kim Tiên) đã mở đủ cả 10
        Assert.Equal(13, KyDuyen.Encounters.Length);
        Assert.Equal(KyDuyen.Encounters.Length, KyDuyen.Encounters.Select(e => e.Id).Distinct().Count());
        foreach (var e in KyDuyen.Encounters)
        foreach (var c in new[] { e.A, e.B }.SelectMany(c => c.Next is { } s ? new[] { s.A, s.B } : new[] { c }))
        {
            Assert.Null(c.Next);                                    // chỉ có hai bước: bước 2 không dẫn tiếp
            Assert.InRange(c.Chance, 0.01, 1);
            Assert.Equal(c.Chance < 1, c.Lose is not null);
            foreach (var o in new[] { c.Win, c.Lose }.OfType<KyDuyen.Outcome>())
            {
                if (o.Fight is { } f) Assert.Contains(KyDuyen.Foes, x => x.Id == f);
                foreach (var (item, qty) in o.Items)
                {
                    Assert.True(GameData.ItemById.ContainsKey(item), $"{e.Id}: thiếu vật phẩm {item}");
                    Assert.True(qty > 0);
                }
            }
        }
        Assert.Contains(KyDuyen.Encounters, e => e.A.Next is not null || e.B.Next is not null);   // có hội thoại hai bước
        Assert.Contains(KyDuyen.Encounters, e => new[] { e.A.Win, e.B.Win }.Any(o => o.Fight is not null));   // có trận đánh
    }

    [Fact]
    public void EncounterIsStablePerPlayerAndSlot()
    {
        var id = Guid.Parse("60000000-0000-0000-0000-000000000001");
        var slot = KyDuyen.At(At(3, 8))!;
        Assert.Same(KyDuyen.Pick(id, slot), KyDuyen.Pick(id, slot));
        // Ghim kết quả: kỳ duyên phải giữ nguyên qua các lần khởi động máy chủ (không dùng hash ngẫu nhiên theo tiến trình).
        // Thêm / bớt kỳ duyên thì kết quả đổi theo: cập nhật id ghim cùng lúc.
        Assert.Equal("dong_phu", KyDuyen.Pick(id, slot).Id);
        var seen = Enumerable.Range(1, 40).Select(d => KyDuyen.Pick(id, KyDuyen.Make(new DateTime(2026, 1, 1).AddDays(d), 12)).Id).Distinct().Count();
        Assert.True(seen >= 5, "40 ngày phải gặp nhiều loại kỳ duyên");
    }

    [Fact]
    public void PagesFillTheLowestOpenBookThenOverflow()
    {
        var pages = new int[KyDuyen.Books.Length];
        pages[0] = 8;
        Assert.Equal(1, KyDuyen.AddPages(pages, realm: 0, 3));   // chỉ quyển 1 mở: 1 trang vào, 2 trang mất
        Assert.Equal(9, pages[0]);
        Assert.Equal(-1, KyDuyen.Target(pages, 0));
        Assert.Equal(3, KyDuyen.AddPages(pages, realm: 2, 3));
        Assert.Equal(3, pages[1]);
        pages[1] = 8;
        Assert.Equal(2, KyDuyen.AddPages(pages, realm: 2, 2));   // 1 trang đủ quyển 2, 1 trang sang quyển 3
        Assert.Equal(9, pages[1]); Assert.Equal(1, pages[2]);
    }

    [Fact]
    public void PagesRoundTripThroughThePlayerField()
    {
        var p = new Player { ThienThu = "9,4,x" };
        var pages = KyDuyen.Pages(p);
        Assert.Equal(new[] { 9, 4, 0, 0, 0, 0, 0, 0, 0, 0 }, pages);
        KyDuyen.SetPages(p, pages);
        Assert.Equal("9,4,0,0,0,0,0,0,0,0", p.ThienThu);
    }

    [Fact]
    public void TotalSumsCompletedBooksAndEndingFollowsDaoTam()
    {
        var p = new Player { ThienThu = "9,9,3" };
        Assert.Equal(KyDuyen.Books[0].Bonus + KyDuyen.Books[1].Bonus, KyDuyen.Total(p));
        Assert.Null(KyDuyen.EndingFor(p));
        var full = new Player { ThienThu = string.Join(',', Enumerable.Repeat(9, 10)), DaoTam = 12 };
        Assert.Same(KyDuyen.Endings[0], KyDuyen.EndingFor(full));
        full.DaoTam = -10; Assert.Same(KyDuyen.Endings[1], KyDuyen.EndingFor(full));
        full.DaoTam = 9; Assert.Same(KyDuyen.Endings[2], KyDuyen.EndingFor(full));
        var books = KyDuyen.Books.Aggregate(KyDuyen.Bonus.None, (a, b) => a + b.Bonus);
        Assert.Equal(books + KyDuyen.Endings[2].Bonus, KyDuyen.Total(full));
    }

    [Fact]
    public async Task ResolveOncePerSlotAndGivePages()
    {
        var (game, p) = await Setup();
        var now = At(3, 8, 5);
        var slot = KyDuyen.At(now)!;
        p.Admin = "kdpick=linh_tuyen";                             // một bước, không đánh, cả hai lối chắc chắn: test không phụ thuộc Id ngẫu nhiên
        var enc = KyDuyen.Encounters.First(x => x.Id == "linh_tuyen");
        const int sure = 0;
        var state = game.KyDuyenStateAt(p, now);
        Assert.NotNull(state.Open);
        Assert.Equal(enc.Title, state.Open!.Title);

        game.ResolveKyDuyenAt(p, sure, now);
        Assert.Equal(slot.Key, p.KyDuyenKey);
        Assert.True(KyDuyen.Pages(p).Sum() > 0);
        Assert.Null(game.KyDuyenStateAt(p, now).Open);
        Assert.Throws<GameException>(() => game.ResolveKyDuyenAt(p, sure, now.AddMinutes(2)));
        Assert.NotNull(game.KyDuyenStateAt(p, At(3, 12, 1)).Open);   // khung kế tiếp mở kỳ duyên mới
    }

    [Fact]
    public async Task TwoStepDialogueAsksASecondQuestionInTheSameSlot()
    {
        var (game, p) = await Setup();
        p.Admin = "kdpick=tien_nhan";
        var now = At(3, 12, 2);
        var enc = KyDuyen.Encounters.First(x => x.Id == "tien_nhan");
        Assert.Equal(enc.Text, game.KyDuyenStateAt(p, now).Open!.Text);

        game.ResolveKyDuyenAt(p, 1, now);                          // "Hỏi lão về Thiên Thư" → câu hỏi thứ hai
        var second = game.KyDuyenStateAt(p, now).Open!;
        Assert.Equal(enc.B.Next!.Text, second.Text);
        Assert.Equal(enc.B.Next.B.Label, second.Choices[1].Label);
        Assert.Equal(0, KyDuyen.Pages(p).Sum());

        game.ResolveKyDuyenAt(p, 1, now.AddMinutes(1));            // "Vạch mặt kẻ lừa đảo": nhìn ra manh mối
        Assert.Null(game.KyDuyenStateAt(p, now).Open);
        Assert.Equal(2, KyDuyen.Pages(p).Sum());
        Assert.Equal(2, p.DaoTam);
        Assert.Throws<GameException>(() => game.ResolveKyDuyenAt(p, 0, now.AddMinutes(2)));
    }

    [Fact]
    public async Task FallingForTheTrickCostsStonesAndGivesNothing()
    {
        var (game, p) = await Setup();
        p.Admin = "kdpick=tien_nhan";
        var now = At(3, 12, 2);
        var stones = p.Count("linh_thach");
        game.ResolveKyDuyenAt(p, 0, now);                          // "Dâng linh thạch"
        Assert.Equal(stones - 5 * KyDuyen.StoneUnit(p.Realm), p.Count("linh_thach"));
        Assert.Equal(0, KyDuyen.Pages(p).Sum());
        Assert.Null(game.KyDuyenStateAt(p, now).Open);
    }

    [Fact]
    public async Task FightRewardsOnlyOnWinAndWeakensOnLoss()
    {
        var (game, p) = await Setup();                             // nhân vật mới: không địch nổi quái mạnh như Boss lang thang
        p.Admin = "kdpick=day_ho";
        var now = At(3, 19, 1);
        game.ResolveKyDuyenAt(p, 0, now);                          // "Lặn xuống lấy bảo vật" → Boss Thủy Để Cổ Thụ Yêu
        Assert.Equal(0, KyDuyen.Pages(p).Sum());
        Assert.Equal(0, p.Count("hoan_cot_dan"));
        Assert.True(p.DebuffUntil > DateTime.UtcNow);
        Assert.Contains(p.Logs, l => l.Text.Contains("Thủy Để Cổ Thụ Yêu") && l.Text.Contains("bại trận"));
        Assert.Null(game.KyDuyenStateAt(p, now).Open);

        // thắng hay thua do trận đánh quyết định (tối đa 150 lượt); phần thưởng phải đi đúng theo kết quả
        var other = await game.CreateAsync("Kỳ duyên 2");
        game.ChooseStarter(other, "hoa", PetData.StarterEgg);
        other.Admin = "god,kdpick=day_ho";                         // bất tử: quái không gây sát thương
        game.ResolveKyDuyenAt(other, 0, now);
        var won = other.Logs.Any(l => l.Text.Contains("Đánh bại Boss Thủy Để Cổ Thụ Yêu"));
        Assert.Equal(won ? 3 : 0, KyDuyen.Pages(other).Sum());
        Assert.Equal(won ? 1 : 0, other.Count("hoan_cot_dan"));
    }

    [Fact]
    public async Task LeavingClosesTheEncounterWithNothingGainedOrLost()
    {
        var (game, p) = await Setup();
        var now = At(3, 19, 3);
        var open = game.KyDuyenStateAt(p, now).Open!;
        Assert.Equal(3, open.Choices.Count);
        Assert.Equal("Bỏ đi", open.Choices[GameService.KyDuyenSkip].Label);
        var stones = p.Count("linh_thach");

        game.ResolveKyDuyenAt(p, GameService.KyDuyenSkip, now);
        Assert.Equal(KyDuyen.At(now)!.Key, p.KyDuyenKey);
        Assert.Null(game.KyDuyenStateAt(p, now).Open);
        Assert.Equal(0, KyDuyen.Pages(p).Sum());
        Assert.Equal(0, p.DaoTam);
        Assert.Equal(stones, p.Count("linh_thach"));
    }

    [Fact]
    public async Task AdminActivationOpensTheChosenEncounterOutsideSlots()
    {
        var (game, p) = await Setup();
        var off = At(3, 10, 20);                                   // ngoài khung giờ
        Assert.Null(game.KyDuyenStateAt(p, off).Open);
        Assert.Equal(KyDuyen.Encounters.Length, game.KyDuyenStateAt(p, off).Encounters!.Count);

        game.AdminAction(p, "kyduyen", "loi_kiep", 0);
        var open = game.KyDuyenStateAt(p, off).Open;
        Assert.NotNull(open);
        Assert.Equal(KyDuyen.Encounters.First(x => x.Id == "loi_kiep").Title, open!.Title);

        game.ResolveKyDuyenAt(p, 1, off);                          // "Chờ lôi tan rồi nhặt": chắc chắn
        Assert.Null(game.KyDuyenStateAt(p, off).Open);
        game.AdminAction(p, "kyduyen", "", 0);                     // kích hoạt lại theo khung giờ: bỏ kỳ duyên đã chọn
        Assert.Equal(KyDuyen.Pick(p.Id, KyDuyen.Make(off.Date, 10, 20)).Title, game.KyDuyenStateAt(p, off).Open!.Title);
        Assert.Equal(At(3, 10, 30).ToUnixTimeMilliseconds(), game.KyDuyenStateAt(p, off).Open!.EndsAt);   // khung admin: 10 phút đang chạy

        game.AdminAction(p, "flag", "kyduyen", 0);                 // tắt cờ: về lịch thật
        Assert.Null(game.KyDuyenStateAt(p, off).Open);
        Assert.DoesNotContain("kdpick=", p.Admin);
        Assert.Throws<GameException>(() => game.AdminAction(p, "kyduyen", "khong_co", 0));
    }

    [Fact]
    public async Task NoEncounterOutsideSlotsOrOffline()
    {
        var (game, p) = await Setup();
        Assert.Null(game.KyDuyenStateAt(p, At(3, 10)).Open);
        Assert.Throws<GameException>(() => game.ResolveKyDuyenAt(p, 0, At(3, 10)));
        Assert.Throws<GameException>(() => game.ResolveKyDuyenAt(p, 0, null));
        Assert.Throws<GameException>(() => game.ResolveKyDuyenAt(p, 3, At(3, 8)));
    }

    [Fact]
    public async Task CompletingABookRaisesStats()
    {
        var (game, p) = await Setup();
        var before = GameService.Stats(p);
        p.ThienThu = "9";
        p.Realm = 0;
        var after = GameService.Stats(p);
        Assert.Equal(KyDuyen.Books[0].Bonus.QiPct, KyDuyen.Total(p).QiPct);
        Assert.True(after.Hp >= before.Hp);
        var source = Assert.Single((await game.BuildStateAsync(p)).StatSources!, s => s.Name == "Thiên Thư");
        Assert.Equal(KyDuyen.Books[0].Bonus.QiPct, source.Qi);
        p.ThienThu = "9,9";
        Assert.True(GameService.Stats(p).Hp > before.Hp, "Quyển Nhị cộng % máu");
    }

    [Fact]
    public async Task BlockedPlayerSeesNoOpenEncounter()
    {
        var (game, p) = await Setup();
        p.Flagged = true;
        Player.SetRequestAdmin(false);   // AsyncLocal: chỉ ảnh hưởng luồng của test này
        try
        {
            var state = game.KyDuyenStateAt(p, At(3, 8, 5));
            Assert.NotNull(state.Blocked);
            Assert.Null(state.Open);
            Assert.Throws<GameException>(() => game.ResolveKyDuyenAt(p, 0, At(3, 8, 5)));
        }
        finally { Player.SetRequestAdmin(true); }
    }
}
