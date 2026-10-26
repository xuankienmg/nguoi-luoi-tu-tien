namespace VanDao.Data;

public class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Đạo hữu";

    /// <summary>Chỉ số cảnh giới trong GameData.Realms.</summary>
    public int Realm { get; set; }

    /// <summary>Chỉ số tầng/kỳ trong cảnh giới hiện tại.</summary>
    public int Stage { get; set; }

    public double Qi { get; set; }

    /// <summary>Tỉ lệ cộng thêm khi đột phá cảnh giới (tăng sau mỗi lần thất bại).</summary>
    public double Bonus { get; set; }

    /// <summary>Tỉ lệ cộng thêm từ Phá Chướng Đan đã dùng cho lần đột phá đại cảnh giới kế tiếp (xóa khi đột phá thành công).</summary>
    public double PillBonus { get; set; }

    /// <summary>Trang phục đang mặc (GameData.Skins); rỗng = tự đổi theo cảnh giới.</summary>
    public string Skin { get; set; } = "";
    public string Gender { get; set; } = "male";
    /// <summary>Trang phục đã mở khóa bằng rương trang phục (SkinDef.UnlockItem), cách nhau dấu phẩy.</summary>
    public string SkinsUnlocked { get; set; } = "";

    /// <summary>Luôn true: nhân vật luôn tự đi thám hiểm. Giữ cột để tương thích DB cũ.</summary>
    public bool AutoExplore { get; set; } = true;

    public DateTime ExploreReadyAt { get; set; } = DateTime.UtcNow;
    public DateTime LastTickAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ---- Bản đồ 10 chương: chương + ải đang đánh (1..15, 16 = Boss); chương cao nhất đã mở và ải cao nhất của chương đó ----
    public int Area { get; set; } = 1;
    public int AreaMax { get; set; } = 1;
    public int Chapter { get; set; }
    public int ChapterMax { get; set; }
    /// <summary>Phiên bản dữ liệu bản đồ: 0 = bản đồ theo cảnh giới (bản cũ) → chuyển về 1-1 khi nạp; 1 = bản đồ chương.</summary>
    public int MapVer { get; set; } = 1;
    /// <summary>Mức độ khó đang đánh: 0 Thường · 1 Khó · 2 Ác mộng.</summary>
    public int Diff { get; set; }
    /// <summary>Ải cao nhất đã mở ở mức Khó / Ác mộng theo chương: "d.c=ải;…" (mức Thường dùng ChapterMax/AreaMax).</summary>
    public string DiffProg { get; set; } = "";
    /// <summary>Boss đã hạ theo mức và chương: "d.c;…". Hạ Boss chương N ở mức d mở chương N ở mức d+1.</summary>
    public string Cleared { get; set; } = "";
    /// <summary>Bản cũ (bản đồ theo cảnh giới), không còn dùng. Giữ cột để tương thích DB cũ.</summary>
    public int MapRealm { get; set; } = -1;

    // ---- Đột phá đại cảnh giới: thất bại phải điều tức (chờ), tẩu hỏa nhập ma bị suy yếu tạm thời ----
    public DateTime BreakCooldownUntil { get; set; } = DateTime.MinValue;
    public DateTime DebuffUntil { get; set; } = DateTime.MinValue;

    // ---- Công pháp: cấp từng kỹ năng ("id:cấp;id:cấp") và số lần hạ Boss (cho điểm kỹ năng) ----
    public string Skills { get; set; } = "";
    public string LearnedManuals { get; set; } = "";
    public int ManualVersion { get; set; }
    /// <summary>Công pháp đang kích hoạt (hệ): quyết định đòn đánh thường; bị động hệ khác chỉ phát huy CultivationManuals.InactiveRate. Rỗng = tự chọn hệ cấp cao nhất.</summary>
    public string ActiveManual { get; set; } = "";
    public int BossKills { get; set; }
    /// <summary>Điểm công pháp vĩnh viễn nhận từ Ngộ Đạo Đan.</summary>
    public int ElixirPoints { get; set; }

    /// <summary>Ô kỹ năng: 3 ô chiêu thức + 1 ô tuyệt kỹ, "id,id,,id" (trống = chưa đặt).</summary>
    public string SkillSlots { get; set; } = "";

    /// <summary>Admin mode: cờ gian lận đang bật (god · break · fast), cách nhau dấu phẩy; điểm kỹ năng admin cộng thêm.</summary>
    public string Admin { get; set; } = "";
    public int AdminPoints { get; set; }

    /// <summary>Code quà tặng đã nhập (GameData.GiftCodes), cách nhau dấu phẩy. Mỗi code dùng 1 lần mỗi nhân vật; tu lại từ đầu thì xóa.</summary>
    public string Redeemed { get; set; } = "";

    /// <summary>Lịch sử quay Tài Xỉu, mới nhất trước: "unixGiây|id,id,…;…" (giữ GameData.SpinHistoryKeep lượt bấm quay).</summary>
    public string SpinHistory { get; set; } = "";
    /// <summary>Ngày quay theo UTC+7 và tổng lượt trang bị + linh thú đã dùng trong ngày.</summary>
    public string SpinDay { get; set; } = "";
    public int SpinDailyUsed { get; set; }
    public string PetSpinDay { get; set; } = "";
    public int PetSpinDailyUsed { get; set; }
    /// <summary>Lượt thường đã quay trong chu kỳ bảo hiểm riêng từng vòng (0..100), không đặt lại theo ngày.</summary>
    public int SpinPityUsed { get; set; }
    public int PetSpinPityUsed { get; set; }

    // ---- Bảng xếp hạng online ----
    /// <summary>Khóa bí mật ngẫu nhiên của nhân vật trên bảng xếp hạng (server chỉ giữ SHA-256). Giữ nguyên khi tu lại từ đầu.</summary>
    /// <summary>Sự kiện quay tích lũy: khóa đợt (yyyy-MM-dd:gear|pet), điểm, các mốc đã nhận (chỉ số, phẩy).</summary>
    public string EventKey { get; set; } = "";
    public int EventPoints { get; set; }
    public string EventClaimed { get; set; } = "";
    public string LbSecret { get; set; } = "";
    /// <summary>Đã từng dùng admin mode → không được lên bảng xếp hạng cho tới khi tu lại từ đầu (không bật cờ admin).</summary>
    public bool Tainted { get; set; }
    /// <summary>File dữ liệu từng bị bản game cũ (không có SaveGuard) ghi vào: vẫn lên bảng xếp hạng nhưng gắn dấu * đỏ,
    /// không vào top 10, không giao dịch ở Chợ. Không tự gỡ (gỡ tay trong DB hoặc tu lại từ đầu).</summary>
    public bool Flagged { get; set; }
    /// <summary>Admin mode chỉ có ở môi trường dev (Program đặt theo ASPNETCORE_ENVIRONMENT). Bản phát hành: cờ đã lưu bị bỏ qua, không xóa.</summary>
    public static bool AdminEnabled { get; set; } = true;
    public bool Cheat(string flag) => AdminEnabled && Admin.Split(',', StringSplitOptions.RemoveEmptyEntries).Contains(flag);

    /// <summary>2 ô: [0] đòn đánh thường · [1] kỹ năng chủ động (tung khi đầy nộ). Dữ liệu 4 ô của bản cũ được xếp lại theo loại.</summary>
    public string[] Slots()
    {
        var a = SkillSlots.Split(',').Select(x => x.Trim()).ToArray();
        var n = VanDao.Game.SkillData.SkillSlotCount;
        string Fit(string id, int i) => VanDao.Game.SkillData.ById.TryGetValue(id, out var s) && VanDao.Game.SkillData.FitsSlot(s, i) ? id : "";
        if (a.Length == n) return Enumerable.Range(0, n).Select(i => Fit(a[i], i)).ToArray();
        return Enumerable.Range(0, n).Select(i => a.Select(x => Fit(x, i)).FirstOrDefault(x => x.Length > 0) ?? "").ToArray();
    }

    public Dictionary<string, int> SkillLevels() =>
        Skills.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split(':'))
            .Where(x => x.Length == 2 && int.TryParse(x[1], out _)).ToDictionary(x => x[0], x => int.Parse(x[1]));

    public void SetSkillLevels(Dictionary<string, int> d) => Skills = string.Join(';', d.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}:{kv.Value}"));

    // ---- Luyện đan (bản cũ, không còn dùng; giữ cột cho DB cũ) ----
    /// <summary>Tổng kinh nghiệm luyện đan (cấp đan sư tính từ đây).</summary>
    public int AlchemyExp { get; set; }
    /// <summary>Đan phương đang luyện, -1 = lò nghỉ.</summary>
    public int RefineRecipe { get; set; } = -1;
    public DateTime RefineStartAt { get; set; } = DateTime.UtcNow;
    public DateTime RefineEndAt { get; set; } = DateTime.UtcNow;
    /// <summary>Số lò còn lại tính cả lò đang luyện; -1 = tự động luyện đến khi hết nguyên liệu.</summary>
    public int RefineLeft { get; set; }
    /// <summary>Lò hiện tại có bỏ dẫn dược hay không (đã trừ khi nhóm lò).</summary>
    public bool RefineCatalyst { get; set; }
    /// <summary>Kết quả lò gần nhất: "success|item|n", "crit|item|n" hoặc "fail|item|0".</summary>
    public string LastRefine { get; set; } = "";
    public DateTime LastRefineAt { get; set; } = DateTime.MinValue;

    /// <summary>Chỉ số vĩnh viễn từ Luyện Thể Đan.</summary>
    public int BodyHp { get; set; }
    public int BodyDef { get; set; }

    public string Hatch { get; set; } = "";
    public List<Pet> Pets { get; set; } = new();
    public List<InventoryItem> Inventory { get; set; } = new();
    public List<LogEntry> Logs { get; set; } = new();
    public List<EquippedItem> Equipment { get; set; } = new();

    public int Count(string itemId) => Inventory.FirstOrDefault(i => i.ItemId == itemId)?.Count ?? 0;

    public void Add(string itemId, int amount)
    {
        var slot = Inventory.FirstOrDefault(i => i.ItemId == itemId);
        if (slot is null)
        {
            slot = new InventoryItem { PlayerId = Id, ItemId = itemId, Count = 0 };
            Inventory.Add(slot);
        }
        slot.Count += amount;
    }

    public bool Has(IReadOnlyDictionary<string, int> cost) => cost.All(kv => Count(kv.Key) >= kv.Value);

    public void Pay(IReadOnlyDictionary<string, int> cost)
    {
        foreach (var (item, amount) in cost) Add(item, -amount);
    }
}

