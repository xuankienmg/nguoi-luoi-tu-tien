namespace VanDao.Data;

public class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Supabase Auth owner. Offline characters have no online owner.</summary>
    public Guid? OwnerId { get; set; }
    /// <summary>Online (PostgreSQL): cột hệ thống xmin, concurrency token để cây nhân vật trong cache RAM không ghi đè dữ liệu
    /// mới hơn do nơi khác ghi. Bản offline (SQLite) bỏ qua thuộc tính này.</summary>
    public uint Version { get; set; }
    public DateTime LastChannelAt { get; set; } = DateTime.MinValue;
    public string Name { get; set; } = "Đạo hữu";
    // Old characters keep their existing starter gifts; CreateAsync explicitly sets false.
    public bool StarterChosen { get; set; } = true;

    /// <summary>Chỉ số cảnh giới trong GameData.Realms.</summary>
    public int Realm { get; set; }

    /// <summary>Chỉ số tầng/kỳ trong cảnh giới hiện tại.</summary>
    public int Stage { get; set; }

    public decimal Qi { get; set; }

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
    /// <summary>Số bí kíp đã lĩnh ngộ của hệ chưa mở ("hệ:số,hệ:số"); đủ CultivationManuals.Required thì mở cây kỹ năng.</summary>
    public string ManualProgress { get; set; } = "";
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
    /// <summary>Quà đăng nhập hằng ngày: ngày đã nhận gần nhất (UTC+7, yyyy-MM-dd) và ô kế tiếp trong vòng 7 ngày (0..6).</summary>
    public string LoginDay { get; set; } = "";
    public int LoginStep { get; set; }
    /// <summary>Tạp Hóa bà Dĩm: ngày (UTC+7) và số món hàng ngày đã mua trong ngày đó, "món:số,món:số".</summary>
    public string ShopDay { get; set; } = "";
    public string ShopBought { get; set; } = "";
    /// <summary>Lượt đã quay trong chu kỳ bảo hiểm riêng từng vòng (0..99); lượt thứ 100 nhận bảo hiểm rồi reset. Giá trị 100 của save cũ giữ lượt bảo hiểm chờ nhận.</summary>
    public int SpinPityUsed { get; set; }
    public int PetSpinPityUsed { get; set; }

    // ---- Bảng xếp hạng online ----
    /// <summary>Khóa bí mật ngẫu nhiên của nhân vật trên bảng xếp hạng (server chỉ giữ SHA-256). Giữ nguyên khi tu lại từ đầu.</summary>
    /// <summary>Sự kiện quay tích lũy: khóa đợt (yyyy-MM-dd:gear|pet), điểm, các mốc đã nhận (chỉ số, phẩy).</summary>
    public string EventKey { get; set; } = "";
    public int EventPoints { get; set; }
    public string EventClaimed { get; set; } = "";
    /// <summary>Kỳ duyên Thiên Thư: khóa khung giờ đã xử lý (yyyy-MM-dd:HH), số trang từng quyển (10 số, phẩy), Đạo Tâm (+ Chính / − Tà, −99…99).</summary>
    public string KyDuyenKey { get; set; } = "";
    public string ThienThu { get; set; } = "";
    public int DaoTam { get; set; }
    /// <summary>Gói Tốc Hành: đã mở thám hiểm ×2 / người chơi đang bật.</summary>
    public bool Speed2 { get; set; }
    public bool Speed2On { get; set; }
    public string LbSecret { get; set; } = "";
    /// <summary>Đã từng dùng admin mode → không được lên bảng xếp hạng cho tới khi tu lại từ đầu (không bật cờ admin).</summary>
    public bool Tainted { get; set; }
    /// <summary>File dữ liệu từng bị bản game cũ (không có SaveGuard) ghi vào: vẫn lên bảng xếp hạng nhưng gắn dấu * đỏ,
    /// không vào top 10, không giao dịch ở Chợ. Không tự gỡ (gỡ tay trong DB hoặc tu lại từ đầu).</summary>
    public bool Flagged { get; set; }
    /// <summary>Admin mode chỉ có ở môi trường dev (Program đặt theo ASPNETCORE_ENVIRONMENT). Bản phát hành: cờ đã lưu bị bỏ qua, không xóa.
    /// Online Server: mặc định tắt, OnlineBoundary bật riêng cho request của tài khoản trong Online:AdminUsers.</summary>
    public static bool AdminEnabled { get => adminRequest.Value ?? adminDefault; set => adminDefault = value; }
    private static bool adminDefault = true;
    private static readonly AsyncLocal<bool?> adminRequest = new();
    /// <summary>Đặt admin mode cho request hiện tại (luồng async này), không đổi giá trị chung.</summary>
    public static void SetRequestAdmin(bool enabled) => adminRequest.Value = enabled;
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
    /// <summary>Cột cũ của lò đan, nay lưu hiệu lực đan hỗ trợ (SupportPills): "loại:phẩm:hạn" ngăn bởi ';'.</summary>
    public string LastRefine { get; set; } = "";
    public DateTime LastRefineAt { get; set; } = DateTime.MinValue;

    /// <summary>Chỉ số vĩnh viễn từ Luyện Thể Đan.</summary>
    public decimal BodyHp { get; set; }
    public decimal BodyDef { get; set; }

    public string Hatch { get; set; } = "";
    /// <summary>Kho chứa Linh Tinh riêng biệt, không chiếm ô túi đồ.</summary>
    public string GemVault { get; set; } = "";
    /// <summary>Khảm nạm theo 10 ô trang bị nhân vật.</summary>
    public string GemSlots { get; set; } = "";
    public List<Pet> Pets { get; set; } = new();
    public List<InventoryItem> Inventory { get; set; } = new();
    public List<LogEntry> Logs { get; set; } = new();
    public List<EquippedItem> Equipment { get; set; } = new();

    public decimal Count(string itemId) =>
        VanDao.Game.GemData.IsGemOrStone(itemId)
            ? VanDao.Game.GemData.GetCount(this, itemId)
            : (Inventory.FirstOrDefault(i => i.ItemId == itemId)?.Count ?? 0);

    /// <summary>Số lượng chặn ở int.MaxValue, cho vòng lặp / số lượt (trang bị, đan, vé…), không dùng cho tổng linh thạch.</summary>
    public int CountInt(string itemId) => (int)Math.Min(Count(itemId), int.MaxValue);

    public void Add(string itemId, decimal amount, int equipmentCopy = 0)
    {
        if (VanDao.Game.GemData.IsGemOrStone(itemId))
        {
            VanDao.Game.GemData.Add(this, itemId, (int)Math.Clamp(amount, int.MinValue, int.MaxValue));   // Linh Tinh trong kho riêng: số nhỏ
            return;
        }
        var slot = Inventory.FirstOrDefault(i => i.ItemId == itemId);
        if (slot is null)
        {
            slot = new InventoryItem { PlayerId = Id, ItemId = itemId, Count = 0 };
            Inventory.Add(slot);
        }
        if (amount < 0) VanDao.Game.GemData.RemoveBagCopies(this, itemId, (int)Math.Min(-amount, int.MaxValue), equipmentCopy);
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
    /// <summary>decimal: linh thạch late-game vượt giới hạn int (2.147.483.647).</summary>
    public decimal Count { get; set; }
}

