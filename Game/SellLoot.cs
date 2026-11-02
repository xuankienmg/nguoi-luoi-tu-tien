namespace VanDao.Game;

/// <summary>Chiến lợi phẩm chỉ bán và nhóm thưởng loại trừ lẫn nhau ở mọi ải thường.</summary>
public static class SellLoot
{
    public static readonly ItemDef[] Items =
    [
        new("sell_loot_low", "Quặng vụn", "salvage", "#a78365",
            "Quặng đồng lẫn đá vụn, chiến lợi phẩm giá thấp.", "img/items/sell_loot_low.png",
            Kind: "Chiến lợi phẩm", Use: "Chỉ dùng để bán lấy 100 linh thạch mỗi món.", Sell: 100),
        new("sell_loot_medium", "Túi châu ngọc", "salvage", "#FF8C00",
            "Túi gấm chứa châu ngọc, chiến lợi phẩm giá trung.", "img/items/sell_loot_medium.png",
            Rarity: "mythic", Kind: "Chiến lợi phẩm", Use: "Chỉ dùng để bán lấy 1.000 linh thạch mỗi món.", Sell: 1000),
        new("sell_loot_high", "Bảo vật vàng ngọc", "salvage", "#E0FFFF",
            "Tinh thể vàng ôm ngọc bích, chiến lợi phẩm quý hiếm.", "img/items/sell_loot_high.png",
            Rarity: "supreme", Kind: "Chiến lợi phẩm", Use: "Chỉ dùng để bán lấy 100.000 linh thạch mỗi món.", Sell: 100000)
    ];

    public static StageLootGroup StageGroup(ChapterDef chapter, int diff, int area)
    {
        var herbs = GameData.HerbDropsAt(chapter, diff, area).ToArray();
        var herbWeight = herbs.Sum(d => GameData.DropChance(d));
        if (herbWeight <= 0) throw new InvalidOperationException("Map chưa có nguồn linh thảo cho nhóm thưởng chung.");
        var choices = new List<StageLootChoice> { new("sell_loot_low", 1, 1, .40) };
        choices.AddRange(herbs.Select(d => new StageLootChoice(d.Item, d.Min, d.Max, .30 * GameData.DropChance(d) / herbWeight)));
        choices.Add(new("sell_loot_medium", 1, 1, .18));
        choices.AddRange(GemData.GetStageStoneGroup(chapter.Index, diff, area).Choices.Select(c => c with { Weight = .10 * c.Weight }));
        choices.Add(new("sell_loot_high", 1, 1, .02));
        return new(1, choices.ToArray());
    }
}
