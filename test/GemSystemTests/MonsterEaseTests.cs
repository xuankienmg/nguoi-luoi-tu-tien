using VanDao.Game;
using Xunit;

namespace GemSystemTests;

/// <summary>cân bằng 11/2026: quái Thường → Địa Ngục 2 nhẹ đi 30% (máu / công / thủ), chỉ số phụ giữ nguyên; Ác Mộng tăng đều ×1,10 mỗi chương.</summary>
public class MonsterEaseTests
{
    [Fact]
    public void OlderDifficultiesAreEasedByMonsterEase()
    {
        for (var d = 0; d <= 4; d++)
        {
            var (c, a) = (GameData.ChapterFor(d, 5), 9);
            var (g, e) = (GameData.AreaWavesRaw(c, a, d)[0], GameData.AreaWaves(c, a, d)[0]);
            Assert.Equal(CombatNumber.Scale(g.Hp, GameData.MonsterEase), e.Hp);
            Assert.Equal(CombatNumber.Scale(g.Atk, GameData.MonsterEase), e.Atk);
            Assert.Equal(CombatNumber.Scale(g.Def, GameData.MonsterEase), e.Def);
            Assert.Equal(g.Crit, e.Crit);
        }
    }

    [Fact]
    public void NightmareStatScaleGrowsByTenPercentEveryChapter()
    {
        for (var c = 1; c < GameData.OpenChapters; c++)
            Assert.Equal(1.10, GameData.DiffStatScale(2, c) / GameData.DiffStatScale(2, c - 1), 9);
    }
}
