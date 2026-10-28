using System.Collections.Concurrent;

namespace VanDao.Game;

public record ChestReward(string Item, int Qty, double Probability, string Group, string? BaseItem = null);

/// <summary>One immutable distribution shared by rolling and displayed odds, cached per realm/chest.</summary>
public static class ChestRewards
{
    private static readonly ConcurrentDictionary<(int Realm,string Chest,int Diff,int Chapter),IReadOnlyList<ChestReward>> Cache=new();
    /// <summary>Rương theo mức (ruong_thuong_kho…) luôn tính theo mức ghi trên rương, bỏ qua diff truyền vào.</summary>
    public static IReadOnlyList<ChestReward> Table(int realm,string chest,int diff=0,int chapter=-1)
    {
        var own=GameData.ChestDiff(chest);if(own>0){diff=own;chest=GameData.ChestBase(chest);}
        return Cache.GetOrAdd((realm,chest,diff,chapter),key=>Build(key.Realm,key.Chest,key.Diff,key.Chapter));
    }
    public static ChestReward Roll(int realm,string chest,Random rng,int diff=0,int chapter=-1)
    {
        var table=Table(realm,chest,diff,chapter);var roll=rng.NextDouble();
        foreach(var reward in table)if((roll-=reward.Probability)<0)return reward;
        return table[^1];
    }
    private static IReadOnlyList<ChestReward> Build(int realm,string chest,int diff,int chapter)
    {
        if(realm<0||realm>=GameData.Realms.Count)throw new GameException("Cảnh giới không hợp lệ.");
        var cd=GameData.Chests[chest];var r=GameData.Realms[realm];var list=new List<ChestReward>();
        var gear=GameData.GearBases.Where(b=>b.Slot!=null&&b.MinRealm==realm).ToArray();
        if(gear.Length==0)throw new InvalidOperationException("Chưa cấu hình trang bị rương đúng cảnh giới.");
        // Linh thảo phải farm ở đúng nguồn bản đồ. Chuyển phần thưởng linh thảo cũ sang trang bị,
        // không chọn nguyên liệu theo cảnh giới người mở rương nhặt từ map thấp.
        var equipmentChance=cd.EquipmentChance+cd.Herb;
        var enlightenment=realm>=GameData.ItemById["ngo_dao_dan"].MinRealm?cd.Enlightenment:0;
        var pillChance=1-equipmentChance-enlightenment;
        var quality=GameData.ChestQuality(chest,realm,diff);var qs=quality.Sum();   // rương Khó / Ác mộng: phẩm nhỉnh hơn
        if(qs<=0||quality.Any(q=>q<0)||pillChance<0)throw new InvalidOperationException("Bảng thưởng rương không hợp lệ.");
        void Add(string item,int qty,double probability,string group,string? baseItem=null){if(probability>0)list.Add(new(item,qty,probability,group,baseItem));}
        for(var q=0;q<quality.Length;q++)if(quality[q]>0){
            foreach(var g in gear)Add(GameData.GearId(g,q),1,equipmentChance/gear.Length*quality[q]/qs,"gear",g.Id);
        }
        var pill=r.StageCost(0).Keys.First(k=>k!="linh_thach");
        for(var n=cd.PillMin;n<=cd.PillMax;n++)Add(pill,n,pillChance/(cd.PillMax-cd.PillMin+1),"pill");
        Add("ngo_dao_dan",1,enlightenment,"enlightenment");
        var chapterIndex=Math.Clamp(chapter<0?realm:chapter,0,PetData.PetEggDrops.Length-1);
        var eggChance=PetData.PetEggDrops[chapterIndex];
        var petChance=chest==GameData.BossChest?eggChance+.045:.05;
        // Keep the approved, realm-gated enlightenment rate exact. Pet rewards
        // take their probability only from the other existing reward branches.
        var otherChance=1-enlightenment;
        if(petChance>otherChance)throw new InvalidOperationException("Tỉ lệ thưởng linh thú vượt phần thưởng khả dụng.");
        var otherScale=(otherChance-petChance)/otherChance;
        for(var i=0;i<list.Count;i++)if(list[i].Group!="enlightenment")
            list[i]=list[i] with {Probability=list[i].Probability*otherScale};
        if(chest==GameData.BossChest){
            var q=PetData.EggQuality(chapterIndex);
            var shift=GameData.DiffQualityShift[Math.Clamp(diff,0,GameData.DiffQualityShift.Length-1)];
            Add(PetData.EggIds[q],1,eggChance*(1-shift),"pet");
            Add(PetData.EggIds[Math.Min(4,q+1)],1,eggChance*shift,"pet");
            var blood=PetLoot.BloodUnlocked(chapterIndex,diff);
            Add("hoa_hinh_dan",1+chapterIndex/3+diff,blood?.03:.045,"pet");
            Add("than_thu_huyet",1+Math.Max(0,chapterIndex-4)/3+diff,blood?.015:0,"pet");
        }else Add("linh_thu_dan",Math.Min(PetLoot.MaxFoodDrop,(1+chapterIndex/2)*(chest=="ruong_hiem"?3:1)+diff),petChance,"pet");
        // Không rương nào ra đan tiến giai: tỉ lệ đan cũ chia đều cho các vật phẩm còn lại.
        {
            var removed=list.Where(x=>x.Group=="pill").Sum(x=>x.Probability);
            list.RemoveAll(x=>x.Group=="pill");
            var itemOdds=list.GroupBy(x=>x.Item).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Probability));
            var share=removed/itemOdds.Count;
            for(var i=0;i<list.Count;i++)list[i]=list[i] with {Probability=list[i].Probability+share*list[i].Probability/itemOdds[list[i].Item]};
        }
        if(chest==GameData.BossChest){
            // Preserve enlightenment and pet odds; take the manual budget from gear/pill/herb.
            var budget=list.Where(x=>x.Group is "gear" or "pill" or "herb").Sum(x=>x.Probability);
            if(budget<CultivationManuals.BossChance)throw new InvalidOperationException("Không đủ ngân sách xác suất công pháp.");
            for(var i=0;i<list.Count;i++)if(list[i].Group is "gear" or "pill" or "herb")
                list[i]=list[i] with {Probability=list[i].Probability*(budget-CultivationManuals.BossChance)/budget};
            var manuals=CultivationManuals.Available;
            foreach(var manual in manuals)Add(manual.Item,1,CultivationManuals.BossChance/manuals.Length,"manual");
        }
        // Gems share the existing single-reward draw; preserve pet, manual and enlightenment odds.
        var gemChance = GemData.ChestGemChance(chest);
        var gemBudget = list.Where(x => x.Group is "gear" or "pill" or "herb").Sum(x => x.Probability);
        if (gemBudget < gemChance) throw new InvalidOperationException("Không đủ ngân sách xác suất Tinh Thạch.");
        for (var i = 0; i < list.Count; i++) if (list[i].Group is "gear" or "pill" or "herb")
            list[i] = list[i] with { Probability = list[i].Probability * (gemBudget - gemChance) / gemBudget };
        foreach (var gem in GemData.ChestRewardPool(chest, diff)) Add(gem.Item, 1, gemChance * gem.Probability, "gem");
        if(Math.Abs(list.Sum(x=>x.Probability)-1)>1e-8||list.Any(x=>!GameData.ItemById.ContainsKey(x.Item)))throw new InvalidOperationException("Tổng xác suất rương không hợp lệ.");
        return list.AsReadOnly();
    }
}
