using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class MapKeyTests
{
    [Fact]
    public void EveryModeUsesItsOwnKeyAcrossAllChaptersAndLoot()
    {
        var keys = Enumerable.Range(0, 4).Select(d => GameData.ChapterFor(d, 0).KeyItem).ToArray();
        Assert.Equal(4, keys.Distinct().Count());
        Assert.Equal(4, GameData.AllItems.Count(i => i.Type == "key"));
        Assert.Equal(4, keys.Select(k => GameData.ItemById[k].Rarity).Distinct().Count());
        foreach (var diff in Enumerable.Range(0, 4))
        foreach (var chapter in Enumerable.Range(0, 10))
        {
            var map = GameData.ChapterFor(diff, chapter);
            Assert.Equal(keys[diff], map.KeyItem);
            var drops = PetLoot.StageOdds(map, diff, 1);
            Assert.Equal(keys[diff], Assert.Single(drops, d => GameData.ItemById.TryGetValue(d.Item, out var item) && item.Type == "key").Item);
        }
        Assert.All(GameData.Chapters, c => Assert.Equal(keys[0], c.KeyItem));
    }

    [Fact]
    public void LoadingOldInventoryPreservesEveryKeyCount()
    {
        var player = new Player { MapVer = 1 };
        var keys = new[] { "con_ngo_lenh", "dong_hai_lenh", "hu_thien_tan_do", "hoa_nguc_lenh" };
        foreach (var key in keys) player.Add(key, 25);
        player.Add("huyet_sac_lenh", 7);
        GameService.ConvertLegacy(player);
        GameService.ConvertLegacy(player);
        Assert.Equal(32, player.Count(keys[0]));
        foreach (var key in keys.Skip(1)) Assert.Equal(25, player.Count(key));
    }
}
