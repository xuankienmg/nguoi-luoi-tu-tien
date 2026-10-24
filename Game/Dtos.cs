namespace VanDao.Game;

// ---- Dữ liệu tĩnh cho giao diện ----
public record GameDataDto(
    IEnumerable<ItemDef> Items,
    IEnumerable<SlotDef> Slots,
    IEnumerable<StatDef> ExtraStats,
    IEnumerable<RealmInfoDto> Realms,
    IEnumerable<SynthDef> Synths,
    IEnumerable<ElementDef> Elements,
    IEnumerable<SkillDef> SkillTree,
    string[] PillChain,
    int Areas,
    string[] LockedRealms,
    double[]? FuseChance = null,
    IEnumerable<ChapterInfoDto>? Chapters = null,
    IEnumerable<BrewDef>? Brews = null,
    IEnumerable<HerbDropDef>? HerbDrops = null,
    IEnumerable<SkinDef>? Skins = null,
    double[]? SpinWeights = null,
    int SpinPrice = 0,
    IEnumerable<EquipmentSetDef>? EquipmentSets = null, IEnumerable<PetDef>? PetDefs = null, double[]? PetSpinWeights = null, int PetSpinPrice = 0);

public record RealmInfoDto(int Index, string Id, string Name, string Desc, string[] Stages,
    string Aura, string Lotus, string Sash, string Hero, string[] Resources,
    string Cn, int BagSlots, string[] EquipSlots, string Boss, string BossImage, string BossLair, string KeyItem, string BossItem,
    string? HeroStand = null, string? HeroMed = null, string? HeroCast = null, string? Halo = null);

// ---- Trạng thái người chơi ----
public record CostDto(string Item, int Need, int Have);

/// <summary>Bản đồ cảnh giới hiện tại: ải đang đánh, ải đã mở, dự đoán thắng/thua từng ải, chìa khóa và yêu hạch Boss.</summary>
public record MapDto(int Area, int AreaMax, string KeyItem, string BossItem, string BossLair, double KeyChanceMin, double KeyChanceMax,
    List<AreaDto> Areas, List<SynthStateDto> Synths, bool HasBossItem, int Realm = 0,
    List<DropInfoDto>? Drops = null, double ChestChance = 0, double RareChestChance = 0, int RareChestMinArea = 0, string BossChest = "",
    int ChapterMax = 0, string Boss = "", string BossImage = "", double[]? Quality = null,
    int Diff = 0, bool[]? DiffOpen = null, bool[]? ChapterOpen = null, bool[]? ChapterCleared = null, double RewardMult = 1, double MonMult = 1);
// Diff = mức độ khó đang đánh (0 Thường · 1 Khó · 2 Ác mộng); DiffOpen = mức đã mở; ChapterOpen / ChapterCleared theo mức đang chọn
// Realm = chương đang đánh (0-based, tên giữ cho tương thích client: nhãn ải "C-x")

/// <summary>Thông tin tĩnh một chương cho thanh chọn chương.</summary>
public record ChapterInfoDto(int Index, string Name, string Realm, bool Enabled, string Boss, string BossImage, string BossLair, string KeyItem, string? BossItem,
    string? HardBossItem = null, string? NightmareBossItem = null);

/// <summary>Bảng rơi của bản đồ (tỉ lệ gốc ở ải 1; client tự nhân theo ải giống server).</summary>
public record DropInfoDto(string Item, int Min, int Max, double Chance, bool Gear, int MinArea = 1);

public record AreaDto(int Index, string Monster, string Place, int Hp, int Atk, int Def, bool Win, int Hits, bool Open, string Image = "",
    List<WaveInfoDto>? Waves = null, bool Elite = false, List<DropInfoDto>? Drops = null);

/// <summary>Một đợt quái của ải (bảng thông tin ải trên bản đồ).</summary>
public record WaveInfoDto(string Name, string Image, string Tier, int Hp, int Atk, int Def, int StoneMin = 0, int StoneMax = 0);

