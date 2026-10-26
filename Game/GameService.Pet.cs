using VanDao.Data;
namespace VanDao.Game;

public partial class GameService
{
    private static int PetLevelCap(Player p) => Player.AdminEnabled && p.Tainted ? 100 : PetData.LevelCap(p.Realm);
    private static Pet FindPet(Player p,Guid id) => p.Pets.FirstOrDefault(x=>x.Id==id) ?? throw new GameException("Không sở hữu linh thú này.");
    private static List<(int Slot,string Egg,DateTime Ready)> Hatches(Player p) => p.Hatch.Split(';',StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Split('|'))
        .Where(x=>x.Length==3 && int.TryParse(x[0],out _) && PetData.EggQuality(x[1])>=0 && long.TryParse(x[2],out var sec) && sec>=0 && sec<=253402300799)
        .Select(x=>(int.Parse(x[0]),x[1],DateTimeOffset.FromUnixTimeSeconds(long.Parse(x[2])).UtcDateTime)).ToList();
    private static void SaveHatches(Player p,List<(int Slot,string Egg,DateTime Ready)> h) => p.Hatch=string.Join(';',h.Select(x=>$"{x.Slot}|{x.Egg}|{new DateTimeOffset(x.Ready).ToUnixTimeSeconds()}"));
    private static int InstantCost(DateTime ready) => Math.Max(0,(int)Math.Ceiling((ready-DateTime.UtcNow).TotalSeconds/60))*100;
    public (string,string) Hatch(Player p,string egg,int slot=0)
    {
        var q=PetData.EggQuality(egg);var h=Hatches(p);
        if(q<0 || slot<0 || slot>=PetData.HatchSlots(p.Realm)) throw new GameException("Trứng hoặc ổ ấp không hợp lệ.");
        if(h.Any(x=>x.Slot==slot)) throw new GameException("Ổ ấp đang bận.");
        if(p.Pets.Count+h.Count>=PetData.Capacity(p.Realm)) throw new GameException("Chuồng thú đã đầy.");
        if(p.Count(egg)<1) throw new GameException("Không có trứng.");
        p.Add(egg,-1);h.Add((slot,egg,DateTime.UtcNow.AddSeconds(PetData.HatchSeconds[q])));SaveHatches(p,h);
        return ("Đã bắt đầu ấp trứng.","good");
    }
    public (string,string) HatchNow(Player p,int slot=0)
    {
        var h=Hatches(p);var i=h.FindIndex(x=>x.Slot==slot);if(i<0)throw new GameException("Ổ ấp trống.");
        var cost=InstantCost(h[i].Ready);if(p.Count("linh_thach")<cost)throw new GameException("Không đủ linh thạch.");
        p.Add("linh_thach",-cost);h[i]=(slot,h[i].Egg,DateTime.UtcNow);SaveHatches(p,h);return ("Trứng đã sẵn sàng nở.","good");
    }
    public (string,string) CollectHatch(Player p,int slot=0)
    {
        var h=Hatches(p);var i=h.FindIndex(x=>x.Slot==slot);if(i<0||h[i].Ready>DateTime.UtcNow)throw new GameException("Trứng chưa sẵn sàng.");
        if(p.Pets.Count>=PetData.Capacity(p.Realm))throw new GameException("Chuồng thú đã đầy.");
        var q=PetData.EggQuality(h[i].Egg);var d=h[i].Egg==PetData.StarterEgg?PetData.ById["hoa_linh_ho"]:PetData.RollHatch(q,Random.Shared);
        var aptitudeQuality=d.Rarity=="ancient"?PetData.SpeciesQuality(d):q;
        var lo=PetData.AptMin[aptitudeQuality];var hi=PetData.AptMax[aptitudeQuality]+1;
        var pet=new Pet{PlayerId=p.Id,DefId=d.Id,AptAtk=Random.Shared.Next(lo,hi),AptDef=Random.Shared.Next(lo,hi),AptHp=Random.Shared.Next(lo,hi),Quality=q==0?0:-1};
        p.Pets.Add(pet);db.Pets.Add(pet);
        h.RemoveAt(i);SaveHatches(p,h);return ($"Linh thú {d.Name} đã nở!","gold");
    }
    public (string,string) Feed(Player p,Guid id,int n=1)
    {
        var pet=FindPet(p,id);if(n is not (0 or 1 or 10 or 100))throw new GameException("Chọn 1, 10, 100 hoặc tối đa.");
        var cap=PetLevelCap(p);if(pet.Level>=cap)throw new GameException("Đã đạt giới hạn cấp cảnh giới.");
        var have=p.Count("linh_thu_dan");if(have<1)throw new GameException("Không có Linh Thú Đan.");
        var limit=n==0?have:Math.Min(n,have);var used=0;
        while(used<limit&&pet.Level<cap){used++;pet.Exp+=PetData.FoodExp;while(pet.Level<cap&&pet.Exp>=PetData.ExpNeed(pet.Level)){pet.Exp-=PetData.ExpNeed(pet.Level);pet.Level++;}}
        if(pet.Level>=cap)pet.Exp=0;
        pet.FoodSpent+=used;p.Add("linh_thu_dan",-used);return ($"Đã cho ăn {used} Linh Thú Đan.","good");
    }
    // Admin cờ "petevo": tiến hóa không cần cấp, cảnh giới hay nguyên liệu.
    private static bool EvolveFree(Player p) => p.Cheat("petevo");
    private static bool EvolveGate(Player p,Pet pet) => pet.Form<2 && (EvolveFree(p) || pet.Level >= (pet.Form==0?30:60) && (pet.Form==0||p.Realm>=3));
    private static bool CanPayEvolve(Player p,Dictionary<string,int> cost) => EvolveFree(p) || p.Has(cost);
    public static int PetAppearance(Pet pet) => pet.AppearanceForm < 0 ? Math.Clamp(pet.Form,0,2) : Math.Clamp(pet.AppearanceForm,0,Math.Clamp(pet.Form,0,2));
    private static string EvolveHint(Player p,Pet pet)
    {
        if(pet.Form>=2)return "Đã mở hình thái cuối. Có thể chọn lại ngoại hình đã mở bên trên.";
        if(EvolveFree(p))return "[Admin] Tiến hóa không cần điều kiện.";
        var missing=new List<string>();var level=pet.Form==0?30:60;
        if(pet.Level<level)missing.Add($"linh thú cấp {level} (hiện tại {pet.Level})");
        if(pet.Form==1&&p.Realm<3)missing.Add("nhân vật đạt Nguyên Anh");
        if(!p.Has(PetData.EvolveCost(pet.Form)))missing.Add("đủ nguyên liệu");
        return missing.Count>0?"Cần "+string.Join(" · ",missing)+".":"Đã đủ điều kiện tiến hóa.";
    }
    public (string,string) Evolve(Player p,Guid id)
    {
        var pet=FindPet(p,id);var cost=PetData.EvolveCost(pet.Form);
        if(!EvolveGate(p,pet)||!CanPayEvolve(p,cost))throw new GameException(EvolveHint(p,pet));
        if(!EvolveFree(p))p.Pay(cost);pet.Form++;pet.AppearanceForm=-1;return ($"{PetData.ById[pet.DefId].Name} đã tiến hóa thành {PetData.ById[pet.DefId].Forms[pet.Form].Name}!","gold");
    }
    public (string,string) SetPetAppearance(Player p,Guid id,int form)
    {
        var pet=FindPet(p,id);
        if(form<0||form>pet.Form||form>2)throw new GameException("Chưa mở ngoại hình này.");
        pet.AppearanceForm=form;return ("Đã đổi ngoại hình linh thú. Chỉ số và tuyệt kỹ giữ nguyên.","good");
    }
    public (string,string) SetPetSlot(Player p,Guid id,int slot)
    {
        var pet=FindPet(p,id);if(slot<0||slot>1+PetData.GuardSlots(p.Realm))throw new GameException("Ô linh thú chưa mở.");
        if(slot>0)foreach(var other in p.Pets.Where(x=>x.Slot==slot))other.Slot=0;
        pet.Slot=slot;return ("Đã đổi vị trí linh thú.","good");
    }
    public (string,string) Release(Player p,Guid id)
    {
        var pet=FindPet(p,id);var refund=PetData.ReleaseValue(pet,PetData.ById[pet.DefId]);var gear=StripPetGear(p,pet);db.Pets.Remove(pet);p.Pets.Remove(pet);p.Add("linh_thu_dan",refund);return ($"Đã phóng sinh, nhận {refund} Linh Thú Đan{(gear>0?$", tháo {gear} trang bị linh thú về túi":"")}.","good");
    }
    /// <summary>Phóng sinh hàng loạt (ids cách nhau dấu phẩy). Bỏ qua con đang xuất chiến / hộ trận.</summary>
    public (string,string) ReleaseMany(Player p,string? ids)
    {
        var want=(ids??"").Split(',',StringSplitOptions.RemoveEmptyEntries).Select(x=>Guid.TryParse(x,out var g)?g:Guid.Empty).Where(g=>g!=Guid.Empty).ToHashSet();
        var list=p.Pets.Where(x=>want.Contains(x.Id)&&x.Slot==0&&PetData.ById.ContainsKey(x.DefId)).ToList();
        if(list.Count==0)throw new GameException("Chưa chọn linh thú nào phóng sinh được (con đang xuất chiến không phóng sinh).");
        var refund=0;foreach(var pet in list){refund+=PetData.ReleaseValue(pet,PetData.ById[pet.DefId]);StripPetGear(p,pet);db.Pets.Remove(pet);p.Pets.Remove(pet);}
        p.Add("linh_thu_dan",refund);
        return Log(p,$"Phóng sinh {list.Count} linh thú, nhận {refund} Linh Thú Đan.","good");
    }
    // Các đường dẫn cũ vẫn kiểm tra công thức mới, không cho cộng điểm ghép lẻ.
    public (string,string) MergeAll(Player p,Guid id) =>
        throw new GameException("Hãy chọn đủ linh thú hiến theo công thức rồi xác nhận ghép.");
    public (string,string) MergePet(Player p,Guid id,Guid foodId) => MergePets(p,id,foodId.ToString());

