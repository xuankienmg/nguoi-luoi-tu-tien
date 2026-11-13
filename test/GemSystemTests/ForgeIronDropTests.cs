using VanDao.Game;

namespace GemSystemTests;

/// <summary>Tinh Thiết rơi ở ải (gieo riêng mỗi trận thắng) và trong rương (lấy phần tỉ lệ của trang bị).</summary>
public class ForgeIronDropTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 15)]
    [InlineData(2, 5)]
    public void Stage_has_separate_iron_roll(int diff, int area)
    {
        var groups = PetLoot.StageGroups(GameData.Chapters[0], diff, area);
        var iron = Assert.Single(groups, g => g.Choices.Any(c => c.Item == ForgeData.Iron));
        Assert.Equal(ForgeData.StageIronChance, iron.Chance, 9);
        Assert.Single(iron.Choices);
        var main = groups.Single(g => g.Choices.All(c => c.Item is not ("linh_thach" or ForgeData.Iron)));
        Assert.Equal(1.0, main.Choices.Sum(c => c.Weight), 9);   // nhóm "1 món mỗi trận" vẫn đủ 100%, không bị bớt
        var odds = Assert.Single(PetLoot.StageOdds(GameData.Chapters[0], diff, area), d => d.Item == ForgeData.Iron);
        Assert.Equal((1, 1), (odds.Min, odds.Max));   // luôn 1 viên, kể cả Khó / Ác mộng
        Assert.Equal(1, PetLoot.RollQuantity(ForgeData.Iron, 1, 1, diff, 9, new Random(1)));
    }

    [Theory]
    [InlineData("ruong_thuong", .05, 1, 2)]
    [InlineData("ruong_hiem", .08, 1, 3)]
    [InlineData(GameData.BossChest, .10, 1, 4)]
    public void Chests_drop_iron_from_gear_share(string chest, double chance, int min, int max)
    {
        foreach (var realm in new[] { 0, 5 })
        {
            var table = ChestRewards.Table(realm, chest);
            var iron = table.Where(x => x.Item == ForgeData.Iron).ToList();
            Assert.Equal(chance, iron.Sum(x => x.Probability), 9);
            Assert.Equal(min, iron.Min(x => x.Qty));
            Assert.Equal(max, iron.Max(x => x.Qty));
            Assert.Equal(1.0, table.Sum(x => x.Probability), 8);
        }
    }

    [Fact]
    public void Iron_takes_only_from_gear()
    {
        foreach (var chest in new[] { "ruong_thuong", "ruong_hiem", GameData.BossChest })
        {
            var table = ChestRewards.Table(3, chest);
            Assert.Equal(GemData.ChestGemChance(chest), table.Where(x => x.Group == "gem").Sum(x => x.Probability), 9);
            // Tinh Thiết + trang bị = phần trang bị khi chưa có Tinh Thiết: các nhóm khác không đổi
            var others = table.Where(x => x.Group is not ("gear" or "iron")).Sum(x => x.Probability);
            Assert.Equal(1 - others, table.Where(x => x.Group is "gear" or "iron").Sum(x => x.Probability), 9);
            Assert.True(table.Where(x => x.Group == "gear").Sum(x => x.Probability) > 0);
        }
    }
}