public class LogEntry
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;

    /// <summary>Độ dài tối đa của Text (cột varchar trên PostgreSQL online; SQLite offline không kiểm tra).</summary>
    public const int TextMax = 500;
    public string Text { get; set; } = "";

    /// <summary>Cắt dòng nhật ký về TextMax ký tự (thêm "…"), không tách đôi ký tự UTF-16.</summary>
    public static string Fit(string text)
    {
        if (text.Length <= TextMax) return text;
        var cut = TextMax - 1;
        if (char.IsHighSurrogate(text[cut - 1])) cut--;
        return text[..cut] + "…";
    }

    /// <summary>"", "good", "bad" hoặc "gold" — quyết định màu trong nhật ký.</summary>
    public string Kind { get; set; } = "";
}

/// <summary>Ảnh chụp bảng xếp hạng của một nhân vật online (chế độ Server), ghi cùng transaction lúc lưu nhân vật.
/// Offline không dùng bảng này: bảng xếp hạng offline nằm trên Supabase (LeaderboardService).</summary>
public class LeaderboardEntry
{
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = "";
    public int Realm { get; set; }
    public int Stage { get; set; }
    public int Level { get; set; }
    public decimal Qi { get; set; }
    public string RealmName { get; set; } = "";
    public string StageName { get; set; } = "";
    public decimal Power { get; set; }
    public long Progress { get; set; }
    public decimal PetPower { get; set; }
    /// <summary>JSON ô → mã vật phẩm.</summary>
    public string Equipment { get; set; } = "{}";
    /// <summary>JSON chỉ số, cùng khóa với stats của submit_score.</summary>
    public string Stats { get; set; } = "{}";
    /// <summary>Đã gắn thì giữ (giống cột flagged trên Supabase).</summary>
    public bool Flagged { get; set; }
    /// <summary>LeaderboardSnapshot.Key lần ghi trước: không đổi thì chỉ cập nhật tu vi theo chu kỳ.</summary>
    public string Key { get; set; } = "";
    /// <summary>JSON PvpFighter: kỹ năng / hiệu ứng / trang phục người khác đánh với ở Lôi đài. Chỉ chủ nhân vật ghi dòng này.</summary>
    public string? Loadout { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Vị trí trên bậc thang Lôi đài online (chế độ Server); Pos nhỏ = hạng cao. Chỉ ghi dưới khóa Lôi đài (ServerArena).</summary>
public class ArenaRank
{
    public Guid PlayerId { get; set; }
    public long Pos { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Một trận Lôi đài online: lượt trong ngày, thời gian nghỉ và lịch sử đều đếm từ đây.</summary>
public class ArenaFight
{
    public long Id { get; set; }
    public Guid AttackerId { get; set; }
    public Guid DefenderId { get; set; }
    public string AttackerName { get; set; } = "";
    public string DefenderName { get; set; } = "";
    /// <summary>Bên khiêu chiến thắng.</summary>
    public bool Win { get; set; }
    /// <summary>Hạng hiển thị hai bên lúc vào trận.</summary>
    public long AttackerRank { get; set; }
    public long DefenderRank { get; set; }
    public bool Swapped { get; set; }
    public long Seed { get; set; }
    /// <summary>pending (đã cấp seed, cùng request) · done</summary>
    public string Status { get; set; } = "pending";
    /// <summary>Tỷ thí với người xếp dưới: không đổi hạng, không tính thắng / thua.</summary>
    public bool Practice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Tin rao Chợ online (chế độ Server), giữ đồ ký gửi. Offline dùng market_listings trên Supabase (MarketService).
/// Nội dung tin (món, số lượng, giá, người bán) không đổi sau khi treo; tin đã đóng chỉ còn SellerClaimed đổi được.</summary>
public class MarketListing
{
    public long Id { get; set; }
    public Guid SellerId { get; set; }
    public string SellerName { get; set; } = "";
    public string ItemId { get; set; } = "";
    public int Qty { get; set; }
    /// <summary>Tổng giá cả chồng.</summary>
    public long Price { get; set; }
    /// <summary>Tiền tệ của Price / Payout: linh_thach hoặc thuong_pham_linh_thach (Cực phẩm linh thạch).</summary>
    public string Currency { get; set; } = "linh_thach";
    /// <summary>open · sold · cancelled · expired</summary>
    public string Status { get; set; } = "open";
    public Guid? BuyerId { get; set; }
    public string? BuyerName { get; set; }
    /// <summary>Người bán thực nhận (giá − thuế).</summary>
    public long Payout { get; set; }
    public bool SellerClaimed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ClosedAt { get; set; }
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

/// <summary>Code donate dùng một lần (chế độ Server, schema game): admin tạo theo số tiền, người chơi online hoặc offline nhập để nhận Cực Phẩm.</summary>
public class DonateCode
{
    public string Code { get; set; } = "";
    public int Amount { get; set; }
    public long Vnd { get; set; }
    public string Note { get; set; } = "";
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAt { get; set; }
    public Guid? UsedByPlayer { get; set; }
    public Guid? UsedByOwner { get; set; }
    public bool Offline { get; set; }
}
