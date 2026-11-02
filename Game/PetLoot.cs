namespace VanDao.Game;

public record StageLootChoice(string Item, int Min, int Max, double Weight = 1);
public record StageLootGroup(double Chance, StageLootChoice[] Choices);

/// <summary>Shared stage distributions for actual loot and the map's displayed odds.</summary>
public static class PetLoot
{
    public const double FoodShare = .30;
    public const int MaxFoodDrop = 4;
    /// <summary>Trần tỉ lệ mỗi trận thắng ở ải thường (StageGroups): Bảo vật vàng ngọc 0,1% · Khiêu Chiến Lệnh 10%.</summary>
    public const double MaxHighSellLoot = .001, MaxKeyDrop = .10;
    /// <param name="chapter">Chương (0-based): Khó / Ác mộng thưởng tăng theo chương (GameData.DiffRewardAt).</param>
    public static int DropQuantity(string item, int quantity, int diff, int chapter = 0)
    {
        var amount = (int)Math.Round(quantity * QuantityMultiplier(item, diff, chapter));
        if (GemData.IsGem(item)) return Math.Min(1, amount);
        return item == "linh_thu_dan" ? Math.Min(MaxFoodDrop, amount) : amount;
    }
    public static double QuantityMultiplier(string item,int diff,int chapter=0) =>
        GameData.GearBaseById.ContainsKey(item)||item is "linh_thach" or "linh_thu_dan"||
        (GameData.ItemById.TryGetValue(item,out var definition)&&definition.Type is "key" or "chest" or "salvage")?1:GameData.DiffRewardAt(diff,chapter);
    public static bool BloodUnlocked(int chapter, int diff) => chapter >= 4 || diff > 0;
    public static (int Min,int Max) FoodAmount(int chapter,int area,int diff=0) => (1, MaxFoodDrop);
    private static bool StartsAtOne(string item) => item is "linh_thu_dan" or "hoa_hinh_dan" ||
        GameData.ItemById.TryGetValue(item, out var definition) && definition.Type == "herb";
    public static (int Min, int Max) QuantityRange(string item, int min, int max, int diff, int chapter = 0) =>
        (StartsAtOne(item) ? 1 : DropQuantity(item, min, diff, chapter), DropQuantity(item, max, diff, chapter));
    /// <summary>Linh thảo và đan linh thú gieo trong khoảng số lượng cuối cùng để luôn có thể rơi từ 1.</summary>
    public static int RollQuantity(string item, int min, int max, int diff, int chapter, Random rng)
    {
        if (!StartsAtOne(item)) return DropQuantity(item, rng.Next(min, max + 1), diff, chapter);
        var range = QuantityRange(item, min, max, diff, chapter);
        return rng.Next(range.Min, range.Max + 1);
    }
    public static StageLootGroup[] StageGroups(ChapterDef chapter,int diff,int area)
    {
        var food=FoodAmount(chapter.Index,area,diff);
        var removed=0d;
        var rows=GameData.DiffDrops(chapter,diff)
            .Where(d => !GemData.IsGem(d.Item) && !(GameData.ItemById.TryGetValue(d.Item, out var item) && item.Type == "herb")).Select(d=>{
            var chance=GameData.DropChance(d);
            chance=Math.Min(1,chance*(chance>=1?1:1+(area-1)*.03));
            var pill=GameData.ItemById.TryGetValue(d.Item,out var item)&&item.Type=="pill";
            if(pill){removed+=chance*(1-FoodShare);return new DropDef("linh_thu_dan",food.Min,food.Max,chance*FoodShare);}
            return d with {Chance=chance};
        }).ToList();
        rows.Add(new(GameData.MapKey(diff),1,1,GameData.KeyChance(area)));
        // rương mang mức của map (Khó / Ác mộng là loại riêng), mở ra tính thưởng theo mức đó
        var rareChest=GameData.ChestId("ruong_hiem",diff);
        rows.Add(new(GameData.ChestId("ruong_thuong",diff),1,1,GameData.ChestChance));
        var rare=area>=GameData.EliteMinArea?GameData.EliteRareChestChance:area>=GameData.RareChestMinArea?GameData.RareChestChance:0;   // ½ rương thường, mọi mức khó
        if(rare>0)rows.Add(new(rareChest,1,1,rare));
        var merged=rows.GroupBy(x=>x.Item).Select(g=>new DropDef(g.Key,g.Key=="linh_thu_dan"?1:g.Min(x=>x.Min),g.Key=="linh_thu_dan"?MaxFoodDrop:g.Sum(x=>x.Max),1-g.Aggregate(1d,(n,x)=>n*(1-x.Chance)))).ToArray();
        // Equal increments per item; rare chest and gear odds are protected (gear keeps its base chance) and no chance exceeds 100%.
        while(removed>1e-12){
            var eligible=Enumerable.Range(0,merged.Length).Where(i=>merged[i].Item!=rareChest&&!GameData.GearBaseById.ContainsKey(merged[i].Item)&&merged[i].Chance<1-1e-12).ToArray();
            if(eligible.Length==0)throw new InvalidOperationException("Không đủ vật phẩm để chia tỉ lệ đan tiến giai.");
            var share=removed/eligible.Length;var spent=0d;
            foreach(var i in eligible){var add=Math.Min(share,1-merged[i].Chance);merged[i]=merged[i] with {Chance=merged[i].Chance+add};spent+=add;}
            removed-=spent;
        }
        // Mỗi trận thắng ra đúng 1 món ngoài linh thạch: gộp các món trên và nhóm chiến lợi phẩm / linh thảo / Tinh Thạch
        // thành một lượt loại trừ tổng 100%, chia theo tổng nên giữ nguyên tỉ lệ tương đối giữa các món.
        var groups = merged.Where(d => d.Item == "linh_thach").Select(d => new StageLootGroup(d.Chance, [new(d.Item, d.Min, d.Max)])).ToList();
        var loot = SellLoot.StageGroup(chapter, diff, area);
        var others = merged.Where(d => d.Item != "linh_thach").ToArray();
        var total = others.Sum(d => d.Chance) + loot.Chance;
        var choices = others.Select(d => new StageLootChoice(d.Item, d.Min, d.Max, d.Chance / total))
            .Concat(loot.Choices.Select(c => c with { Weight = c.Weight * loot.Chance / total })).ToArray();
        // Bảo vật vàng ngọc tối đa 0,1% và Khiêu Chiến Lệnh tối đa 10% mỗi trận; phần bớt ra cộng cho Tinh Thạch (giữ tỉ lệ giữa các viên).
        var freed = 0d;
        for (var i = 0; i < choices.Length; i++)
        {
            var cap = choices[i].Item == "sell_loot_high" ? MaxHighSellLoot : choices[i].Item == GameData.MapKey(diff) ? MaxKeyDrop : 1;
            if (choices[i].Weight > cap) { freed += choices[i].Weight - cap; choices[i] = choices[i] with { Weight = cap }; }
        }
        var gemWeight = choices.Where(c => GemData.IsGem(c.Item)).Sum(c => c.Weight);
        if (gemWeight > 0) choices = choices.Select(c => GemData.IsGem(c.Item) ? c with { Weight = c.Weight + freed * c.Weight / gemWeight } : c).ToArray();
        groups.Add(new(1, choices));
        return groups.ToArray();
    }
    public static StageLootChoice Choose(StageLootGroup group,Random rng)
    {
        var roll=rng.NextDouble();
        foreach(var choice in group.Choices)if((roll-=choice.Weight)<0)return choice;
        return group.Choices[^1];
    }
    public static List<DropInfoDto> StageOdds(ChapterDef chapter,int diff,int area)
    {
        return StageGroups(chapter,diff,area).SelectMany(g=>g.Choices.Select(c=>new {
            c.Item,c.Min,c.Max,Chance=g.Chance*c.Weight,Gear=GameData.GearBaseById.ContainsKey(c.Item)
        })).GroupBy(c=>c.Item).Select(rows=>{
            var first=rows.First();
            var range=QuantityRange(first.Item,rows.Min(x=>x.Min),rows.Sum(x=>x.Max),diff,chapter.Index);
            return new DropInfoDto(first.Item,range.Min,range.Max,1-rows.Aggregate(1d,(n,x)=>n*(1-x.Chance)),first.Gear);
        }).ToList();
    }
    /// <summary>Cách nhận một vật phẩm Boss (theo BossMaterials, mọi chương): "Rơi khi hạ Boss (mọi chương): Thường 15% · 1 viên; …".</summary>
    public static string BossSource(string item)
    {
        var parts=new List<string>();
        for(var d=0;d<GameData.DiffNames.Length;d++){
            var x=Enumerable.Range(0,GameData.OpenChapters).SelectMany(c=>BossMaterials(c,d)).FirstOrDefault(m=>m.Item==item);
            if(x is null)continue;
            parts.Add($"{GameData.DiffNames[d]} {Math.Round(x.Chance*100):0}% · {(x.Min==x.Max?$"{x.Min}":$"{x.Min}–{x.Max}")} viên");
        }
        return parts.Count==0?"":$"Rơi khi hạ Boss: {string.Join("; ",parts)}.";
    }
    /// <summary>Vật phẩm rơi khi hạ Boss. Không gồm đan đột phá (đan tiến giai theo cảnh giới).</summary>
    public static DropDef[] BossMaterials(int chapter,int diff)
        => BossMaterialsCore(chapter, diff).Concat(GemData.BossBonusDrops(chapter, diff)).ToArray();

