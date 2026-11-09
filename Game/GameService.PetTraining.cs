using VanDao.Data;
namespace VanDao.Game;

/// <summary>Bồi dưỡng linh thú: tẩy luyện / bồi nguyên tư chất, trang bị linh thú, thức tỉnh huyết mạch, cộng hưởng ngũ hành.</summary>
public partial class GameService
{
    // ================= Chỉ số =================
    internal static string GearOf(Pet pet, string slot) => slot switch { "collar" => pet.GearCollar, "armor" => pet.GearArmor, "charm" => pet.GearCharm, _ => "" };
    private static void SetGear(Pet pet, string slot, string item)
    {
        switch (slot) { case "collar": pet.GearCollar = item; break; case "armor": pet.GearArmor = item; break; case "charm": pet.GearCharm = item; break; default: throw new GameException("Ô trang bị linh thú không hợp lệ."); }
    }
    /// <summary>% cộng thêm vào một chỉsố (atk · def · hp) từ trang bị linh thú đang mặc.</summary>
    private static double PetGearPct(Pet pet, string stat) => PetTraining.Slots.Where(s => s.Stat == stat)
        .Sum(s => PetTraining.Gear.TryGetValue(GearOf(pet, s.Id), out var g) ? g.Pct : 0);
    private static (double Pct, string Kind, (string Element, double Pct, string Kind)[] Parts) PetResonance(Player p, Pet pet) =>
        PetData.ById.TryGetValue(pet.DefId, out var d) ? PetTraining.Resonance(d.Elements, CultivationManuals.Active(p)) : (0, "", []);
    /// <summary>Tháo mọi trang bị linh thú về túi (phóng sinh / làm con hiến), không chặn khi túi đầy để không mất đồ.</summary>
    private static int StripPetGear(Player p, Pet pet)
    {
        var n = 0;
        foreach (var s in PetTraining.Slots)
        {
            var item = GearOf(pet, s.Id);
            if (item.Length == 0) continue;
            if (GameData.ItemById.ContainsKey(item)) { p.Add(item, 1); n++; }
            SetGear(pet, s.Id, "");
        }
        return n;
    }

    // ================= Trạng thái gửi giao diện =================
    private static readonly PetTrainingRules TrainingRules = new(
        PetTraining.Slots.Select(s => new PetGearSlotDto(s.Id, s.Name, s.Stat, s.StatName)).ToArray(),
        PetTraining.TierNames,
        PetTraining.Gear.Values.Select(g => new PetGearInfoDto(g.Id, g.Slot, g.Tier, g.Pct)).ToArray(),
        PetTraining.FuseCount, Enumerable.Range(0, PetTraining.TierNames.Length).Select(PetTraining.FuseStones).ToArray(),
        PetTraining.MaxLocks,
        PetTraining.MaxAwaken, PetTraining.AwakenBonus, PetTraining.ResonanceSame, PetTraining.ResonanceSupport,
        new[] { PetTraining.RerollItem }.ToDictionary(id => id, PetLoot.BossSource));

    private static PetTrainingDto TrainingFor(Player p, Pet pet)
    {
        var d = PetData.ById[pet.DefId];
        var (lo, hi) = PetTraining.AptRange(pet, d);
        var lv = Math.Clamp(pet.Awaken, 0, PetTraining.MaxAwaken);
        var cost = PetTraining.AwakenCost(lv);
        var (res, kind, parts) = PetResonance(p, pet);
        return new(lo, hi, PetTraining.RerollStones(PetData.QualityOf(pet, d)), lv, PetTraining.AwakenNames[lv],
            cost.Select(x => new CostDto(x.Key, x.Value, p.Count(x.Key))).ToArray(),
            lv < PetTraining.MaxAwaken ? PetTraining.AwakenDonorStars[lv] : -1, AwakenGate(pet), AwakenHint(p, pet),
            PetTraining.Slots.ToDictionary(s => s.Id, s => GearOf(pet, s.Id)),
            new() { ["atk"] = PetGearPct(pet, "atk"), ["def"] = PetGearPct(pet, "def"), ["hp"] = PetGearPct(pet, "hp") },
            res, kind, CultivationManuals.Active(p), parts.Select(x => new PetResonancePartDto(x.Element, x.Pct, x.Kind)).ToArray());
    }