public record SynthStateDto(string From, int Max);

/// <summary>Đột phá: tỉ lệ gốc, thời gian điều tức sau thất bại, thời gian suy yếu (tẩu hỏa nhập ma), thay đổi khi thành công.</summary>
public record BreakStateDto(double BaseChance, long CooldownMs, long DebuffMs, BreakPreviewDto Preview);

public record BreakPreviewDto(int Hp, int Atk, int Def, double RatePct, int BagSlots, string[] NewSlots, string[] NewStats, int SkillPoints = 0);

public record LogDto(DateTime At, string Text, string Kind);

public record StateDto(
    Guid Id,
    string Name,
    int Realm,
    int Stage,
    string RealmName,
    string StageName,
    double Qi,
    double QiReq,
    double Rate,
    double ChannelGain,
    bool IsPeak,
    bool HasNext,
    string TargetTitle,
    string TargetKind,
    List<CostDto> TargetCost,
    double Chance,
    double Bonus,
    long ExploreReadyInMs,
    bool AutoExplore,
    long ExploreCooldownMs,
    double ExploreQiGain,
    Dictionary<string, string> Equipment,
    BonusDto Bonuses,
    CombatStats Stats,
    BattleDto Battle,
    string RealmCn,
    string NextRealmName,
    string NextRealmCn,
    bool AtTop,
    int BagUsed,
    int BagCap,
    string[] EquipSlots,
    int Level,
    Dictionary<string, int> Inventory,
    MapDto Map,
    Dictionary<string, int> Skills,
    string[] SkillSlots,
    int SkillPoints,
    int SkillPointsTotal,
    BreakStateDto Break,
    List<LogDto> Logs,
    string Admin = "",    // cờ admin mode đang bật
    List<ChestOddsDto>? ChestOdds = null,   // tỉ lệ phần thưởng khi mở rương (hiện ở chế độ admin)
    double PillBonus = 0,    // tỉ lệ đột phá cộng thêm từ Phá Chướng Đan
    string Skin = "",
    string SpinHistory = "",
    EquipmentEffectsState? GearEffects = null,
    StatSourceDto[]? StatSources = null,
    EffectState[]? CombatLimits = null, PetState? Pet = null, SpinDailyDto? SpinDaily = null, SpinDailyDto? PetSpinDaily = null, string[]? UnlockedElements = null, string? ActiveManual = null,
    string[]? SkinsUnlocked = null, string Gender = "male", SpinEventDto? Event = null,
    ArenaReplayDto? Arena = null);   // trận Lôi đài vừa đánh: màn hình chính diễn lại một lần
/// <summary>Sự kiện quay tích lũy Thiên Cơ (track: gear · pet). Online = có giờ máy chủ, mới cộng điểm và nhận quà được.</summary>
public record SpinEventDto(bool Active, bool Online, string Key, string Track, string Name, long StartsAt, long EndsAt, int Points,
    List<SpinEventMilestoneDto> Milestones, string? Blocked, string NextTrack, long NextStartsAt);
public record SpinEventMilestoneDto(int Points, List<ItemQtyDto> Rewards, bool Claimed, bool Ready);   // Trang phục và giới tính chỉ đổi ngoại hình.

public record SpinDailyDto(int Limit, int Used, int Remaining, long ResetAt);

public record StatSourceDto(string Name, double Atk = 0, double Def = 0, double Hp = 0,
    double AtkPct = 0, double DefPct = 0, double HpPct = 0, double Crit = 0, double Speed = 0, double Qi = 0);

