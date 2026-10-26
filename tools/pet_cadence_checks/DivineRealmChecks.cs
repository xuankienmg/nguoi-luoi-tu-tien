using VanDao.Game;
using VanDao.Data;
/// <summary>Thần thông bộ: mốc theo phẩm (Thần → Huyền Tiên) như cũ, chỉ số mạnh theo cảnh giới của bộ đang mặc.</summary>
internal static class DivineRealmChecks
{
    private sealed class FixedRandom(double value):Random { public override double NextDouble()=>value; }
    public static void Run()
    {
        int checks=0;
        void Check(bool ok,string msg){if(!ok)throw new Exception(msg);checks++;}
        // bộ Luyện Khí = số cũ; mỗi cảnh giới +5%
        Check(DivineAbility.NullifyChance(7,0)==.15&&DivineAbility.NullifyChance(9,0)==.20&&DivineAbility.ExtraBonus(0)==20&&DivineAbility.BoostPct(0)==100&&DivineAbility.SaveHpPct(0)==0,"Luyện Khí set keeps old numbers");
        var top=GameData.Realms.Count-1;
        Check(Math.Abs(DivineAbility.Scale(top)-(1+.05*top))<1e-9&&DivineAbility.NullifyChance(9,top)>.20&&DivineAbility.ExtraBonus(top)>20&&DivineAbility.BoostPct(top)>100,"higher realm set is stronger");
        for(var r=1;r<=top;r++)Check(DivineAbility.Scale(r)>DivineAbility.Scale(r-1)&&DivineAbility.SaveHpPct(r)>DivineAbility.SaveHpPct(r-1),"monotonic by set realm");
        // trận đánh dùng đúng số theo cảnh giới của bộ
        var low=new DivineCombat(9,0,1000);var high=new DivineCombat(9,5,1000);
        Check(!low.Nullify(new FixedRandom(.22))&&high.Nullify(new FixedRandom(.22)),"nullify chance scales (20% vs 25%)");
        high.Avoided();Check(high.Amplify(100)==225,"Thiên cơ +125% at Luyện Hư set");
        low.Avoided();Check(low.Amplify(100)==200,"Thiên cơ +100% at Luyện Khí set");
        Check(new DivineCombat(8,0).ExtraChance(10)==30&&new DivineCombat(8,4).ExtraChance(10)==34&&new DivineCombat(8,9).ExtraChance(40)==50,"extra hit scales, capped at 50");
        var save0=new DivineCombat(6,0,1000);save0.BeginTurn();Check(save0.Receive(100,500)==1,"Luyện Khí set keeps 1 HP");
        var save5=new DivineCombat(6,5,1000);save5.BeginTurn();Check(save5.Receive(100,500)==100&&save5.Saved,"Luyện Hư set keeps 10% max HP");
        // trạng thái gửi giao diện: cảnh giới bộ = cảnh giới thấp nhất của 10 món; mô tả ghi số thật
        var set=EquipmentEffects.Sets.Select(s=>new{s,items=GameData.AllItems.Where(i=>i.SetId==s.Id&&i.Slot!=null&&i.Rarity==GameData.RarityOrder[9]).GroupBy(i=>i.Slot).Select(g=>g.First()).ToArray()})
            .FirstOrDefault(x=>x.items.Length>=10&&x.items.Select(i=>i.MinRealm).Distinct().Count()==1&&x.items[0].MinRealm>0);
        if(set is not null){
            var realm=set.items[0].MinRealm;
            var p=new Player{Realm=Math.Max(realm,GameData.Realms.Count-1)};
            foreach(var it in set.items.Take(10))p.Equipment.Add(new EquippedItem{Slot=it.Slot!,ItemId=it.Id});
            var st=DivineAbility.For(p);
            Check(st.Tier==9&&st.SetRealm==realm&&st.SetRealmName==GameData.Realms[realm].Name,"state reports set realm");
            Check(st.Description.Contains(PetData.Percent(Math.Round(DivineAbility.BoostPct(realm),1))+"%"),"description shows scaled numbers");
        }
        Console.WriteLine($"PASS {checks} divine ability set-realm checks{(set is null?" (no uniform Huyền Tiên set found for state check)":"")}.");
    }
}
