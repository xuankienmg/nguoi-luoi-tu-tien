namespace VanDao.Game;

public static partial class GameData
{
    /// <summary>Kim Tiên (11/2026): cảnh giới thứ 11, sau Chân Tiên.</summary>
    public const int KimTienRealm = 10;
    public const string KimTienPill = "kim_tien_dan", KimTienCore = "dia_nguc_hoa_long_hach";
    /// <summary>Bộ lọc vàng kim cho ảnh Chân Tiên dùng lại ở đan / trang bị Kim Tiên (chờ ảnh riêng).</summary>
    public const string KimTienTint = "sepia(.45) saturate(1.9) hue-rotate(-12deg) brightness(1.1)";

    private static IEnumerable<ItemDef> FinalRealmItems()
    {
        // Kim Tiên Đan chưa có ảnh riêng: dùng ảnh Chân Tiên Đan kèm bộ lọc vàng kim.
        foreach (var (id, name, tier, realm, icon, tint) in new[] {
            ("do_kiep_dan", "Độ Kiếp Đan", 9, 7, "do_kiep_dan", (string?)null),
            ("chan_tien_dan", "Chân Tiên Đan", 10, 8, "chan_tien_dan", null),
            (KimTienPill, "Kim Tiên Đan", 11, 9, "chan_tien_dan", KimTienTint) })   // Thập Nhất Phẩm (Alchemy.MaxRank = 11)
            yield return new(id, name, "pill", "#dcc5ff", "Linh đan ngưng tụ đạo vận, dùng tiến giai và tu luyện.",
                $"img/dan/{icon}.png", Tint: tint, Tier: tier, MinRealm: realm, Rarity: "immortal", Kind: "Đan dược",
                Use: "Luyện từ Hỗn Nguyên Đạo Liên, Âm Dương Bàn Đào, linh thạch và linh thảo Hiếm bắt buộc trong đan phương; dùng đột phá và lên kỳ.");
        for (var realm = 7; realm <= 9; realm++)
            yield return new(BreakPillId(realm), realm switch { 7 => "Độ Kiếp Phá Chướng Đan", 8 => "Chân Tiên Phá Chướng Đan", _ => "Kim Tiên Phá Chướng Đan" },
                "elixir", "#ddc5ff", "Ổn định đạo tâm khi vượt bình cảnh.", $"img/dan/{BreakPillId(realm)}.png", MinRealm: realm, Rarity: "immortal", Kind: "Đan dược",
                Use: "+10% tỉ lệ đột phá mỗi viên, tối đa +30%; giữ hiệu lực đến khi thành công.");
        yield return new(BreakPillId(KimTienRealm), "Kim Tiên Phá Chướng Đan · Nội cảnh", "elixir", "#ddc5ff",
            "Hỗ trợ vượt bình cảnh giữa các kỳ Kim Tiên.", $"img/dan/{BreakPillId(9)}.png", Tint: KimTienTint, MinRealm: KimTienRealm, Rarity: "immortal", Kind: "Đan dược",
            Use: "Không bắt buộc. +10 điểm % tỉ lệ đột phá mỗi viên, tối đa +30 điểm %, trần tổng 95%; giữ hiệu lực đến khi đột phá thành công.");
        yield return new("ac_mong_cung_ky_hach", "Ác Mộng Cùng Kỳ Hạch", "boss", "#b183ff",
            "Tinh hạch lôi kiếp từ Cùng Kỳ chương 5 mức Ác Mộng.", "img/core/core_12.png", Rarity: "immortal", Kind: "Nguyên liệu Boss",
            Use: "Đột phá Đại Thừa → Độ Kiếp. Rơi chắc chắn từ Boss chương 5 Ác Mộng; mất khi đột phá thất bại.");
        yield return new(KimTienCore, "Địa Ngục Hỏa Long Hạch", "boss", "#ffcf6a",
            "Long hạch nóng rực của Hỏa Long trấn giữ Liệt Hỏa Uyên ở Địa Ngục 2.", "img/core/core_15.png", Tint: "hue-rotate(-25deg) saturate(1.6)", Rarity: "supreme", Kind: "Nguyên liệu Boss",
            Use: "Chỉ rơi từ Boss Hỏa Long chương 5 Địa Ngục 2 (100%, 1 viên). Cần để đột phá Chân Tiên → Kim Tiên; mất khi đột phá thất bại.");
    }