/// <summary>Tỉ lệ (%) phần thưởng 1 lần mở rương với nhân vật hiện tại: Level = cấp đồ ra, Quality = % cấp Phàm → Thiên,
/// Treasure / Gear = % từng món gốc (pháp bảo / trang bị), Pill = % ra đan (PillItem, PillMin–PillMax viên).</summary>
public record ChestOddsDto(string Chest, int Level, double[] Quality, List<ItemQtyPctDto> Treasure, List<ItemQtyPctDto> Gear,
    string PillItem, int PillMin, int PillMax, double Pill,
    List<ItemQtyPctDto>? Herbs = null, int HerbMin = 1, int HerbMax = 1, double Herb = 0, double Enlightenment = 0, List<ItemQtyPctDto>? Pets = null, List<ItemQtyPctDto>? Manuals = null);
public record ItemQtyPctDto(string Item, double Pct);

public record BonusDto(double QiPct, double SpeedPct);

/// <summary>
/// Kết quả trận đánh của chuyến thám hiểm (tính sẵn với hạt giống cố định theo chuyến)
/// để hoạt ảnh ở client khớp đúng với server.
/// </summary>
public record BattleDto(string Monster, string Image, int MonsterHp, int MonsterAtk, int MonsterDef,
    int HeroDmg, int MonsterDmg, int Hits, bool Win,
    int[] MonHpLeft, int[] HeroHpLeft, bool[] MonSkip, List<CastDto> Casts, int HeroHp,
    string Basic = "hoa_cau",    // chiêu dùng làm đòn đánh thường (để client chọn animation)
    int[]? Rage = null,          // nộ cuối mỗi lượt, 0–100 (lượt tung kỹ năng: về 0)
    int Burn = 0, double BurnPct = 0,    // thiêu đốt (Phần Cốt): sát thương mỗi lần kích hoạt · % công
    bool[]? Burned = null,                // lượt nào thiêu đốt kích hoạt (30%)
    // hiệu ứng trang bị theo cấp (GameService.GearEffects), mảng theo lượt như Burned:
    bool[]? Bolt = null, int BoltDmg = 0,  // Thiên lôi: lượt nào giáng sét · sát thương mỗi tia
    bool[]? Extra = null,                  // Liên kích: lượt nào đánh thêm 1 đòn thường (HeroDmg)
    int[]? Heal = null,                    // Hồi khí: máu hồi cuối lượt
    int[]? Reflect = null,                 // Địa mạch: sát thương phản lại quái
    int Guard = 0, int[]? GuardLeft = null,   // Thổ thuẫn: khiên đầu trận · khiên còn lại cuối mỗi lượt
    int FirstHit = 0,                      // Địa chấn: sát thương thêm ở lượt đầu
    int EnrageTurn = -1, int MonsterDmgEnraged = 0,   // Boss Cuồng bạo: lượt (đã lấy mẫu) bắt đầu nổi giận · sát thương mỗi đòn khi đó
    List<WaveDto>? Waves = null,
    int[]? BasicDamage = null, bool[]? BasicCritical = null,
    int[]? ExtraDamage = null, bool[]? ExtraCritical = null,
    bool[]? Dodged = null, double DodgeChance = 0,
    int[]? HeroHpBefore = null, int[]? GuardBefore = null, int[]? BurnDamage = null, DivineBattleDto? Divine = null, PetBattleDto? Pet = null,
    string[]? MonControl = null,           // loại khống chặn lượt phản công: "paralyze" · "stun" · "freeze" · "" (mảng theo lượt)
    bool[]? Shatter = null,                // lượt quái đóng băng từ lượt trước nhận thêm sát thương
    bool[]? Weakened = null, bool[]? WindGuard = null, int[]? CounterDamage = null); // trạng thái và sức phản công theo lượt

public record DivineTurnDto(bool Nullified, bool Saved, bool Boosted, bool Charged);
public record DivineBattleDto(int Tier, string Name, double ExtraChance, DivineTurnDto[] Turns);