public class InventoryItem
{
    public Guid PlayerId { get; set; }
    public string ItemId { get; set; } = "";
    public int Count { get; set; }
}

public class LogEntry
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string Text { get; set; } = "";

    /// <summary>"", "good", "bad" hoặc "gold" — quyết định màu trong nhật ký.</summary>
    public string Kind { get; set; } = "";
}

/// <summary>Trang bị đang mặc, mỗi ô (Slot) một món.</summary>
public class EquippedItem
{
    public Guid PlayerId { get; set; }
    public string Slot { get; set; } = "";
    public string ItemId { get; set; } = "";
}
public class Pet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public string DefId { get; set; } = "";
    public int Level { get; set; } = 1;
    public int Exp { get; set; }
    public int Form { get; set; }
    public int AppearanceForm { get; set; } = -1;
    /// <summary>Phẩm của riêng con này (0 Phàm … 4 Thiên); -1 = theo phẩm của loài. Thú Đản Phàm nở ra loài Linh nhưng mang phẩm Phàm.</summary>
    public int Quality { get; set; } = -1;
    /// <summary>Số sao (0–5) từ ghép linh thú cùng loài; mỗi sao cộng PetData.StarBonus chỉ số.</summary>
    public int Stars { get; set; }
    /// <summary>Điểm ghép tích lũy (mỗi con hiến = 1 + điểm của nó); số sao suy ra từ PetData.StarCost.</summary>
    public int StarPts { get; set; }
    public int AptAtk { get; set; } = 100;
    public int AptDef { get; set; } = 100;
    public int AptHp { get; set; } = 100;
    public int Slot { get; set; }
    public int FoodSpent { get; set; }
    /// <summary>Thức tỉnh huyết mạch (0–3) sau 5★: mỗi cấp +10% chỉ số.</summary>
    public int Awaken { get; set; }
    /// <summary>Trang bị linh thú đang mặc (id vật phẩm, rỗng = trống): Hạng Khuyên · Linh Giáp · Hộ Mệnh Phù.</summary>
    public string GearCollar { get; set; } = "";
    public string GearArmor { get; set; } = "";
    public string GearCharm { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