    private static IEnumerable<ItemDef> FinalRealmGear()
    {
        var pieces = new Dictionary<string, (string Name, string Desc)> {
            ["kiem"]=("Ngự Lôi Kiếm", "Trường kiếm khảm tinh ngọc, kiếm khí xuyên phá lớp giáp hộ thân."),
            ["chau"]=("Hỗn Nguyên Châu", "Linh châu trong thiên nghi kim văn, ngưng tụ linh lực thành đòn công kích."),
            ["mu"]=("Cửu Thiên Quan", "Ngọc quan khắc phù trận, bảo hộ thần thức trước uy áp thiên địa."),
            ["ao"]=("Vạn Đạo Bào", "Đạo bào nhiều lớp thêu kim văn, giữ vững nguyên thần và nhục thân."),
            ["gang"]=("Long Văn Hộ Uyển", "Hộ uyển long văn dẫn linh lực qua đôi tay khi thi triển công pháp."),
            ["quan"]=("Lưu Vân Khố", "Trường khố thêu vân văn, nhẹ như mây nhưng bền trước đao kiếm."),
            ["giay"]=("Đạp Hư Hài", "Tiên hài giữ bước chân ổn định khi vượt qua hư không."),
            ["day"]=("Đạo Tâm Trụy", "Ngọc trụy ôn dưỡng đạo tâm và hỗ trợ hấp thu linh khí."),
            ["phu"]=("Hộ Mệnh Phù", "Linh phù khắc pháp ấn nhiều lớp, bảo hộ sinh cơ khi giao chiến."),
            ["nhan"]=("Quy Nguyên Giới", "Tinh ngọc trên mặt nhẫn gom linh khí về kinh mạch chủ nhân.")
        };
        // Kim Tiên: tên / mô tả riêng, ảnh dùng lại bộ Thái Sơ (ct_) kèm bộ lọc vàng kim.
        var kimTien = new Dictionary<string, (string Name, string Desc)> {
            ["kiem"]=("Trảm Đạo Kiếm", "Tiên kiếm đúc từ kim tinh bất hủ, một kiếm chém đứt nhân quả."),
            ["chau"]=("Hỗn Độn Kim Châu", "Kim châu chứa một tia hỗn độn khí, xoay chuyển là thiên địa đảo lộn."),
            ["mu"]=("Tử Kim Quan", "Kim quan khảm tử tinh, giữ nguyên thần bất động giữa vạn kiếp."),
            ["ao"]=("Cửu Long Kim Bào", "Kim bào thêu chín rồng vàng, vạn pháp khó xâm."),
            ["gang"]=("Kim Cang Hộ Uyển", "Hộ uyển kim cang bất hoại, dẫn tiên lực tụ nơi đôi chưởng."),
            ["quan"]=("Lưu Kim Khố", "Trường khố dệt kim tuyến, nhẹ như mây mà vững như núi."),
            ["giay"]=("Đạp Vân Kim Hài", "Kim hài đạp mây, một bước vượt vạn dặm hư không."),
            ["day"]=("Kim Liên Trụy", "Ngọc trụy hình kim liên, đạo tâm thanh tịnh không nhiễm bụi trần."),
            ["phu"]=("Bất Hủ Kim Phù", "Kim phù khắc tiên văn bất hủ, che chở sinh cơ chủ nhân."),
            ["nhan"]=("Quy Nhất Kim Giới", "Kim giới thu vạn pháp về một, linh khí tuôn chảy không ngừng.")
        };
        foreach (var (prefix, name, realm, mult, tint) in new[] {
            ("dk_", "Thiên Kiếp", 8, 15625d, "hue-rotate(235deg) saturate(1.2)"),
            ("ct_", "Thái Sơ", 9, 78125d, "brightness(1.2) saturate(.65)"),
            ("kt_", "Kim Tiên", KimTienRealm, 390625d, KimTienTint) })   // ×5 mỗi cảnh giới như các bộ trước
            foreach (var item in DeriveSet(prefix, name, realm, mult, tint, "Tiên khí hợp đạo, bảo hộ chân thân."))
            {
                var slot=item.Id[prefix.Length..];
                var piece=prefix=="kt_" ? kimTien[slot] : pieces[slot];
                var title=prefix=="ct_" ? piece.Name.Replace("Ngự Lôi", "Khai Thiên").Replace("Cửu Thiên", "Thái Thanh") : piece.Name;
                yield return item with { Name = name + " " + title, Desc = piece.Desc,
                    Icon=$"img/final_realms/gear/{(prefix=="kt_" ? "ct_" : prefix)}{slot}.png", Tint=prefix=="kt_" ? KimTienTint : null };
            }
    }

