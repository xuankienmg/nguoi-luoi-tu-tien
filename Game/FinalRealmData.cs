namespace VanDao.Game;

public static partial class GameData
{
    private static IEnumerable<ItemDef> FinalRealmItems()
    {
        foreach (var (id, name, tier, realm, tint) in new[] {
            ("do_kiep_dan", "Độ Kiếp Đan", 9, 7, "hue-rotate(240deg)"),
            ("chan_tien_dan", "Chân Tiên Đan", 10, 8, "brightness(1.25)") })
            yield return new(id, name, "pill", "#dcc5ff", "Linh đan ngưng tụ đạo vận, dùng tiến giai và tu luyện.",
                "img/dan/dan_15.png", Tier: tier, MinRealm: realm, Tint: tint, Rarity: "immortal", Kind: "Đan dược",
                Use: "Luyện từ Hỗn Nguyên Đạo Liên, Âm Dương Bàn Đào, linh thạch và linh thảo Hiếm bắt buộc trong đan phương; dùng đột phá và lên kỳ.");
        for (var realm = 7; realm <= 8; realm++)
            yield return new(BreakPillId(realm), realm == 7 ? "Độ Kiếp Phá Chướng Đan" : "Chân Tiên Phá Chướng Đan",
                "elixir", "#ddc5ff", "Ổn định đạo tâm khi vượt bình cảnh.", "img/dan/dan_02.png", MinRealm: realm, Rarity: "immortal", Kind: "Đan dược",
                Use: "+10% tỉ lệ đột phá mỗi viên, tối đa +30%; giữ hiệu lực đến khi thành công.");
        yield return new(BreakPillId(9), "Chân Tiên Phá Chướng Đan · Nội cảnh", "elixir", "#ddc5ff",
            "Hỗ trợ vượt bình cảnh giữa các kỳ Chân Tiên.", "img/dan/dan_02.png", MinRealm: 9, Rarity: "immortal", Kind: "Đan dược",
            Use: "Không bắt buộc. +10 điểm % tỉ lệ đột phá mỗi viên, tối đa +30 điểm %, trần tổng 95%; giữ hiệu lực đến khi đột phá thành công.");
        yield return new("ac_mong_cung_ky_hach", "Ác Mộng Cùng Kỳ Hạch", "boss", "#b183ff",
            "Tinh hạch lôi kiếp từ Cùng Kỳ chương 5 mức Ác Mộng.", "img/core/core_12.png", Rarity: "immortal", Kind: "Nguyên liệu Boss",
            Use: "Đột phá Đại Thừa → Độ Kiếp. Rơi chắc chắn từ Boss chương 5 Ác Mộng; mất khi đột phá thất bại.");
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
        foreach (var (prefix, name, realm, mult, tint) in new[] {
            ("dk_", "Thiên Kiếp", 8, 15625d, "hue-rotate(235deg) saturate(1.2)"),
            ("ct_", "Thái Sơ", 9, 78125d, "brightness(1.2) saturate(.65)") })
            foreach (var item in DeriveSet(prefix, name, realm, mult, tint, "Tiên khí hợp đạo, bảo hộ chân thân."))
            {
                var piece=pieces[item.Id[prefix.Length..]];
                var title=prefix=="ct_" ? piece.Name.Replace("Ngự Lôi", "Khai Thiên").Replace("Cửu Thiên", "Thái Thanh") : piece.Name;
                yield return item with { Name = name + " " + title, Desc = piece.Desc,
                    Icon=$"img/final_realms/gear/{item.Id}.png", Tint=null };
            }
    }