    private static DropDef[] BossMaterialsCore(int chapter,int diff)
    {
        var quantity=1+Math.Max(0,chapter)/3+diff;
        var drops=new List<DropDef>{new("hoa_hinh_dan",1,quantity+1,.25+.05*diff)};
        if(GameData.BreakthroughBossItem(chapter,diff) is { } core)
            drops.Add(new(core,1,1,1));
        if(BloodUnlocked(chapter,diff)){
            var blood=1+Math.Max(0,chapter-4)/3+diff;
            drops.Add(new("than_thu_huyet",blood,blood+1,Math.Min(.30,.12+.02*Math.Max(0,chapter-4)+.04*diff)));
        }
        // bồi dưỡng linh thú: đan tư chất + trang bị linh thú (tỉ lệ từng món = một lần rơi chia đều 3 ô × trọng số phẩm)
        drops.Add(new(PetTraining.RerollItem,1,1+diff,Math.Min(.35,.15+.05*diff)));
        var cap=PetTraining.DropTierCap(chapter,diff);
        var total=Enumerable.Range(0,cap+1).Sum(t=>PetTraining.DropTierWeight(t,cap));
        foreach(var slot in PetTraining.Slots)for(var t=0;t<=cap;t++)
            drops.Add(new(PetTraining.GearId(slot.Id,t),1,1,BossGearChance(diff)/PetTraining.Slots.Length*PetTraining.DropTierWeight(t,cap)/total));
        return drops.ToArray();
    }
    /// <summary>Tỉ lệ một lần hạ Boss rơi trang bị linh thú (một món, ô và phẩm ngẫu nhiên).</summary>
    public static double BossGearChance(int diff) => .12+.03*diff;
    /// <summary>Trang bị linh thú rơi khi hạ Boss: một lần gieo; trúng thì ô ngẫu nhiên, phẩm ngẫu nhiên Phàm → trần (tối đa Thiên).</summary>
    public static string? RollBossGear(int chapter,int diff,Random rng)
    {
        if(rng.NextDouble()>=BossGearChance(diff))return null;
        var slot=PetTraining.Slots[rng.Next(PetTraining.Slots.Length)];
        var cap=PetTraining.DropTierCap(chapter,diff);
        var roll=rng.Next(Enumerable.Range(0,cap+1).Sum(t=>PetTraining.DropTierWeight(t,cap)));
        var tier=0;
        while((roll-=PetTraining.DropTierWeight(tier,cap))>=0)tier++;
        return PetTraining.GearId(slot.Id,tier);
    }
    /// <summary>Gieo vật phẩm Boss thật: từng món trong BossMaterials độc lập, riêng trang bị linh thú chỉ một lần (RollBossGear).</summary>
    public static List<(string Item,int Quantity)> RollBossMaterials(int chapter,int diff,Random rng)
    {
        var got=new List<(string,int)>();
        foreach(var material in BossMaterialsCore(chapter,diff)){
            if(PetTraining.Gear.ContainsKey(material.Item)||rng.NextDouble()>=material.Chance)continue;
            got.Add((material.Item,rng.Next(material.Min,material.Max+1)));
        }
        if(RollBossGear(chapter,diff,rng) is { } gear)got.Add((gear,1));
        if(GemData.RollBossBonus(chapter,diff,rng) is { } gem)got.Add((gem,1));
        return got;
    }
}
