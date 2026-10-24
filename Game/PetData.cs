namespace VanDao.Game;

public record PetForm(string Name, string Image, double ShareMult, string SkillName);
public record PetStarRecipe(int TargetStars, int SameSpeciesStars, int OtherCount, int OtherStars);
public record PetMiracleDef(string Name,string Description);
public record PetDef(string Id, string Name, string Element, string Rarity, string Role, string Desc,
    PetForm[] Forms, string TalentText, string SkillText, int SkillEvery,
    string Talent, double TalentValue, string Skill, int Atk, int Def, int Hp, PetMiracleDef? Miracle = null);

public static class PetData
{
    public static PetMiracleDef? MiracleFor(string id) => id switch {
        "that_thai_ho_diep" => new("Thất Thải Niết Bàn","Nhận sát thương chí tử sau cứu mạng trang bị: hồi sinh với 25% máu tối đa. Một lần mỗi trận, không đặt lại khi đổi đợt quái. Không thêm khiên/bất tử, không hưởng tăng hồi máu hoặc sao."),
        "thai_co_loi_long" => new("Thái Cổ Thiên Kiếp","Còn sống và máu ≤30%: giáng sét tím gây 250% công, bỏ qua 30% phòng thủ địch và chắc chắn tê liệt 1 lượt. Một lần mỗi trận, không cộng dồn tê liệt. Không tăng theo sao, không đặt lại chu kỳ tuyệt kỹ."),
        _ => null
    };
    public const string StarterEgg = "thu_dan_hoa_ho";
    public static int EggQuality(string id) => id == StarterEgg ? 1 : Array.IndexOf(EggIds, id);
    public static readonly string[] Qualities = { "common", "uncommon", "rare", "epic", "legendary", "ancient" };
    public static readonly string[] EggIds = { "thu_dan_pham", "thu_dan_linh", "thu_dan_huyen", "thu_dan_dia", "thu_dan_thien", "thu_dan_thuong_co" };
    public static readonly int[] HatchSeconds = { 600, 1800, 3600, 14400, 28800, 43200 };
    public static readonly double[] PetSpinWeights = { 45, 30, 17, 7, .9, .1 };
    // Calibrated against peak-stage, full same-realm Huyen gear at aptitude 90.
    // Runtime scaling depends only on realm, never on the owner's actual gear.
    public static readonly double[] RealmStatScale = { .36, .385, 1.47, 2.98, 13.3, 59.8, 299.0, 1495.0 };
    // Phẩm Phàm → Thiên (10/2026: giãn từ .8–1.6 để các phẩm khác nhau rõ hơn).
    public static readonly double[] RarityStatScale = { .7, 1, 1.35, 1.8, 2.4, 3.2 };
    /// <summary>Khoảng tư chất theo phẩm linh thú (Phàm → Thượng Cổ); Hồ Điệp dùng tư chất Thượng Cổ.</summary>
    public static readonly int[] AptMin = { 40, 50, 60, 70, 80, 90 }, AptMax = { 75, 85, 90, 95, 100, 100 };
    // ---- linh thú thừa: ghép cùng loài lên sao, phóng sinh (lẻ / hàng loạt) hoàn Linh Thú Đan ----
    public const int MaxStars = 5;
    /// <summary>Ngưỡng điểm cũ chỉ để đọc dữ liệu đã lưu, không còn là chi phí ghép.</summary>
    public static readonly int[] StarCost = { 2, 3, 4, 5, 6 };
    /// <summary>Mã ngưỡng sao tương thích dữ liệu cũ; công thức hiến nằm ở StarRecipes.</summary>
    public static int StarAt(int s) => StarCost.Take(Math.Clamp(s, 0, MaxStars)).Sum();
    public static int MaxPts => StarAt(MaxStars);
    public static int StarsFrom(int pts) { var s = 0; while (s < MaxStars && pts >= StarAt(s + 1)) s++; return s; }
    /// <summary>Điểm ghép của con: con có sao từ luật cũ (chưa có StarPts) quy đổi bằng mốc của số sao đó.</summary>
    public static int Pts(VanDao.Data.Pet pet) => pet.StarPts > 0 || pet.Stars <= 0 ? pet.StarPts : StarAt(pet.Stars);
    public static readonly PetStarRecipe[] StarRecipes =
    [
        new(1, 0, 0, 0), new(2, 1, 1, 0), new(3, 2, 1, 1),
        new(4, 3, 1, 2), new(5, 4, 1, 3),
    ];
    public static int StarsOf(VanDao.Data.Pet pet) => StarsFrom(Pts(pet));
    public static bool CanStarDonor(VanDao.Data.Pet main, VanDao.Data.Pet donor, bool sameSpecies)
    {
        var stars = StarsOf(main);
        if (stars >= MaxStars || donor.Id == main.Id || donor.Slot != 0 ||
            !ById.ContainsKey(main.DefId) || !ById.ContainsKey(donor.DefId)) return false;
        var recipe = StarRecipes[stars];
        if (sameSpecies) return donor.DefId == main.DefId && StarsOf(donor) == recipe.SameSpeciesStars;
        var quality = QualityOf(main, ById[main.DefId]);
        var donorQuality = QualityOf(donor, ById[donor.DefId]);
        return recipe.OtherCount > 0 && StarsOf(donor) == recipe.OtherStars &&
            donorQuality <= quality && donorQuality >= Math.Max(0, quality - 1);
    }
    /// <summary>Mỗi sao +6% chỉ số, hệ số kỹ năng và thiên phú (5 sao: +30%).</summary>
    public const double StarBonus = .06;
    /// <summary>Linh Thú Đan nhận khi phóng sinh theo phẩm Phàm → Thượng Cổ (cộng nửa số đan đã cho ăn; mỗi sao tính thêm một phần).</summary>
    public static readonly int[] ReleaseFood = { 5, 10, 20, 40, 80, 160 };
    public static int ReleaseValue(VanDao.Data.Pet pet, PetDef d) => pet.FoodSpent / 2 + ReleaseFood[QualityOf(pet, d)] * (1 + StarsFrom(Pts(pet)));
    public static int SpeciesQuality(PetDef d) => Array.IndexOf(Qualities, d.Rarity);
    /// <summary>Phẩm của con linh thú: Quality riêng (vd. Phàm từ Thú Đản Phàm), không có thì theo loài.</summary>
    public static int QualityOf(VanDao.Data.Pet pet, PetDef d) => pet.Quality >= 0 && pet.Quality < Qualities.Length ? pet.Quality : SpeciesQuality(d);
    /// <summary>Chỉ số gốc của loài đã nhân phẩm loài; con mang phẩm khác loài thì đổi theo tỉ lệ.</summary>
    public static double QualityAdjust(VanDao.Data.Pet pet, PetDef d) => RarityStatScale[QualityOf(pet, d)] / RarityStatScale[SpeciesQuality(d)];
    public static double StatScale(int realm, PetDef def) =>
        RealmStatScale[Math.Clamp(realm, 0, RealmStatScale.Length - 1)];
    public const int FoodExp = 100;
    // Giảm 75% so với tỉ lệ gốc 15% / 12% / 10% / 8%.
    public static readonly double[] PetEggDrops = { .0375, .0375, .03, .025, .025, .025, .02, .02, .02, .02 };
    public static int EggQuality(int chapter) => chapter>=6?4:chapter>=3?3:chapter>=2?2:1;
    // Mốc tính theo cấp hiện tại: từ cấp 25, 50, 65, 80 tăng dần chi phí lên cấp.
    public static int ExpNeed(int level) => (50 + level * 25) * (level switch
    {
        >= 80 => 5,
        >= 65 => 4,
        >= 50 => 3,
        >= 25 => 2,
        _ => 1,
    });
    public static int LevelCap(int realm) => new[] {20,40,60,80,90,100}[Math.Clamp(realm,0,5)];
    public static int Capacity(int realm) => 20 + Math.Max(0,realm) * 5;
    public static int GuardSlots(int realm) => 0;
    public static int HatchSlots(int realm) => realm >= 5 ? 4 : realm >= 4 ? 3 : realm >= 2 ? 2 : 1;
    private static string[] SkillNames(string id) => id switch {
        "hoa_linh_ho" => ["Hỏa Vĩ", "Liệt Diễm Hồ Ảnh", "Cửu Vĩ Phần Thiên"],
        "bang_tinh_tho" => ["Sương Giáp", "Băng Kính Hộ Thân", "Quảng Hàn Thiên Mạc"],
        "thanh_moc_loc" => ["Thanh Mộc Hồi Xuân", "Linh Lộc Cam Lộ", "Vạn Mộc Trường Sinh"],
        "kim_giap_quy" => ["Kim Giáp Hộ Thể", "Huyền Nham Bích Lũy", "Bất Diệt Kim Thành"],
        "loi_van_dieu" => ["Lôi Vũ", "Vân Tiêu Lôi Kích", "Cửu Tiêu Lôi Kiếp"],
        "bich_thuy_giao" => ["Giao Long Thủy Tức", "Bích Hải Triều Sinh", "Thương Hải Quy Nguyên"],
        "thanh_phong_hac" => ["Thanh Phong Dưỡng Khí", "Linh Hạc Hồi Nguyên", "Cửu Thiên Tiên Tức"],
        "tu_dien_mieu" => ["Tử Điện Trảo", "Lôi Ảnh Tập Sát", "Tịch Diệt Thiên Lôi"],
        "chu_tuoc" => ["Xích Vũ", "Phượng Diễm Liệu Nguyên", "Chu Tước Phần Thiên"],
        "huyen_vu" => ["Huyền Thủy Thuẫn", "Quy Xà Hộ Trận", "Bắc Minh Thiên Bích"],
        "thanh_long" => ["Thanh Lôi Thổ Tức", "Cửu Thiên Lôi Dẫn", "Thanh Long Thiên Phạt"],
        "bach_ho" => ["Bạch Hổ Liệt Trảo", "Canh Kim Phá Quân", "Bạch Hổ Tru Thiên"],
        "that_thai_ho_diep" => ["Thất Thải Linh Quang", "Điệp Vũ Hồi Xuân", "Thượng Cổ Thất Thải"],
        "thai_co_loi_long" => ["Tử Điện Giáng", "Thiên Lôi Phán Quyết", "Thái Cổ Lôi Phạt"],
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Chưa đặt tên tuyệt kỹ cho linh thú.")
    };
    private static PetDef Make(string id,string name,string el,int q,string role,string talent,double value,string text,string skill)
    {
        var k = RarityStatScale[q];
        return new(id,name,el,Qualities[q],role,q==5?"Phẩm Thượng Cổ, cao hơn Thiên. Nở từ Thượng Cổ Thú Đản; trưởng thành ở cấp 30 và 60.":"Ấp từ Thú Đản; trưởng thành ở cấp 30 và 60.",
            Enumerable.Range(0,3).Select(f=>new PetForm((id=="thai_co_loi_long"?new[]{"Ấu thể","Trưởng thành","Thượng Cổ"}:new[]{"Ấu thể","Thành thể","Thần thú"})[f],$"img/pet/{id}{(id=="thanh_long"?"_loi":"")}_{f}.png",new[]{.10,.15,.22}[f],SkillNames(id)[f])).ToArray(),
            text,SkillDescription(skill,0,0,q),SkillEveryOf(skill),
            talent,value,skill,(int)Math.Round(24*k),(int)Math.Round(12*k),(int)Math.Round(120*k),q==5?MiracleFor(id):null);
    }
    public static double StarScale(int stars) => 1 + Math.Clamp(stars,0,MaxStars) * StarBonus;
    // ---- Tuyệt kỹ: Linh Uy = Công linh thú + Công chủ nhân × Đồng tâm (Linh Hộ: Máu linh thú + Máu chủ nhân × Đồng tâm) ----
    /// <summary>Đồng tâm theo hình thái: phần chỉ số chủ nhân cộng vào Linh Uy.</summary>
    public static readonly double[] SkillBond = { .3, .4, .5 };
    /// <summary>Ngân sách sát thương mỗi lượt (% Linh Uy, hình thái cuối) theo phẩm Phàm → Thượng Cổ.</summary>
    public static readonly double[] SkillBudget = { 20, 25, 30, 36, 42, 50 };
    /// <summary>Chiêu hồi máu / khiên dùng 15% ngân sách sát thương của phẩm.</summary>
    public const double SupportShare = .15;
    /// <summary>Hệ số hình thái của ngân sách: Ấu thể · Thành thể · Thần thú.</summary>
    public static readonly double[] SkillFormK = { .5, .75, 1 };
    public static int SkillEveryOf(string skill) => skill is "fox" or "frost" or "turtle" ? 5 : skill is "phoenix" or "butterfly" or "ancient_thunder" ? 3 : 4;
    /// <summary>Phẩm gốc của loài mang chiêu (dùng cho mô tả khi không biết phẩm riêng của con).</summary>
    private static readonly Dictionary<string,int> SkillQuality = new() {
        ["fox"]=1,["frost"]=1,["deer"]=2,["turtle"]=2,["eagle"]=2,["leech"]=3,["crane"]=3,["cat"]=3,
        ["phoenix"]=4,["xuanwu"]=4,["dragon"]=4,["tiger"]=4,["butterfly"]=5,["ancient_thunder"]=5 };
    /// <summary>Khống chế quái theo hệ linh thú: Lôi → tê liệt · Băng → đóng băng · Thổ / Phong → choáng (mỗi lần tung tuyệt kỹ, 1 lượt).</summary>
    public static (string Kind, double Chance) ControlOf(string skill) => skill switch {
        "ancient_thunder" => ("paralyze", .5), "cat" => ("paralyze", .25), "eagle" => ("paralyze", .2), "dragon" => ("paralyze", .2),
        "frost" => ("freeze", .3), "turtle" => ("stun", .25), "tiger" => ("stun", .25), _ => ("", 0) };
    public static string ControlName(string kind) => kind switch { "paralyze" => "tê liệt", "stun" => "choáng", "freeze" => "đóng băng", _ => "" };
    /// <summary>Quái đang đóng băng nhận thêm 10% sát thương (lượt kế tiếp, trước khi băng vỡ).</summary>
    public const double FreezeDamageBonus = .10;
    /// <summary>Phần ngân sách trả cho hiệu ứng phụ của chiêu (đánh dấu, xuyên thủ, giảm công…); khống chế tính 0,4 × tỉ lệ.</summary>
    private static double SkillEffectCost(string skill,int form) => (skill switch {
        "fox" => .15, "leech" => .2, "cat" => .25, "tiger" => .1, "dragon" => form==2 ? .1 : 0,
        "frost" => .25, "turtle" => .2, "crane" => .3, "xuanwu" => .3, _ => 0 }) + .4 * ControlOf(skill).Chance;
    public static bool SkillIsSupport(string skill) => skill is "frost" or "deer" or "turtle" or "crane" or "butterfly" or "xuanwu";
    /// <summary>Hệ số chiêu (% Linh Uy mỗi lần thi triển) = ngân sách × lượt hồi chiêu × hệ số hình thái × (1 − chi phí hiệu ứng).</summary>
    public static double SkillCoef(string skill,int form,int quality=-1)
    {
        var f=Math.Clamp(form,0,2);var q=quality is >= 0 and < 6 ? quality : SkillQuality.GetValueOrDefault(skill,1);
        return SkillBudget[q]*(SkillIsSupport(skill)?SupportShare:1)*SkillEveryOf(skill)*SkillFormK[f]*(1-SkillEffectCost(skill,f));
    }
    /// <summary>Hệ số phản đòn của Huyền Vũ: 15% hệ số sát thương nếu chiêu là chiêu đánh.</summary>
    public static double ReflectCoef(int form,int quality=-1)
    {
        var f=Math.Clamp(form,0,2);var q=quality is >= 0 and < 6 ? quality : SkillQuality["xuanwu"];
        return SkillBudget[q]*SkillEveryOf("xuanwu")*SkillFormK[f]*.15;
    }
    public static string Percent(double value) => value.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture).Replace('.',',');
    /// <param name="quality">Phẩm riêng của con; −1 = phẩm gốc của loài.</param>
    /// <summary>Chỉ số thật để mô tả kỹ năng bằng số cụ thể: Linh Uy, Linh Hộ (như PetCombat) và máu tối đa của chủ nhân.</summary>
    public record SkillNumbers(double Power, double Life, int OwnerHp);
    /// <summary>Linh Uy / Linh Hộ của một con: chỉ số riêng + chỉ số chủ nhân × Đồng tâm theo hình thái (khớp PetCombat).</summary>
    public static SkillNumbers NumbersOf(CombatStats pet, CombatStats owner, int form) =>
        new(pet.Atk + owner.Atk * SkillBond[Math.Clamp(form,0,2)], pet.Hp + owner.Hp * SkillBond[Math.Clamp(form,0,2)], owner.Hp);
    private static string Num(double v) => Math.Round(v).ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));
    /// <param name="n">Có chỉ số thật (linh thú đang có): ghi số máu / khiên / sát thương cụ thể; null = ghi theo % Linh Uy / Linh Hộ.</param>
    public static string SkillDescription(string skill,int form,int stars,int quality=-1,SkillNumbers? n=null)
    {
        var f=Math.Clamp(form,0,2);var scale=StarScale(stars);
        double Vn(double a,double b,double c)=>(f==0?a:f==1?b:c)*scale;
        string V(double a,double b,double c)=>Percent(Vn(a,b,c));
        var c=SkillCoef(skill,f,quality);string C(double share=1)=>Percent(Math.Round(c*share,1));
        // sát thương / lượng hồi, khiên: có chỉ số thật thì ghi số, không thì ghi % Linh Uy / Linh Hộ
        // cụm hành động: có chỉ số thật thì ghi số (trận đánh cắt phần lẻ hồi máu / khiên), không thì ghi theo % Linh Uy / Linh Hộ
        string Dmg(double share=1)=>n is null?$"gây sát thương bằng {C(share)}% Linh Uy":$"gây {Num(n.Power*c*share/100)} sát thương";
        string Heal(double share=1)=>n is null?$"hồi máu bằng {C(share)}% Linh Hộ":$"hồi {Num(Math.Floor(n.Life*c*share/100))} máu";
        string Guard(double share=1)=>n is null?$"tạo khiên bằng {C(share)}% Linh Hộ":$"tạo khiên {Num(Math.Floor(n.Life*c*share/100))}";
        string Reflect()=>n is null?$"gây sát thương bằng {Percent(Math.Round(ReflectCoef(f,quality),1))}% Linh Uy":$"gây {Num(n.Power*ReflectCoef(f,quality)/100)} sát thương";
        string OwnerHp(double pct)=>n is null?$"{Percent(pct)}% máu tối đa của chủ nhân":$"{Num(n.OwnerHp*pct/100)} máu";
        static string U(string x)=>char.ToUpper(x[0])+x[1..];
        var shieldCap=n is null?"Khiên từ linh thú tối đa 30% máu tối đa của chủ nhân.":$"Khiên từ linh thú tối đa {Num(n.OwnerHp*.3)} (30% máu chủ nhân).";
        var (ck,cc)=ControlOf(skill);
        var control=ck.Length==0||skill=="ancient_thunder"?"":$" {Percent(cc*100)}% cơ hội khiến quái {ControlName(ck)} 1 lượt"+(ck=="freeze"?$", lượt sau quái nhận thêm {Percent(FreezeDamageBonus*100)}% sát thương.":".");
        var text=skill switch {
            "fox" => $"{U(Dmg())}. Trong 2 lượt kế tiếp, đòn bạo kích của chủ nhân tăng {V(5,8,12)}% sát thương.",
            "frost" => $"{U(Guard())} cho chủ nhân, giảm {V(5,8,12)}% công địch trong 2 lượt.",
            "deer" => $"{U(Heal(.7))} cho chủ nhân, sau đó mỗi lượt {Heal(.15)} trong 2 lượt kế tiếp.",
            "turtle" => $"{U(Guard())} cho chủ nhân. Khi khiên còn, chủ nhân nhận ít hơn {V(3,5,7)}% sát thương.",
            "eagle" => $"Đánh 3 lần, tổng cộng {Dmg()} chia đều mỗi lần.",
            "leech" => $"{U(Dmg())}. Chủ nhân hồi máu bằng {V(20,25,30)}% sát thương thực gây ra, tối đa {OwnerHp(Vn(4,6,8))}.",
            "crane" => $"{U(Heal())} cho chủ nhân, tăng {V(5,8,12)}% né tránh trong 2 lượt.",
            "cat" => $"{U(Dmg())}, bỏ qua {V(15,25,35)}% phòng thủ địch.",
            "phoenix" => $"{U(Dmg(.6))}, rồi thiêu đốt {Dmg(.2)} mỗi lượt trong 2 lượt kế tiếp.",
            "xuanwu" => $"{U(Guard())} cho chủ nhân. Trong 2 lượt, mỗi lần chủ nhân bị đánh thì phản đòn {Reflect()}.",
            "dragon" => $"Gọi 3 tia sét, tổng cộng {Dmg()} chia đều mỗi tia."+(f==2?" Cả chiêu bỏ qua 20% phòng thủ địch.":""),
            "tiger" => $"{U(Dmg())}; mục tiêu dưới 30% máu chịu thêm 25% sát thương chiêu.",
            "butterfly" => $"{U(Heal(.45))} và {Guard(.55)} cho chủ nhân.",
            "ancient_thunder" => $"Gọi sét tím từ trời xuống {Dmg()}; 50% cơ hội khiến đối thủ tê liệt trong 1 lượt. Mỗi 3 lượt chủ nhân kích hoạt.",
            _ => throw new ArgumentOutOfRangeException(nameof(skill))
        };
        // khiên: ghi giới hạn khiên; có số sát thương thật thì nhắc số đó chưa trừ thủ địch (PetCombat trừ một lần cho cả chiêu)
        var shield=skill is "frost" or "turtle" or "xuanwu" or "butterfly";
        var hits=skill is not ("frost" or "turtle" or "deer" or "crane" or "butterfly");
        var note=(shield?" "+shieldCap:"")+(n is not null&&hits?" Số sát thương chưa trừ phòng thủ địch.":"");
        return text+note+control;
    }
    public static string TalentDescription(PetDef def,int stars)
    {
        var value=def.TalentValue*StarScale(stars);var v=Percent(value);
        var effect=def.Talent switch {
            "crit"=>$"chủ nhân +{v}% bạo kích.",
            "guard"=>$"chủ nhân giảm {v}% sát thương nhận vào.",
            "healing"=>$"dưới 50% máu, chủ nhân nhận thêm {v}% hiệu quả hồi máu.",
            "healing_all"=>$"chủ nhân nhận thêm {v}% hiệu quả hồi máu ở mọi ngưỡng máu.",
            "def"=>$"chủ nhân +{v}% phòng thủ.",
            "extra"=>$"chủ nhân +{v}% liên kích.",
            "qi"=>$"chủ nhân +{v}% tốc độ thu tu vi.",
            "speed"=>$"chủ nhân +{v}% thân pháp, tăng né tránh, tăng tốc di chuyển và giao chiến.",
            "pen"=>$"chủ nhân +{v}% xuyên thủ.",
            "burn_damage"=>$"chủ nhân gây thêm {v}% sát thương lên mục tiêu đang cháy.",
            "shield"=>$"đầu trận nhận khiên bằng {v}% máu tối đa.",
            "atk"=>$"chủ nhân +{v}% tấn công.",
            "critdmg"=>$"chủ nhân +{v}% sát thương bạo kích (150% → {Percent(150+value)}%).",
            _=>def.TalentText
        };
        return def.TalentText.Split(':')[0]+": "+effect;
    }
    public static readonly PetDef[] Definitions = {
        Make("hoa_linh_ho","Hỏa Linh Hồ","hoa",1,"atk","crit",4,"Hồ Hỏa Linh Tâm: chủ nhân +4% bạo kích.","fox"),
        Make("bang_tinh_tho","Băng Tinh Thố","bang",1,"def","guard",4,"Băng Tâm Hộ Chủ: chủ nhân giảm 4% sát thương nhận vào.","frost"),
        Make("thanh_moc_loc","Thanh Mộc Lộc","moc",2,"hp","healing",15,"Sinh Sinh Bất Tức: dưới 50% máu, chủ nhân nhận thêm 15% hiệu quả hồi máu.","deer"),
        Make("kim_giap_quy","Kim Giáp Quy","tho",2,"def","def",10,"Kim Giáp Kiên Tâm: chủ nhân +10% phòng thủ.","turtle"),
        Make("loi_van_dieu","Lôi Vân Điêu","loi",2,"atk","extra",5,"Lôi Đình Truy Kích: chủ nhân +5% liên kích.","eagle"),
        Make("bich_thuy_giao","Bích Thủy Giao","thuy",3,"qi","qi",10,"Thủy Linh Dẫn Khí: chủ nhân +10% tốc độ thu tu vi.","leech"),
        Make("thanh_phong_hac","Thanh Phong Hạc","gio",3,"crit","speed",5,"Thanh Phong Đồng Hành: chủ nhân +5% thân pháp, tăng né tránh, tăng tốc di chuyển và giao chiến.","crane"),
        Make("tu_dien_mieu","Tử Điện Miêu","loi",3,"atk","pen",8,"Tử Điện Xuyên Tâm: chủ nhân +8% xuyên thủ.","cat"),
        Make("chu_tuoc","Chu Tước","hoa",4,"atk","burn_damage",10,"Bất Diệt Chân Hỏa: chủ nhân gây thêm 10% sát thương lên mục tiêu đang cháy.","phoenix"),
        Make("huyen_vu","Huyền Vũ","thuy",4,"def","shield",10,"Bắc Minh Hộ Chủ: đầu trận nhận khiên bằng 10% máu tối đa.","xuanwu"),
        Make("thanh_long","Thanh Long","loi",4,"atk","atk",8,"Lôi Long Chi Uy: chủ nhân +8% tấn công.","dragon"),
        Make("bach_ho","Bạch Hổ","gio",4,"crit","critdmg",20,"Canh Kim Sát Phạt: chủ nhân +20% sát thương bạo kích (150% → 170%).","tiger"),
        Make("that_thai_ho_diep","Thất Thải Hồ Điệp","moc",5,"hp","healing_all",18,"Thất Thải Tiên Duyên: chủ nhân nhận thêm 18% hiệu quả hồi máu ở mọi ngưỡng máu.","butterfly"),
        Make("thai_co_loi_long","Thái Cổ Lôi Long","loi",5,"atk","pen",12,"Lôi Uy Xuyên Giáp: chủ nhân +12% xuyên thủ.","ancient_thunder") };
    public static readonly IReadOnlyDictionary<string,PetDef> ById = Definitions.ToDictionary(x=>x.Id);
    // Common eggs contain the two entry species; egg quality controls aptitude, species retains its own rarity.
    public static PetDef[] Pool(int quality) => quality==0 ? Definitions.Take(2).ToArray() : Definitions.Where(d=>d.Rarity==Qualities[quality]).ToArray();
    public static PetDef RollHatch(int quality, Random rng)
    {
        var pool=Pool(quality);return pool[rng.Next(pool.Length)];
    }
    public static Dictionary<string,int> EvolveCost(int form) => form==0 ? new(){["hoa_hinh_dan"]=10,["linh_thach"]=10000,["kim_o_tinh_hach"]=2} : form==1 ? new(){["than_thu_huyet"]=10,["hoa_hinh_dan"]=20,["linh_thach"]=100000,["huyet_giao_hach"]=3} : new();
    public static readonly ItemDef[] Items = EggIds.Select((id,q)=>new ItemDef(id,new[]{"Phàm","Linh","Huyền","Địa","Thiên","Thượng Cổ"}[q]+" Thú Đản","egg","#bda5ef",q==5?"Ấp 12 giờ, nở Thất Thải Hồ Điệp hoặc Thái Cổ Lôi Long phẩm Thượng Cổ (mỗi loài 50%). Nhận từ Quay Linh Thú (0,1%).":"Ấp trong menu Linh thú.",Icon:$"img/pet/egg_{new[]{"pham","linh","huyen","dia","thien","thuong_co"}[q]}.png",Rarity:Qualities[q],Kind:"Thú Đản"))
        .Concat(new[]{new ItemDef(StarterEgg,"Trứng Hỏa Hồ","egg","#edaa73","Quà khởi đầu. Ấp chắc chắn nhận Hỏa Linh Hồ.",Icon:"img/pet/egg_linh.png",Rarity:"uncommon",Kind:"Thú Đản"),new ItemDef("linh_thu_dan","Linh Thú Đan","petfood","#abd78a","+100 EXP linh thú. Rơi ở ải thường, chia tỉ lệ với đan tiến giai; chương và ải cao rơi nhiều hơn.",Icon:"img/pet/linh_thu_dan.png",Rarity:"uncommon"),new ItemDef("hoa_hinh_dan","Hóa Hình Đan","petmaterial","#bda5ef","Nguyên liệu tiến hóa. Có thể rơi từ Boss và Rương Boss.",Icon:"img/pet/hoa_hinh_dan.png",Rarity:"epic"),new ItemDef("than_thu_huyet","Thần Thú Huyết","petmaterial","#efab86","Nguyên liệu thần thú. Rơi từ Boss chương 5 trở đi hoặc mức Khó / Ác mộng và Rương Boss tương ứng.",Icon:"img/pet/than_thu_huyet.png",Rarity:"legendary")}).ToArray();
}
