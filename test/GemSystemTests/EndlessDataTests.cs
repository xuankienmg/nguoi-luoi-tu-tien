using System.Reflection;
using VanDao.Data;
using VanDao.Game;
using Xunit;

namespace GemSystemTests;

/// <summary>Mức Vô Tận (Diff 5): công thức chỉ số theo tầng, cấu trúc tầng, điều kiện mở mức.</summary>
public class EndlessDataTests
{
    private static T Call<T>(string name, params object[] args) =>
        (T)typeof(GameService).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args)!;

    [Fact]
    public void DiffNamesEndWithEndless()
    {
        Assert.Equal("Vô Tận", GameData.DiffNames[EndlessData.Diff]);
        Assert.Equal(EndlessData.Diff + 1, GameData.DiffNames.Length);
        Assert.True(GameData.IsEndless(EndlessData.Diff));
        Assert.False(GameData.IsEndless(4));
        foreach (var len in new[] { GameData.DiffMult.Length, GameData.DiffReward.Length, GameData.DiffStoneReward.Length, GameData.DiffQualityShift.Length,
                     GameData.DiffBossChests.Length, GameData.DiffChestQualityShift.Length, GameData.DiffChestSuffix.Length })
            Assert.Equal(GameData.DiffNames.Length, len);
        foreach (var arr in GameData.DiffChestHighGrade.Values) Assert.Equal(GameData.DiffNames.Length, arr.Length);
    }

    [Fact]
    public void StatsNeverDecreaseWithFloorAndStayBelowCombatCap()
    {
        MonsterDef First(int f) => EndlessData.WavesAt(f)[0];
        for (var f = 1; f < EndlessData.MaxFloor; f++)
        {
            Assert.True(EndlessData.StatScale(f + 1) > EndlessData.StatScale(f));
            if (EndlessData.IsBoss(f) || EndlessData.IsBoss(f + 1)) continue;
            var (a, b) = (First(f), First(f + 1));
            Assert.True(b.Hp >= a.Hp && b.Atk >= a.Atk && b.Def >= a.Def, $"tầng {f} → {f + 1}");
        }
        // Boss: cùng mốc neo nên tầng Boss sau luôn mạnh hơn tầng Boss trước.
        for (var f = EndlessData.BossEvery; f + EndlessData.BossEvery <= EndlessData.MaxFloor; f += EndlessData.BossEvery)
        {
            var (a, b) = (First(f), First(f + EndlessData.BossEvery));
            Assert.True(b.Hp > a.Hp && b.Atk > a.Atk && b.Def > a.Def, $"Boss tầng {f}");
        }
        // Không chạm trần CombatNumber.Max (chừa dư ≥ 10^6 cho nhân tỉ lệ khi tính sát thương) ở mọi tầng, kể cả khi đang cuồng bạo.
        for (var f = 1; f <= EndlessData.MaxFloor; f++)
            foreach (var w in EndlessData.WavesAt(f))
            {
                var top = Math.Max(w.Hp, Math.Max(w.Atk, w.Def));
                Assert.True(top < CombatNumber.Max / 1_000_000m, $"tầng {f}: {top}");
                Assert.True(w.Hp > 0 && w.Atk > 0);
            }
    }

    [Fact]
    public void FloorOneIsAtLeastHellTwoChapterTenStage()
    {
        var anchor = HellMapData.AreaWaves(HellMapData.Chapter(4, 9), GameData.Areas, 4)[0];
        Assert.True(EndlessData.WavesAt(1)[0].Atk >= anchor.Atk);
        Assert.True(EndlessData.WavesAt(1)[0].Def >= anchor.Def);
    }

    [Fact]
    public void FloorStructure()
    {
        for (var f = 1; f <= EndlessData.MaxFloor; f++)
        {
            var w = EndlessData.WavesAt(f);
            if (f % 10 == 0) { Assert.Single(w); Assert.Equal("boss", w[0].Tier); Assert.True(GameData.IsBossArea(EndlessData.Diff, f)); }
            else
            {
                Assert.Equal(3, w.Length);
                Assert.Equal(f % 5 == 0, w[^1].Tier == "elite");
                Assert.DoesNotContain(w.Take(2), x => x.Tier != "normal");
                Assert.False(GameData.IsBossArea(EndlessData.Diff, f));
            }
            Assert.All(w, x => Assert.StartsWith("img/", x.Image));
        }
        // chủ đề đổi mỗi 25 tầng, Boss xoay vòng
        Assert.NotEqual(EndlessData.ThemeName(25), EndlessData.ThemeName(26));
        Assert.Equal(EndlessData.ThemeName(1), EndlessData.ThemeName(25));
        Assert.NotEqual(EndlessData.WavesAt(10)[0].Name, EndlessData.WavesAt(20)[0].Name);
    }

    [Fact]
    public void RewardGrowsSlowerThanStats()
    {
        Assert.True(EndlessData.RewardMult(100) > EndlessData.RewardMult(1));
        Assert.True(EndlessData.RewardMult(100) / EndlessData.RewardMult(1) < EndlessData.StatScale(100) / EndlessData.StatScale(1) / 4);
        var c = GameData.ChapterFor(EndlessData.Diff, 0);
        var s1 = GameData.StoneRange(c, 1, "normal", EndlessData.Diff).Max;
        var s100 = GameData.StoneRange(c, 100, "normal", EndlessData.Diff).Max;
        Assert.True(s100 > s1);
    }

    [Fact]
    public void UnlocksAfterHellTwoChapterTenBoss()
    {
        var p = new Player();
        Assert.False(Call<bool>("ChapterOpen", p, EndlessData.Diff, 0));
        p.Cleared = "4.8";
        Assert.False(Call<bool>("ChapterOpen", p, EndlessData.Diff, 0));
        p.Cleared = "4.8;4.9";
        Assert.True(Call<bool>("ChapterOpen", p, EndlessData.Diff, 0));
        Assert.False(Call<bool>("ChapterOpen", p, EndlessData.Diff, 1));   // chỉ một "chương" duy nhất
        // tiến độ: tầng cao nhất đã mở lưu ở DiffProg "5.0"; mặc định tầng 1
        p.Diff = EndlessData.Diff;
        Assert.Equal(1, Call<int>("MapAreaMax", p));
        p.DiffProg = "5.0=37";
        Assert.Equal(37, Call<int>("MapAreaMax", p));
    }

    [Fact]
    public void OlderDifficultiesKeepTheirShape()
    {
        for (var d = 0; d < EndlessData.Diff; d++)
        {
            Assert.Equal(GameData.Areas, GameData.AreasFor(d));
            Assert.Equal(GameData.Areas + 1, GameData.BossAreaFor(d));
            Assert.True(GameData.IsBossArea(d, GameData.BossAreaFor(d)));
            Assert.False(GameData.IsBossArea(d, GameData.Areas));
        }
        Assert.Equal(GameData.HellWaves, GameData.WavesFor(4));
        Assert.Equal(GameData.Waves, GameData.WavesFor(EndlessData.Diff));
    }
}