/// <summary>Một đợt quái trong trận: chỉ số đã tính công pháp/trang bị, sát thương hai bên, lượt bắt đầu (đã lấy mẫu).</summary>
/// <summary>Stones = linh thạch rơi khi hạ quái đợt này (đã gieo sẵn theo chuyến, server cộng đúng số này).</summary>
public record WaveDto(string Name, string Image, string Tier, int Hp, int Atk, int Def, int HeroDmg, int MonsterDmg, int Start, int Stones = 0);

/// <summary>Kỹ năng tự thi triển ở lượt Turn (0-based) của hoạt ảnh trận.</summary>
public record CastDto(int Turn, string Skill, string Name, string Anim, int Dmg, bool Critical = false, string Secondary = "");

/// <summary>Kết quả một lần luyện chế/hợp thành: thành phẩm và nguyên liệu đã tiêu hao (dùng chung cho mọi công thức).</summary>
public record ItemQtyDto(string Item, int Qty);
public record CraftResultDto(string Recipe, ItemQtyDto Output, List<ItemQtyDto> Inputs, List<ItemQtyDto>? Outputs = null);

public record ActionResponse(StateDto State, string? Message = null, string? Kind = null, CraftResultDto? Craft = null);

public record CreatePlayerRequest(string? Name);

public class GameException(string message) : Exception(message);

public record PetState(List<PetDto> Owned, List<HatchDto> Hatch, int Capacity, int LevelCap, int GuardSlots, int HatchSlots, int[]? StarCost = null, PetStarRecipe[]? StarRecipes = null, PetTrainingRules? Training = null);
public record PetDto(Guid Id, string DefId, string Name, string Element, string Rarity, int Level, int Exp, int ExpNeed,
    int Form, string FormName, int Slot, int Atk, int Def, int Hp, int AptAtk, int AptDef, int AptHp,
    int ShareAtk, int ShareDef, int ShareHp, string TalentText, string SkillText, int SkillEvery,
    CostDto[] EvolveCost, bool CanEvolve, string Image, int AppearanceForm = -1, string EvolveHint = "", int Stars = 0, int ReleaseValue = 0, int StarPts = 0, string NextStarSkillText = "", string NextStarTalentText = "", PetMiracleDef? Miracle = null, PetTrainingDto? Training = null);
/// <summary>Bồi dưỡng của một con: khoảng tư chất, thức tỉnh, trang bị đang mặc, cộng hưởng ngũ hành.</summary>
public record PetTrainingDto(int AptMin, int AptMax, int RerollStones, int Awaken, string AwakenName, CostDto[] AwakenCost, int AwakenDonorStars, bool AwakenGate, string AwakenHint,
    Dictionary<string, string> Gear, Dictionary<string, double> GearPct, double Resonance, string ResonanceKind, string ManualElement);
public record PetGearSlotDto(string Id, string Name, string Stat, string StatName);
public record PetGearInfoDto(string Id, string Slot, int Tier, double Pct);
/// <summary>Luật bồi dưỡng linh thú (hằng số), gửi kèm trạng thái để giao diện không tự đoán.</summary>
public record PetTrainingRules(PetGearSlotDto[] Slots, string[] TierNames, PetGearInfoDto[] Gear, int FuseCount, int[] FuseStones, int MaxLocks,
    int MaxAwaken, double AwakenBonus, double ResonanceSame, double ResonanceSupport, Dictionary<string, string>? Sources = null);   // Sources: cách nhận đan bồi dưỡng (tooltip)
public record HatchDto(int Slot, string Egg, DateTime ReadyAt, int ReadyInSeconds, bool Ready, int InstantCost);
/// <param name="ParalyzeTurns">Số lượt quái bị khống bởi lần thi triển này (mọi loại khống); ControlKind = "paralyze" · "stun" · "freeze".</param>
public record PetCastDto(int Turn, int Dmg, int Heal, int Guard, string Skill, int[]? Hits = null, string Kind = "", int ParalyzeTurns = 0, bool IsMiracle = false, double At = .45, string ControlKind = "");
public record PetBattleDto(Guid Id, string Name, string Image, string Element, PetCastDto[] Casts);