    /// <summary>Hiến trọn bộ đã chọn, tăng đúng một sao. Kiểm tra toàn bộ trước khi trừ linh thú.</summary>
    public (string,string) MergePets(Player p,Guid id,string? foods)
    {
        var pet=FindPet(p,id);var from=PetData.StarsOf(pet);
        if(from>=PetData.MaxStars)throw new GameException($"Đã đạt tối đa {PetData.MaxStars} sao.");
        var recipe=PetData.StarRecipes[from];
        var parts=(foods??"").Split(',',StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length!=1+recipe.OtherCount)throw new GameException($"Lên {recipe.TargetStars}★ cần 1 linh thú cùng loài {recipe.SameSpeciesStars}★"+(recipe.OtherCount>0?$" và {recipe.OtherCount} linh thú bất kỳ {recipe.OtherStars}★.":"."));
        var ids=new List<Guid>();
        foreach(var part in parts){if(!Guid.TryParse(part,out var donorId)||ids.Contains(donorId))throw new GameException("Linh thú hiến không hợp lệ hoặc bị chọn trùng.");ids.Add(donorId);}
        var donors=ids.Select(donorId=>FindPet(p,donorId)).ToArray();
        if(!PetData.CanStarDonor(pet,donors[0],true))throw new GameException($"Con hiến chính phải cùng loài, {recipe.SameSpeciesStars}★ và không đang xuất chiến.");
        if(donors.Skip(1).Any(d=>!PetData.CanStarDonor(pet,d,false)))throw new GameException($"Con hiến thêm phải {recipe.OtherStars}★, cùng phẩm hoặc thấp hơn linh thú chính 1 phẩm, không đang xuất chiến.");
        var back=donors.Sum(d=>d.FoodSpent/2);
        foreach(var donor in donors){StripPetGear(p,donor);db.Pets.Remove(donor);p.Pets.Remove(donor);}
        pet.Stars=recipe.TargetStars;pet.StarPts=PetData.StarAt(pet.Stars);
        if(back>0)p.Add("linh_thu_dan",back);
        return Log(p,$"Ghép linh thú: {PetData.ById[pet.DefId].Name} lên {pet.Stars}★ (+{PetData.StarBonus*100:0}% chỉ số, kỹ năng và thiên phú){(back>0? $", hoàn {back} Linh Thú Đan":"")}.","gold");
    }
    /// <summary>Admin: tăng n cấp cho linh thú, tối đa tới giới hạn cấp hiện tại.</summary>
    private (string,string) AdminPetLevel(Player p,string? id,int n)
    {
        if(!Guid.TryParse(id,out var petId))throw new GameException("Chưa chọn linh thú.");
        var pet=FindPet(p,petId);var cap=PetLevelCap(p);
        if(pet.Level>=cap)throw new GameException($"Đã đạt cấp tối đa {cap}.");
        pet.Level=(int)Math.Min(cap,(long)pet.Level+n);pet.Exp=0;
        return Log(p,$"[Admin] {PetData.ById[pet.DefId].Name} lên cấp {pet.Level}.","good");
    }
    /// <summary>Admin: tăng n sao cho linh thú, không cần con hiến (tối đa MaxStars).</summary>
    private (string,string) AdminPetStar(Player p,string? id,int n)
    {
        if(!Guid.TryParse(id,out var petId))throw new GameException("Chưa chọn linh thú.");
        var pet=FindPet(p,petId);var from=PetData.StarsOf(pet);
        if(from>=PetData.MaxStars)throw new GameException($"Đã đạt tối đa {PetData.MaxStars} sao.");
        pet.Stars=Math.Min(PetData.MaxStars,from+n);pet.StarPts=PetData.StarAt(pet.Stars);
        return Log(p,$"[Admin] {PetData.ById[pet.DefId].Name} lên {pet.Stars}★.","good");
    }
    public static CombatStats PetStats(Player p,Pet pet)
    {
        var d=PetData.ById[pet.DefId];var lv=Math.Min(pet.Level,PetLevelCap(p));
        var scale=PetData.StatScale(p.Realm,d)*PetData.QualityAdjust(pet,d)*(1+PetData.StarsFrom(PetData.Pts(pet))*PetData.StarBonus);   // sao từ ghép cùng loài   // Thú Đản Phàm: loài Linh nhưng chỉ số phẩm Phàm
        scale*=(1+Math.Clamp(pet.Awaken,0,PetTraining.MaxAwaken)*PetTraining.AwakenBonus)*(1+PetResonance(p,pet).Pct);   // thức tỉnh huyết mạch · cộng hưởng ngũ hành với công pháp đang kích hoạt
        int Stat(int b,int apt,string stat)=> (int)Math.Round(b*(1+(lv-1)*.22*apt/100.0)*(1+pet.Form*.4)*scale*(1+PetGearPct(pet,stat)/100));
        return new(Stat(d.Atk,pet.AptAtk,"atk"),Stat(d.Hp,pet.AptHp,"hp"),Stat(d.Def,pet.AptDef,"def"));
    }
    private static double PetWeight(Player p,Pet pet) => pet.Slot==1?1:0;
    public static double PetTalent(Player p,string talent) => p.Pets.Where(x=>PetData.ById.ContainsKey(x.DefId)).Sum(x=>PetData.ById[x.DefId].Talent==talent?PetData.ById[x.DefId].TalentValue*PetData.StarScale(PetData.StarsOf(x))*PetWeight(p,x):0);
    public static CombatStats PetBonus(Player p)
    {
        double a=0,d=0,h=0;foreach(var pet in p.Pets.Where(x=>PetData.ById.ContainsKey(x.DefId))){var st=PetStats(p,pet);var k=PetData.ById[pet.DefId].Forms[Math.Clamp(pet.Form,0,2)].ShareMult*PetWeight(p,pet);a+=Math.Floor(st.Atk*k);d+=Math.Floor(st.Def*k);h+=Math.Floor(st.Hp*k);}
        return new((int)a,(int)h,(int)d,PetTalent(p,"speed"),PetTalent(p,"crit"));
    }
    public static PetState PetStateFor(Player p){ var owner=Stats(p); return new(p.Pets.Where(x=>PetData.ById.ContainsKey(x.DefId)).Select(pet=>{
        var d=PetData.ById[pet.DefId];var st=PetStats(p,pet);var nums=PetData.NumbersOf(st,owner,pet.Form);/* mô tả kỹ năng ghi số thật */var form=d.Forms[Math.Clamp(pet.Form,0,2)];var k=form.ShareMult*PetWeight(p,pet);var cost=PetData.EvolveCost(pet.Form);
        return new PetDto(pet.Id,d.Id,d.Name,d.Element,PetData.Qualities[PetData.QualityOf(pet,d)],pet.Level,pet.Exp,PetData.ExpNeed(pet.Level),pet.Form,form.Name,pet.Slot,st.Atk,st.Def,st.Hp,pet.AptAtk,pet.AptDef,pet.AptHp,(int)(st.Atk*k),(int)(st.Def*k),(int)(st.Hp*k),PetData.TalentDescription(d,PetData.StarsOf(pet)),PetData.SkillDescription(d.Skill,pet.Form,PetData.StarsOf(pet),PetData.QualityOf(pet,d),nums),d.SkillEvery,cost.Select(x=>new CostDto(x.Key,x.Value,p.Count(x.Key))).ToArray(),EvolveGate(p,pet)&&CanPayEvolve(p,cost),d.Forms[PetAppearance(pet)].Image,PetAppearance(pet),EvolveHint(p,pet),PetData.StarsFrom(PetData.Pts(pet)),PetData.ReleaseValue(pet,d),PetData.Pts(pet),PetData.StarsOf(pet)<PetData.MaxStars?PetData.SkillDescription(d.Skill,pet.Form,PetData.StarsOf(pet)+1,PetData.QualityOf(pet,d),nums):"",PetData.StarsOf(pet)<PetData.MaxStars?PetData.TalentDescription(d,PetData.StarsOf(pet)+1):"",PetData.QualityOf(pet,d)==5?d.Miracle:null,TrainingFor(p,pet),d.Elements,RankedPetPower(p,pet));
    }).ToList(),Hatches(p).Select(h=>new HatchDto(h.Slot,h.Egg,h.Ready,Math.Max(0,(int)Math.Ceiling((h.Ready-DateTime.UtcNow).TotalSeconds)),h.Ready<=DateTime.UtcNow,InstantCost(h.Ready))).ToList(),PetData.Capacity(p.Realm),PetLevelCap(p),PetData.GuardSlots(p.Realm),PetData.HatchSlots(p.Realm),PetData.StarCost,PetData.StarRecipes,TrainingRules);}
    public (string msg,string kind,CraftResultDto craft) PetSpin(Player p,int n,DateTimeOffset? now=null)
    {
        n=Math.Clamp(n,1,10);var quota=CheckSpinDaily(p,n,now,pet:true);if(p.Count(GameData.PetSpinTicket)<n)throw new GameException("Không đủ Vé Linh Thú.");
        var pity=p.PetSpinPityUsed;
        var outs=Enumerable.Range(0,n).Select(_=>PetData.EggIds[SpinPity.Roll(ref pity,pet:true)]).ToArray();
        var need=outs.Distinct().Count(x=>p.Count(x)==0);if(BagCap(p)-BagUsed(p)+(p.Count(GameData.PetSpinTicket)==n?1:0)<need)throw new GameException("Hành trang đầy.");
        p.PetSpinPityUsed=pity;
        RecordSpinDaily(p,n,quota,pet:true);var ev=AddEventPoints(p,"pet",n);
        p.Add(GameData.PetSpinTicket,-n);foreach(var egg in outs)p.Add(egg,1);
        p.SpinHistory=string.Join(';',p.SpinHistory.Split(';',StringSplitOptions.RemoveEmptyEntries).Prepend($"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}|{string.Join(',',outs)}").Take(GameData.SpinHistoryKeep));
        var got=outs.GroupBy(x=>x).Select(g=>new ItemQtyDto(g.Key,g.Count())).ToList();return (ev>0?$"Đã quay Linh Thú. Thiên Cơ +{ev} điểm.":"Đã quay Linh Thú.","good",new("spin:pet",got[0],new(){new(GameData.PetSpinTicket,n)},got));
    }
}
