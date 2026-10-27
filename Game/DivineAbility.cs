using VanDao.Data;

namespace VanDao.Game;

public record DivineProgress(int Tier, string Quality, string Name, string Description, int Count, bool Active);
/// <param name="SetRealm">Cảnh giới của bộ đang kích hoạt (MinRealm thấp nhất trong 10 món): chỉ số thần thông tăng theo cảnh giới này.</param>
public record DivineAbilityState(int Tier, string? SetId, string? SetName, string Name, string Description, DivineProgress[] Progress, int SetRealm = 0, string SetRealmName = "");

public static class DivineAbility
{
    public static readonly (int Tier, string Name, string Description)[] Definitions =
    {
        (6, "Bất Diệt Thần Khu", "Nhận đòn chí tử: giữ 1 HP, bất tử đến hết lượt. Một lần mỗi trận."),
        (7, "Tiên Linh Hộ Mệnh", "Có 15% cơ hội vô hiệu hóa đòn tấn công của quái nếu né tránh thất bại."),
        (8, "Đạo Ảnh Liên Kích", "Thêm 20% liên kích."),
        (9, "Nghịch Chuyển Thiên Cơ", "Có 20% cơ hội vô hiệu hóa đòn tấn công của quái nếu né tránh thất bại. Khi né hoặc vô hiệu hóa thành công, đòn thường hoặc kỹ năng tấn công kế tiếp tăng 100% sát thương. Không cộng dồn.")
    };
    // ---- sức mạnh theo cảnh giới của bộ: mức gốc ở bộ Luyện Khí, mỗi cảnh giới cao hơn +RealmStep (bộ Chân Tiên ×1,45) ----
    public const double RealmStep = .05;
    /// <summary>Bất Diệt Thần Khu: phần máu tối đa giữ lại mỗi cảnh giới của bộ (bộ Luyện Khí giữ 1 HP).</summary>
    public const double SaveHpStep = .02;
    public static int ClampRealm(int realm) => Math.Clamp(realm, 0, GameData.Realms.Count - 1);
    public static double Scale(int realm) => 1 + RealmStep * ClampRealm(realm);
    public static double NullifyChance(int tier, int realm) => (tier == 9 ? .20 : tier == 7 ? .15 : 0) * Scale(realm);
    public static double ExtraBonus(int realm) => 20 * Scale(realm);
    public static double BoostPct(int realm) => 100 * Scale(realm);
    public static double SaveHpPct(int realm) => SaveHpStep * ClampRealm(realm);
    private static string P(double v) => PetData.Percent(Math.Round(v, 1));
    /// <summary>Mô tả thần thông với số thật theo cảnh giới của bộ.</summary>
    public static string Describe(int tier, int realm) => tier switch
    {
        6 => SaveHpPct(realm) > 0 ? $"Nhận đòn chí tử: giữ {P(SaveHpPct(realm) * 100)}% máu tối đa, bất tử đến hết lượt. Một lần mỗi trận."
                                   : Definitions[0].Description,
        7 => $"Có {P(NullifyChance(7, realm) * 100)}% cơ hội vô hiệu hóa đòn tấn công của quái nếu né tránh thất bại.",
        8 => $"Thêm {P(ExtraBonus(realm))}% liên kích.",
        9 => $"Có {P(NullifyChance(9, realm) * 100)}% cơ hội vô hiệu hóa đòn tấn công của quái nếu né tránh thất bại. Khi né hoặc vô hiệu hóa thành công, đòn thường hoặc kỹ năng tấn công kế tiếp tăng {P(BoostPct(realm))}% sát thương. Không cộng dồn.",
        _ => "Cần 10 món cùng bộ đạt phẩm Thần trở lên."
    };
    public static DivineAbilityState For(Player p)
    {
        var groups = EquipmentEffects.ValidItems(p).Where(i => i.SetId != null)
            .GroupBy(i => i.SetId).Select(g => new { Id=g.Key, Items=g.ToArray(),
                Tier=g.Count()==10 ? g.Min(i=>Array.IndexOf(GameData.RarityOrder,i.Rarity)) : 0 })
            .OrderByDescending(g=>g.Tier).ThenByDescending(g=>g.Items.Length).ThenBy(g=>g.Id).ToArray();
        var best=groups.FirstOrDefault();
        var tier=best?.Tier >= 6 ? best.Tier : 0;
        var realm=best is null ? 0 : ClampRealm(best.Items.Min(i=>i.MinRealm));   // cảnh giới của bộ đang mặc
        var definition=Definitions.FirstOrDefault(d=>d.Tier==tier);
        return new(tier,best?.Id,best?.Items[0].Set,definition.Name ?? "Chưa kích hoạt",Describe(tier,realm),
            Definitions.Select(d=>new DivineProgress(d.Tier,GameData.QualityName[d.Tier],d.Name,Describe(d.Tier,realm),
                best?.Items.Count(i=>Array.IndexOf(GameData.RarityOrder,i.Rarity)>=d.Tier) ?? 0,tier==d.Tier)).ToArray(),
            realm,best is null ? "" : GameData.Realms[realm].Name);
    }
}

/// <summary>One instance per battle, retained across waves. Only the selected tier operates.</summary>
/// <param name="realm">Cảnh giới của bộ (DivineAbilityState.SetRealm): chỉ số thần thông tăng theo cảnh giới này.</param>
/// <param name="maxHp">Máu tối đa của người mang (Bất Diệt Thần Khu giữ một phần máu theo cảnh giới của bộ).</param>
public sealed class DivineCombat(int tier, int realm = 0, long maxHp = 0)
{
    private bool used, protectedThisTurn, charged;
    public bool Saved { get; private set; }
    public bool Charged => charged;
    public void BeginTurn(){protectedThisTurn=false;Saved=false;}
    public double ExtraChance(double chance) => tier==8 ? Math.Min(50,chance+DivineAbility.ExtraBonus(realm)) : chance;
    public bool Nullify(Random rng) => (tier==7 || tier==9) && rng.NextDouble()<DivineAbility.NullifyChance(tier,realm);
    public void Avoided(){if(tier==9)charged=true;}
    public long Amplify(long damage)
    {
        if(!charged || damage<=0)return damage;
        charged=false;
        return CombatNumber.Round(damage*(1+DivineAbility.BoostPct(realm)/100));
    }
    public long Receive(long hp,long damage)
    {
        if(protectedThisTurn)return hp;
        if(tier==6 && !used && damage>=hp && hp>0){used=true;protectedThisTurn=true;Saved=true;return Math.Max(1,(long)(maxHp*DivineAbility.SaveHpPct(realm)));}
        return hp-damage;
    }
}