    private static IEnumerable<RealmDef> FinalRealms()
    {
        // Độ Kiếp (8) · Chân Tiên (9) · Kim Tiên (10, 11/2026). Mỗi cảnh giới: tu vi ×16, chỉ số ×5.
        var defs = new[] {
            (Realm: 8, Id: "do_kiep", Name: "Độ Kiếp", Desc: "Vượt thiên kiếp, rèn nguyên thần bất diệt.", Pill: "do_kiep_dan", Prefix: "dk_", Aura: "#ac80ff",
                Bag: 700, Qi: 13312000000000d, Rate: 4500000, StageStone: 400000, StatMult: 562500, Halo: "img/halo_08.png"),
            (Realm: 9, Id: "chan_tien", Name: "Chân Tiên", Desc: "Phi thăng tiên đạo, trường sinh cùng thiên địa.", Pill: "chan_tien_dan", Prefix: "ct_", Aura: "#ffe9b0",
                Bag: 800, Qi: 212992000000000d, Rate: 24000000, StageStone: 1200000, StatMult: 2812500, Halo: "img/halo_09.png"),
            (Realm: KimTienRealm, Id: "kim_tien", Name: "Kim Tiên", Desc: "Tiên thân bất hủ như kim cương, vạn kiếp không mài mòn, đạo quả viên mãn.", Pill: KimTienPill, Prefix: "kt_", Aura: "#ffd34d",
                Bag: 900, Qi: 3407872000000000d, Rate: 128000000, StageStone: 3600000, StatMult: 14062500, Halo: "img/halo_09.png") };   // chưa có halo_10: dùng chung vòng sáng Chân Tiên
        foreach (var d in defs)
        {
            var realm = d.Realm;
            yield return new RealmDef {
                Id = d.Id, Name = d.Name, Cn = "", Desc = d.Desc,
                Stages = ["Sơ kỳ", "Trung kỳ", "Hậu kỳ"], Aura = d.Aura,
                Lotus = "#fff4dc", Sash = "#e6c46a", BagSlots = d.Bag,
                EquipSlots = ["weapon", "offhand", "head", "body", "hands", "legs", "feet", "necklace", "charm", "ring"],
                // These realms grant progression and aura effects, not a new costume.
                Hero = "img/hero_hopthe.png", HeroStand = "img/hero_hopthe_stand.png",
                HeroMed = "img/med_hopthe.png", HeroCast = "img/hoa_cau_ref_hopthe.png", Halo = d.Halo,
                QiReq = s => (decimal)Math.Round(d.Qi * Math.Pow(1.7, s)),
                Rate = s => d.Rate,
                StageCost = s => new() { ["linh_thach"] = d.StageStone * (s + 1), [d.Pill] = s + 1 },
                BreakCost = realm switch {
                    8 => new() { ["chan_tien_dan"] = 1, ["linh_thach"] = 4000000, ["ac_mong_cung_ky_vuong_hach"] = 1 },
                    9 => new() { [KimTienPill] = 1, ["linh_thach"] = 12000000, [KimTienCore] = 1 },
                    _ => null },
                BreakChance = realm switch { 8 => .08, 9 => .06, _ => 1 }, StatMult = d.StatMult,
                Boss = realm == 9 ? new("Hỏa Long", "img/hell2/boss_hoa_long.png", 1, 1, 1) : new("Cùng Kỳ Vương", "img/boss_cung_ky_vuong.png", 1, 1, 1),
                BossItem = realm switch { 8 => "ac_mong_cung_ky_vuong_hach", 9 => KimTienCore, _ => "" },
                Drops = new[] { new DropDef("linh_thach", 700, 1600, 1) }.Concat(
                    new[] { "kiem", "chau", "mu", "ao", "gang", "quan", "giay", "day", "phu", "nhan" }
                        .Select(s => new DropDef(d.Prefix + s, 1, 1, .006))).ToArray(),
                Resources = ["linh_thach", d.Pill, "hon_nguyen_dao_lien", "am_duong_ban_dao", realm == 8 ? "ac_mong_cung_ky_vuong_hach" : KimTienCore]
            };
        }
    }
}