    // ================= Tư chất =================
    /// <summary>Tẩy luyện: random lại các tư chất chưa khóa (locks = "atk,def"…, tối đa 2) trong khoảng của phẩm.</summary>
    public (string, string) RerollAptitude(Player p, Guid id, string? locks)
    {
        var pet = FindPet(p, id); var d = PetData.ById[pet.DefId];
        var keep = (locks ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToHashSet();
        if (keep.Any(k => k is not ("atk" or "def" or "hp"))) throw new GameException("Tư chất khóa không hợp lệ.");
        if (keep.Count > PetTraining.MaxLocks) throw new GameException($"Chỉ khóa được tối đa {PetTraining.MaxLocks} tư chất.");
        var pills = PetTraining.RerollPills(keep.Count); var stones = PetTraining.RerollStones(PetData.QualityOf(pet, d));
        if (p.Count(PetTraining.RerollItem) < pills) throw new GameException($"Cần {pills} Hoán Cốt Đan (1 viên + 1 viên mỗi tư chất khóa).");
        if (p.Count("linh_thach") < stones) throw new GameException($"Cần {stones:N0} linh thạch.");
        var (lo, hi) = PetTraining.AptRange(pet, d);
        var before = (pet.AptAtk, pet.AptDef, pet.AptHp);
        if (!keep.Contains("atk")) pet.AptAtk = Random.Shared.Next(lo, hi + 1);
        if (!keep.Contains("def")) pet.AptDef = Random.Shared.Next(lo, hi + 1);
        if (!keep.Contains("hp")) pet.AptHp = Random.Shared.Next(lo, hi + 1);
        p.Add(PetTraining.RerollItem, -pills); p.Add("linh_thach", -stones);
        string Diff(string n, int a, int b) => a == b ? $"{n} {b}" : $"{n} {a}→{b}";
        return ($"Tẩy luyện {d.Name}: {Diff("Công", before.AptAtk, pet.AptAtk)} · {Diff("Thủ", before.AptDef, pet.AptDef)} · {Diff("Huyết", before.AptHp, pet.AptHp)}.", "good");
    }


    // ================= Trang bị linh thú =================
    public (string, string) EquipPetGear(Player p, Guid id, string itemId)
    {
        var pet = FindPet(p, id);
        if (!PetTraining.Gear.TryGetValue(itemId, out var g)) throw new GameException("Đây không phải trang bị linh thú.");
        if (p.Count(itemId) < 1) throw new GameException("Không có trang bị này trong hành trang.");
        var old = GearOf(pet, g.Slot);
        if (old == itemId) throw new GameException("Linh thú đang mặc đúng món này.");
        if (old.Length > 0 && p.Count(old) == 0 && p.Count(itemId) > 1 && BagUsed(p) >= BagCap(p)) throw new GameException("Hành trang đầy, không tháo được món đang mặc.");
        p.Add(itemId, -1);
        if (old.Length > 0 && GameData.ItemById.ContainsKey(old)) p.Add(old, 1);
        SetGear(pet, g.Slot, itemId);
        return ($"{PetData.ById[pet.DefId].Name} mặc {GameData.ItemById[itemId].Name}.", "good");
    }

    public (string, string) UnequipPetGear(Player p, Guid id, string slot)
    {
        var pet = FindPet(p, id);
        if (PetTraining.Slots.All(s => s.Id != slot)) throw new GameException("Ô trang bị linh thú không hợp lệ.");
        var old = GearOf(pet, slot);
        if (old.Length == 0) throw new GameException("Ô này đang trống.");
        if (p.Count(old) == 0 && BagUsed(p) >= BagCap(p)) throw new GameException("Hành trang đầy.");
        if (GameData.ItemById.ContainsKey(old)) p.Add(old, 1);
        SetGear(pet, slot, "");
        return ($"Đã tháo {GameData.ItemById.GetValueOrDefault(old)?.Name ?? old}.", "good");
    }

    /// <summary>Ghép 3 món cùng ô, cùng phẩm → 1 món phẩm kế (n = số lần, 0 = tối đa).</summary>
    public (string msg, string kind, CraftResultDto? craft) FusePetGear(Player p, string itemId, int n)
    {
        if (!PetTraining.Gear.TryGetValue(itemId, out var g)) throw new GameException("Đây không phải trang bị linh thú.");
        if (g.Tier >= PetTraining.TierNames.Length - 1) throw new GameException("Đã là phẩm cao nhất.");
        var stones = PetTraining.FuseStones(g.Tier);
        var max = Math.Min(p.CountInt(itemId) / PetTraining.FuseCount, stones > 0 ? (int)Math.Min(int.MaxValue, p.Count("linh_thach") / stones) : int.MaxValue);
        if (p.Count(itemId) < PetTraining.FuseCount) throw new GameException($"Cần {PetTraining.FuseCount} món {GameData.ItemById[itemId].Name}.");
        if (max < 1) throw new GameException($"Cần {stones:N0} linh thạch mỗi lần ghép.");
        var times = n <= 0 ? max : Math.Min(n, max);
        var to = PetTraining.GearId(g.Slot, g.Tier + 1);
        if (p.Count(to) == 0 && p.Count(itemId) - times * PetTraining.FuseCount > 0 && BagUsed(p) >= BagCap(p)) throw new GameException("Hành trang đầy.");
        p.Add(itemId, -times * PetTraining.FuseCount); p.Add("linh_thach", -stones * times); p.Add(to, times);
        var msg = Log(p, $"Ghép trang bị linh thú: {times * PetTraining.FuseCount} {GameData.ItemById[itemId].Name} → {times} {GameData.ItemById[to].Name}.", "good");
        return (msg.Item1, msg.Item2, new CraftResultDto("petgear", new ItemQtyDto(to, times),
            new() { new(itemId, times * PetTraining.FuseCount), new("linh_thach", stones * times) }, null));
    }

    /// <summary>Hợp thành hỗn hợp: 3 món cùng phẩm nhưng khác ô (hoặc lẫn nhiều ô) → 1 món phẩm kế, ô nhận về ngẫu nhiên đều trong mọi ô.
    /// Cùng phí linh thạch như hợp thường. 3 món cùng một loại thì vẫn đi đường hợp thường (<see cref="FusePetGear"/>), ô cố định.</summary>
    public (string msg, string kind, CraftResultDto? craft) FuseMixedPetGear(Player p, IReadOnlyList<string> itemIds, Random? rng = null)
    {
        if (itemIds.Count != PetTraining.FuseCount) throw new GameException($"Cần đúng {PetTraining.FuseCount} món trang bị linh thú.");
        var defs = itemIds.Select(id => PetTraining.Gear.TryGetValue(id, out var d) ? d : throw new GameException("Đây không phải trang bị linh thú.")).ToList();
        var tier = defs[0].Tier;
        if (defs.Any(d => d.Tier != tier)) throw new GameException("Các món phải cùng phẩm.");
        if (tier >= PetTraining.TierNames.Length - 1) throw new GameException("Đã là phẩm cao nhất.");
        if (defs.Select(d => d.Slot).Distinct().Count() == 1) throw new GameException("Cả 3 món cùng ô: dùng hợp thành thường (ô không đổi).");
        var uses = itemIds.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        foreach (var (id, n) in uses) if (p.Count(id) < n) throw new GameException($"Không đủ {GameData.ItemById[id].Name} trong hành trang.");
        var stones = PetTraining.FuseStones(tier);
        if (p.Count("linh_thach") < stones) throw new GameException($"Cần {stones:N0} linh thạch mỗi lần ghép.");
        var slot = PetTraining.Slots[(rng ?? Random.Shared).Next(PetTraining.Slots.Length)].Id;
        var to = PetTraining.GearId(slot, tier + 1);
        var freed = uses.Count(x => p.Count(x.Key) == x.Value);   // ô túi trống ra sau khi tiêu hao
        if (p.Count(to) == 0 && freed == 0 && BagUsed(p) >= BagCap(p)) throw new GameException("Hành trang đầy.");
        foreach (var (id, n) in uses) p.Add(id, -n);
        p.Add("linh_thach", -stones); p.Add(to, 1);
        var msg = Log(p, $"Hợp thành hỗn hợp trang bị linh thú: {string.Join(" + ", itemIds.Select(id => GameData.ItemById[id].Name))} → {GameData.ItemById[to].Name} (ô ngẫu nhiên).", "good");
        return (msg.Item1, msg.Item2, new CraftResultDto("petgear", new ItemQtyDto(to, 1),
            uses.Select(x => new ItemQtyDto(x.Key, x.Value)).Append(new("linh_thach", stones)).ToList(), null));
    }

    /// <summary>Hợp nhanh theo số trong túi ban đầu (không hợp tiếp món vừa tạo), phẩm thấp trước tới khi hết linh thạch.
    /// Mỗi phẩm: nhóm đủ 3 món cùng ô hợp thường trước; món lẻ còn lại (khác ô) gom 3 món một lượt hỗn hợp, ô nhận về ngẫu nhiên.</summary>
    public (string msg, string kind, CraftResultDto? craft) QuickFusePetGear(Player p, Random? rng = null)
    {
        rng ??= Random.Shared;
        var left = p.Count("linh_thach");
        var consume = new Dictionary<string, long>(); var produce = new Dictionary<string, long>();
        long spent = 0; var total = 0; var mixedTimes = 0;
        for (var t = 0; t < PetTraining.TierNames.Length - 1; t++)
        {
            var stones = PetTraining.FuseStones(t);
            var rem = new List<(string id, decimal n)>();
            foreach (var s in PetTraining.Slots)
            {
                var id = PetTraining.GearId(s.Id, t); var c = p.Count(id);
                var times = (int)Math.Min(Math.Floor(c / PetTraining.FuseCount), Math.Floor(left / stones));
                if (times > 0)
                {
                    var to = PetTraining.GearId(s.Id, t + 1);
                    consume[id] = consume.GetValueOrDefault(id) + (long)times * PetTraining.FuseCount;
                    produce[to] = produce.GetValueOrDefault(to) + times;
                    left -= (long)stones * times; spent += (long)stones * times; total += times; c -= (long)times * PetTraining.FuseCount;
                }
                rem.Add((id, c));
            }
            while (left >= stones && rem.Sum(x => x.n) >= PetTraining.FuseCount)
            {
                var picked = new List<int>();
                for (var k = 0; k < PetTraining.FuseCount; k++)
                {
                    var i = rem.IndexOf(rem.MaxBy(x => x.n)); picked.Add(i);
                    rem[i] = (rem[i].id, rem[i].n - 1);
                }
                if (picked.Distinct().Count() == 1) break;
                foreach (var i in picked) consume[rem[i].id] = consume.GetValueOrDefault(rem[i].id) + 1;
                var o = PetTraining.GearId(PetTraining.Slots[rng.Next(PetTraining.Slots.Length)].Id, t + 1);
                produce[o] = produce.GetValueOrDefault(o) + 1;
                left -= stones; spent += stones; total++; mixedTimes++;
            }
        }
        if (total == 0)
        {
            var g = Enumerable.Range(0, PetTraining.TierNames.Length - 1).Where(t => PetTraining.Slots.Sum(s => p.Count(PetTraining.GearId(s.Id, t))) >= PetTraining.FuseCount).Cast<int?>().FirstOrDefault();
            throw new GameException(g is null ? $"Cần ít nhất {PetTraining.FuseCount} trang bị linh thú cùng phẩm."
                : $"Cần {PetTraining.FuseStones(g.Value):N0} linh thạch mỗi lần ghép.");
        }
        var after = consume.Keys.Concat(produce.Keys).Distinct().ToDictionary(id => id, p.Count);
        foreach (var x in consume) after[x.Key] -= x.Value;
        foreach (var x in produce) after[x.Key] += x.Value;
        var extra = after.Count(x => x.Value > 0 && p.Count(x.Key) == 0) - after.Count(x => x.Value == 0 && p.Count(x.Key) > 0);
        if (extra > 0 && BagUsed(p) + extra > BagCap(p)) throw new GameException("Hành trang đầy.");
        foreach (var x in consume) p.Add(x.Key, -x.Value);
        p.Add("linh_thach", -spent);
        foreach (var x in produce) p.Add(x.Key, x.Value);
        var inputs = consume.Select(x => new ItemQtyDto(x.Key, x.Value)).Append(new("linh_thach", spent)).ToList();
        var outputs = produce.Select(x => new ItemQtyDto(x.Key, x.Value)).ToList();
        var msg = Log(p, $"Hợp nhanh trang bị linh thú: {total} lượt{(mixedTimes > 0 ? $" ({mixedTimes} lượt hỗn hợp)" : "")} → {string.Join(", ", outputs.Select(o => $"{o.Qty} {GameData.ItemById[o.Item].Name}"))}.", "good");
        return (msg.Item1, msg.Item2, new CraftResultDto("petgear", outputs[0], inputs, outputs));
    }

    // ================= Thức tỉnh huyết mạch =================
    private static bool AwakenGate(Pet pet) => PetData.StarsOf(pet) >= PetData.MaxStars && pet.Awaken < PetTraining.MaxAwaken;
    private static string AwakenHint(Player p, Pet pet)
    {
        if (pet.Awaken >= PetTraining.MaxAwaken) return $"Đã thức tỉnh tối đa (+{PetTraining.MaxAwaken * PetTraining.AwakenBonus * 100:0}% chỉ số).";
        if (PetData.StarsOf(pet) < PetData.MaxStars) return $"Cần {PetData.MaxStars}★ để thức tỉnh huyết mạch.";
        var stars = PetTraining.AwakenDonorStars[pet.Awaken];
        var missing = new List<string>();
        if (!p.Pets.Any(x => CanAwakenDonor(pet, x))) missing.Add($"1 {PetData.ById[pet.DefId].Name} {stars}★ khác đang nghỉ");
        if (!p.Has(PetTraining.AwakenCost(pet.Awaken))) missing.Add("đủ nguyên liệu");
        return missing.Count > 0 ? "Cần " + string.Join(" · ", missing) + "." : "Đã đủ điều kiện thức tỉnh.";
    }
    private static bool CanAwakenDonor(Pet pet, Pet donor) => pet.Awaken < PetTraining.MaxAwaken && donor.Id != pet.Id && donor.DefId == pet.DefId && donor.Slot == 0
        && PetData.StarsOf(donor) >= PetTraining.AwakenDonorStars[pet.Awaken];

    public (string, string) AwakenPet(Player p, Guid id, Guid donorId)
    {
        var pet = FindPet(p, id);
        if (!AwakenGate(pet)) throw new GameException(AwakenHint(p, pet));
        var donor = FindPet(p, donorId);
        if (!CanAwakenDonor(pet, donor)) throw new GameException($"Con hiến phải cùng loài, từ {PetTraining.AwakenDonorStars[pet.Awaken]}★ và đang nghỉ.");
        var cost = PetTraining.AwakenCost(pet.Awaken);
        if (!p.Has(cost)) throw new GameException(AwakenHint(p, pet));
        p.Pay(cost);
        var back = donor.FoodSpent / 2; StripPetGear(p, donor);
        db.Pets.Remove(donor); p.Pets.Remove(donor);
        if (back > 0) p.Add("linh_thu_dan", back);
        pet.Awaken++;
        return Log(p, $"{PetData.ById[pet.DefId].Name} {PetTraining.AwakenNames[pet.Awaken]}: +{pet.Awaken * PetTraining.AwakenBonus * 100:0}% chỉ số{(back > 0 ? $", hoàn {back} Linh Thú Đan" : "")}.", "gold");
    }
}
