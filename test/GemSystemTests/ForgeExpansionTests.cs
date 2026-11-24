using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;
using static VanDao.Game.ForgeData;
namespace GemSystemTests;

public class ForgeExpansionTests
{
    private static ForgeRelicData.Mods M(params string[] ids) => ForgeRelicData.ModsOf(ids);
    private sealed class R(double v) : Random { public override double NextDouble() => v; }
    [Fact] public void CatalogAndCapMatchApprovedDesign()
    {
        Assert.Equal(25, ForgeRelicData.All.Length); Assert.Equal(150, MaxLevel);
        foreach(var id in ForgeRelicData.All.Select(r => r.Id))
            { Assert.True(ForgeRelicData.ById.ContainsKey(id)); Assert.NotNull(ForgeRelicData.ById[id].Icon); Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory,"../../../../../wwwroot",ForgeRelicData.ById[id].Icon!)), id); }
    }
    [Fact] public void FireRelicsCombineWithoutChargingDuringFrenzy()
    {
        var m=M("hoa_long_dinh","ngung_hoa_chau","truong_minh_dang","phuong_vu");
        var n=Apply(new SlotState{L=10,C=6},Outcome.Success,m);
        Assert.Equal(0,n.C); Assert.Equal(6,n.F);
        var fire=Apply(n,Outcome.Success,m); Assert.Equal(0,fire.C); Assert.Equal(5,fire.F);
        Assert.Equal(1,Apply(new SlotState{L=10},Outcome.Hold,m).C);
    }
    [Fact] public void ProtectionAndGreatNeverExceed150()
    {
        Assert.Equal(112,Apply(new SlotState{L=150},Outcome.Break,M("bat_diet_hon","cuu_chuyen_lien")).L);
        Assert.Equal(13,Apply(new SlotState{L=10},Outcome.Great,M("thien_cong_an"),new R(0)).L);
        Assert.Equal(150,Apply(new SlotState{L=149},Outcome.Great,M("thien_cong_an"),new R(0)).L);
        var plain=OddsAt(100,false); var safe=OddsAt(100,false,M("ho_cot_phu"));
        Assert.Equal(plain.Damage*.7,safe.Damage,8); Assert.Equal(plain.Hold+plain.Damage*.3,safe.Hold,8);
    }
    [Fact] public void CostsAndArmorBonusRespectStacking()
    {
        var m=M("hoan_thiet_an","tu_tai_bon","han_thiet_tam");
        Assert.Equal(8550,StoneCost(GameData.ItemById["linh_thach"],100,m));
        Assert.Equal(10,IronCost(100,m)); Assert.Equal(1,IronCost(0,m));
        var p=new Player(); Store(p,new ForgeSave{Relics=["linh_van_khac","ngu_hanh_dai"]});
        Assert.Equal(1.15,BonusMult(Levels(p),"body"),8); Assert.Equal(1.1,BonusMult(Levels(p),"weapon"),8);
    }
    [Fact] public void SerializedLegacyStateIsClampedWithoutWriting()
    {
        var p=new Player{ForgeState="{\"Slots\":{\"weapon\":{\"L\":200}},\"Undo\":{\"Slot\":\"weapon\",\"Before\":{\"L\":199}}}"};
        var save=Load(p);Assert.Equal(150,save.Slots["weapon"].L);Assert.Equal(150,save.Undo!.Before.L);
        Assert.Equal(BonusPct(150),BonusPct(200));
    }
    [Fact] public void DamageProtectionUsesRemainingDamageAfterAnvil()
    {
        var old=OddsAt(100,false,M("ban_thach_de"));
        var combined=OddsAt(100,false,M("ban_thach_de","ho_cot_phu"));
        Assert.Equal(.042,combined.Damage,8);Assert.Equal(old.Hold+.018,combined.Hold,8);
    }
    [Fact] public void ArmorBonusMatchesCharacterAndLeaderboardAndExpires()
    {
        var p=new Player{Realm=5};var item=GameData.AllItems.Where(i=>i.Slot=="body"&&i.MinRealm<=5).MaxBy(i=>i.Hp)!;
        p.Equipment.Add(new(){PlayerId=p.Id,Slot="body",ItemId=item.Id});
        var save=new ForgeSave{Relics=["linh_van_khac"]};save.Slots["body"]=new(){L=100};Store(p,save);
        var plain=GameService.Stats(p,false).Hp;
        save.Relics.Add("ngu_hanh_dai");Store(p,save);
        Assert.True(GameService.Stats(p,false).Hp>plain);
        Assert.Equal(345,LeaderboardSnapshot.Of(p,true).Stats["forge_bonus:body"],8);
        save.ShopDay="2000-01-01";Store(p,save);
        Assert.Equal(1,BonusMult(Levels(p),"body"),8);
        Assert.Equal(300,LeaderboardSnapshot.Of(p,true).Stats["forge_bonus:body"],8);
    }
    [Fact] public void OldSavesAndUndoAreClampedTo150()
    {
        var p=new Player{ForgeLevels="weapon:200"};
        var save=Load(p); Assert.Equal(150,save.Slots["weapon"].L);
        save.Undo=new("weapon",new SlotState{L=200}); Store(p,save);
        Assert.Equal(150,Load(p).Undo!.Before.L);
    }
    [Fact] public void AllFourRelicCombinationsHaveValidOdds()
    {
        var ids=ForgeRelicData.All.Select(d=>d.Id).ToArray();
        for(int a=0;a<ids.Length;a++) for(int b=a+1;b<ids.Length;b++)
        for(int c=b+1;c<ids.Length;c++) for(int d=c+1;d<ids.Length;d++)
        foreach(var l in new[]{0,30,100,149,150}) foreach(var fire in new[]{false,true})
        {
            var o=OddsAt(l,fire,M(ids[a],ids[b],ids[c],ids[d]));
            Assert.Equal(1,o.ToArray().Sum(),8); Assert.All(o.ToArray(),v=>Assert.InRange(v,0,1));
        }
    }
    [Fact] public async Task HoldRefundCannotBeRepeatedByUndo()
    {
        using var db=new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection(); db.Database.EnsureCreated(); var game=new GameService(db);
        var p=await game.CreateAsync("Relic"); var gear=GameData.AllItems.First(i=>i.Slot=="weapon"&&i.MinRealm==0);
        p.Equipment.RemoveAll(e=>e.Slot=="weapon"); p.Equipment.Add(new(){PlayerId=p.Id,Slot="weapon",ItemId=gear.Id});
        p.Add("linh_thach",100000);p.Add(Iron,100);p.Add(Rewind,2);
        var save=Load(p);save.Relics=["bao_nguyen_kinh","tu_tai_bon"];save.Slots["weapon"]=new(){L=10};Store(p,save);
        var before=p.Count("linh_thach"); var o=OddsAt(10,false,M(save.Relics.ToArray()));
        game.ForgeStrike(p,"weapon",new R(o.Great+o.Success+.01));
        Assert.Equal(before-4500,p.Count("linh_thach"));
        game.ForgeUndo(p,new R(.99)); Assert.Equal(before-4500,p.Count("linh_thach"));
        Assert.Throws<GameException>(()=>game.ForgeUndo(p));
    }
}