public static class FinalRealmSkills
{
    private static readonly Dictionary<string, string[]> Names = new() {
        ["hoa"]=["Dịch Linh Ấn", "Cửu Kiếp Hỏa Thể", "Ngũ Chưởng Dịch Linh Ấn", "Bất Diệt Tiên Hỏa"],
        ["thuy"]=["Dịch Linh Thủy Ấn", "Huyền Thủy Độ Kiếp", "Ngũ Chưởng Triều Sinh", "Vô Lượng Tiên Thủy"],
        ["moc"]=["Kiến Mộc Phá Kiếp", "Vạn Kiếp Sinh Tức", "Thanh Đế Khai Thiên", "Trường Sinh Tiên Thể"],
        ["tho"]=["Kiếp Sơn Trấn Ngục", "Bất Động Kiếp Thân", "Hậu Thổ Định Càn Khôn", "Vô Cực Tiên Thổ"],
        ["loi"]=["Cửu Tiêu Thiên Kiếp", "Vạn Lôi Đạo Thể", "Thái Sơ Lôi Phạt", "Hỗn Nguyên Lôi Tâm"],
        ["bang"]=["Thiên Kiếp Hàn Ngục", "Băng Phách Độ Kiếp", "Vĩnh Hằng Tiên Băng", "Vô Trần Băng Tâm"],
        ["gio"]=["Cửu Kiếp Phong Bạo", "Tiêu Dao Kiếp Thể", "Thái Hư Tiên Trảm", "Vô Cực Phong Thần"]
    };
    /// <summary>Công pháp Kim Tiên (11/2026): [chủ động, bị động] mỗi hệ. Chủ động dùng VFX Thần Binh Trảm theo hệ (img/fx/kim-tien/&lt;hệ&gt;).</summary>
    private static readonly Dictionary<string, string[]> KimTienNames = new() {
        ["hoa"]=["Kim Ô Thần Tướng Trảm", "Bất Hủ Kim Diễm Thể"],
        ["thuy"]=["Thiên Hà Thần Tướng Trảm", "Bất Hủ Kim Thủy Thể"],
        ["moc"]=["Trường Sinh Thần Tướng Trảm", "Bất Hủ Kim Mộc Thể"],
        ["tho"]=["Trấn Sơn Thần Tướng Trảm", "Bất Hủ Kim Cương Thể"],
        ["loi"]=["Cửu Tiêu Thần Tướng Trảm", "Bất Hủ Kim Lôi Thể"],
        ["bang"]=["Hàn Thiên Thần Tướng Trảm", "Bất Hủ Kim Băng Thể"],
        ["gio"]=["Toái Phong Thần Tướng Trảm", "Bất Hủ Kim Phong Thể"]
    };
    /// <summary>Cảnh giới có nhánh công pháp nối tiếp, theo thứ tự.</summary>
    private static readonly (int Realm, string Id)[] Realms = [(8, "do_kiep"), (9, "chan_tien"), (GameData.KimTienRealm, "kim_tien")];
    private static string NameOf(string element, int realm, bool active) =>
        realm == GameData.KimTienRealm ? KimTienNames[element][active ? 0 : 1] : Names[element][(realm - 8) * 2 + (active ? 0 : 1)];