    private static IEnumerable<RealmDef> FinalRealms()
    {
        foreach (var realm in new[] { 8, 9 })
        {
            var id = realm == 8 ? "do_kiep" : "chan_tien";
            var pill = realm == 8 ? "do_kiep_dan" : "chan_tien_dan";
            var prefix = realm == 8 ? "dk_" : "ct_";
            yield return new RealmDef {
                Id = id, Name = realm == 8 ? "Độ Kiếp" : "Chân Tiên", Cn = "",
                Desc = realm == 8 ? "Vượt thiên kiếp, rèn nguyên thần bất diệt." : "Phi thăng tiên đạo, trường sinh cùng thiên địa.",
                Stages = ["Sơ kỳ", "Trung kỳ", "Hậu kỳ"], Aura = realm == 8 ? "#ac80ff" : "#ffe9b0",
                Lotus = "#fff4dc", Sash = "#e6c46a", BagSlots = realm == 8 ? 700 : 800,
                EquipSlots = ["weapon", "offhand", "head", "body", "hands", "legs", "feet", "necklace", "charm", "ring"],
                // These realms grant progression and aura effects, not a new costume.
                Hero = "img/hero_hopthe.png", HeroStand = "img/hero_hopthe_stand.png",
                HeroMed = "img/med_hopthe.png", HeroCast = "img/hoa_cau_ref_hopthe.png", Halo = $"img/halo_0{realm}.png",
                QiReq = s => Math.Round((realm == 8 ? 13312000000000d : 212992000000000d) * Math.Pow(1.7, s)),
                Rate = s => realm == 8 ? 4500000 : 24000000,
                StageCost = s => new() { ["linh_thach"] = (realm == 8 ? 400000 : 1200000) * (s + 1), [pill] = s + 1 },
                BreakCost = realm == 8 ? new() { ["chan_tien_dan"] = 1, ["linh_thach"] = 4000000, ["ac_mong_cung_ky_vuong_hach"] = 1 } : null,
                BreakChance = realm == 8 ? .08 : 1, StatMult = realm == 8 ? 562500 : 2812500,
                Boss = new("Cùng Kỳ Vương", "img/boss_cung_ky_vuong.png", 1, 1, 1),
                BossItem = realm == 8 ? "ac_mong_cung_ky_vuong_hach" : "",
                Drops = new[] { new DropDef("linh_thach", 700, 1600, 1) }.Concat(
                    new[] { "kiem", "chau", "mu", "ao", "gang", "quan", "giay", "day", "phu", "nhan" }
                        .Select(s => new DropDef(prefix + s, 1, 1, .006))).ToArray(),
                Resources = ["linh_thach", pill, "hon_nguyen_dao_lien", "am_duong_ban_dao", "ac_mong_cung_ky_vuong_hach"]
            };
        }
    }
}

public static class FinalRealmSkills
{
    private static readonly Dictionary<string, string[]> Names = new() {
        ["hoa"]=["Thiên Kiếp Phần Thiên", "Cửu Kiếp Hỏa Thể", "Thái Sơ Tịnh Thế Diễm", "Bất Diệt Tiên Hỏa"],
        ["thuy"]=["Kiếp Hải Nộ Triều", "Huyền Thủy Độ Kiếp", "Thiên Hà Quy Nguyên", "Vô Lượng Tiên Thủy"],
        ["moc"]=["Kiến Mộc Phá Kiếp", "Vạn Kiếp Sinh Tức", "Thanh Đế Khai Thiên", "Trường Sinh Tiên Thể"],
        ["tho"]=["Kiếp Sơn Trấn Ngục", "Bất Động Kiếp Thân", "Hậu Thổ Định Càn Khôn", "Vô Cực Tiên Thổ"],
        ["loi"]=["Cửu Tiêu Thiên Kiếp", "Vạn Lôi Đạo Thể", "Thái Sơ Lôi Phạt", "Hỗn Nguyên Lôi Tâm"],
        ["bang"]=["Thiên Kiếp Hàn Ngục", "Băng Phách Độ Kiếp", "Vĩnh Hằng Tiên Băng", "Vô Trần Băng Tâm"],
        ["gio"]=["Cửu Kiếp Phong Bạo", "Tiêu Dao Kiếp Thể", "Thái Hư Tiên Trảm", "Vô Cực Phong Thần"]
    };

    private static string PassiveEffect(SkillDef previous) => "Mỗi cấp: " + string.Join(", ", new[] {
        ("công",previous.AtkPct+1), ("máu",previous.HpPct+1), ("sát thương kỹ năng",previous.SkillPct+1),
        ("phòng thủ",previous.DefPct), ("bạo kích",previous.Crit), ("thân pháp",previous.Speed),
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
                foreach (var realm in new[] { 8, 9 })
                {
                    var id = $"{group.Key}_{(realm == 8 ? "do_kiep" : "chan_tien")}_{tree}";
                    var active = tree == "active";
                    var next = previous with {
                        Id = id, Name = Names[group.Key][(realm-8)*2+(active?0:1)], MinRealm = realm, Row = realm + 1,
                        Cost = active ? realm + 4 : realm + 1, Req = [previous.Id + ":3"],
                        DmgBase = active ? (int)Math.Round(previous.DmgBase * 1.2) : 0,
                        DmgPerLv = active ? previous.DmgPerLv + 6 : 0,
                        AtkPct = active ? 0 : previous.AtkPct + 1, HpPct = active ? 0 : previous.HpPct + 1,
                        SkillPct = active ? 0 : previous.SkillPct + 1,
                        Code = $"{group.Key.ToUpperInvariant()}_{tree.ToUpperInvariant()}_REALM_{realm}",
                        Effect = active ? $"Tiên pháp cao cấp: sát thương {(int)Math.Round(previous.DmgBase * 1.2)}% công, thêm {previous.DmgPerLv + 6}% mỗi cấp."
                            : PassiveEffect(previous)
                    };
                    yield return next; previous = next;
                }
            }
    }
}