    // cân bằng 11/2026: mỗi hệ tăng chỉ số bị động bậc Độ Kiếp / Chân Tiên theo vai trò riêng (tổng điểm ngân sách gần như nhau)
    private static readonly Dictionary<string, (double Atk, double Def, double Hp, double Crit, double Skill, double Pen, double Weaken)> Step = new() {
        ["hoa"] = (0, 0, 0, 0, 1.5, 0.5, 0), ["thuy"] = (0, 0, 1.5, 0, 0.7, 0, 0.3), ["moc"] = (0, 1, 2, 0, 1, 0, 0),
        ["tho"] = (0, 2, 1.5, 0, 1, 0, 0), ["loi"] = (1, 0, 0, 1, 0, 0, 0), ["bang"] = (0, 1.5, 0, 0, 1, 1, 0),
        ["gio"] = (1, 0, 0, 0.5, 0, 0, 0)
    };

    private static string PassiveEffect(SkillDef previous) => "Mỗi cấp: " + string.Join(", ", new[] {
        ("công",previous.AtkPct), ("máu",previous.HpPct), ("sát thương kỹ năng",previous.SkillPct),
        ("phòng thủ",previous.DefPct), ("bạo kích",previous.Crit), ("né tránh",previous.Speed),
        ("tu luyện",previous.QiPct), ("xuyên thấu",previous.Pen), ("giảm công quái",previous.MonAtkDown)
    }.Where(x=>x.Item2>0).Select(x=>$"{x.Item1} +{x.Item2.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)}%")) + ".";

    // Reuse proven visual families while extending each element's actual skill tree.
    public static IEnumerable<SkillDef> Extend(IEnumerable<SkillDef> source)
    {
        var original = source.ToArray();
        foreach (var s in original) yield return s;
        foreach (var group in original.GroupBy(s => s.Element))
            foreach (var tree in new[] { "active", "passive" })
            {
                var previous = group.Where(s => s.Tree == tree).OrderByDescending(s => s.MinRealm).First();
                foreach (var (realm, realmId) in Realms)
                {
                    var id = $"{group.Key}_{realmId}_{tree}";
                    var active = tree == "active";
                    var next = previous with {
                        Id = id, Name = NameOf(group.Key, realm, active), MinRealm = realm, Row = realm + 1,
                        Cost = active ? realm + 4 : realm + 1, Req = [previous.Id + ":3"],
                        DmgBase = active ? (int)Math.Round(previous.DmgBase * 1.2) : 0,
                        DmgPerLv = active ? previous.DmgPerLv + 6 : 0,
                        AtkPct = active ? 0 : previous.AtkPct + Step[group.Key].Atk, HpPct = active ? 0 : previous.HpPct + Step[group.Key].Hp,
                        SkillPct = active ? 0 : previous.SkillPct + Step[group.Key].Skill,
                        DefPct = active ? previous.DefPct : previous.DefPct + Step[group.Key].Def,
                        Crit = active ? previous.Crit : previous.Crit + Step[group.Key].Crit,
                        Pen = active ? previous.Pen : previous.Pen + Step[group.Key].Pen,
                        MonAtkDown = active ? previous.MonAtkDown : previous.MonAtkDown + Step[group.Key].Weaken,
                        Code = $"{group.Key.ToUpperInvariant()}_{tree.ToUpperInvariant()}_REALM_{realm}",
                        Effect = active ? $"Tiên pháp cao cấp: sát thương {(int)Math.Round(previous.DmgBase * 1.2)}% công, thêm {previous.DmgPerLv + 6}% mỗi cấp."
                            : ""
                    };
                    if (!active) next = next with { Effect = PassiveEffect(next) };
                    yield return next; previous = next;
                }
            }
    }
}
