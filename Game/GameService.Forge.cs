using VanDao.Data;
namespace VanDao.Game;

/// <summary>Lò Rèn: rèn ô trang bị đang mặc. Server roll kết quả; client chỉ hiển thị (S.forge).</summary>
public partial class GameService
{
    private static ItemDef ForgeItem(Player p, string slot)
    {
        if (!GameData.Realms[p.Realm].EquipSlots.Contains(slot)) throw new GameException("Ô trang bị chưa mở.");
        return EquipmentEffects.ValidItems(p).FirstOrDefault(i => i.Slot == slot)
            ?? throw new GameException("Ô này chưa mặc trang bị, không có gì để rèn.");
    }

    public (string msg, string kind) ForgeStrike(Player p, string slot, Random? random = null)
    {
        var item = ForgeItem(p, slot);
        var save = ForgeData.Load(p);
        var cur = save.Slots.GetValueOrDefault(slot) ?? new ForgeData.SlotState();
        var mods = ForgeRelicData.ModsOf(save.Relics);
        var rng = random ?? Random.Shared;
        if (cur.L >= ForgeData.MaxLevel) throw new GameException($"Đã đạt cực hạn +{ForgeData.MaxLevel}.");
        var stone = ForgeData.StoneCost(item, cur.L, mods);
        var iron = ForgeData.IronCost(cur.L);
        if (p.Count("linh_thach") < stone) throw new GameException($"Cần {stone:N0} linh thạch cho nhát rèn này.");
        if (p.Count(ForgeData.Iron) < iron) throw new GameException($"Cần {iron} Tinh Thiết cho nhát rèn này.");
        p.Add("linh_thach", -stone);
        var outcome = ForgeData.Roll(ForgeData.OddsAt(cur.L, cur.F > 0, mods), rng.NextDouble());
        var ironFree = mods.IronFreeChance > 0 && rng.NextDouble() < mods.IronFreeChance;   // Tiết Thiết Lô
        if (!ironFree) p.Add(ForgeData.Iron, -iron);
        var next = ForgeData.Apply(cur, outcome, mods);
        save.Undo = new(slot, cur);
        save.Slots[slot] = next;
        save.N++;
        save.Log.Insert(0, new(save.N, slot, item.Id, cur.L, next.L, ForgeData.Key(outcome), DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        ForgeData.Store(p, save);
        RecordDaily(p, DailyQuestData.EvForge);

        var slotName = GemData.SlotNames.GetValueOrDefault(slot, slot);
        var lit = cur.F == 0 && next.F > 0 ? $" Thần Hỏa bùng lên: {mods.FrenzyStrikes} nhát tới không thể vỡ." : "";
        var milestone = (outcome is ForgeData.Outcome.Success or ForgeData.Outcome.Great)
            && (next.L / 10 > cur.L / 10 || next.L == ForgeData.MaxLevel);
        return outcome switch
        {
            ForgeData.Outcome.Break when cur.L >= 10 => Log(p, $"Lò Rèn: {slotName} +{cur.L} vỡ nát, về +{next.L}.", "bad"),
            ForgeData.Outcome.Break => ($"Vỡ nát: {slotName} +{cur.L} về +{next.L}.", "bad"),
            ForgeData.Outcome.Great when milestone => Log(p, $"Lò Rèn: Đại thành! {slotName} +{cur.L} → +{next.L}.{lit}", "gold"),
            ForgeData.Outcome.Success when milestone => Log(p, $"Lò Rèn: {slotName} lên +{next.L}.{lit}", "gold"),
            ForgeData.Outcome.Great => ($"Đại thành! {slotName} +{cur.L} → +{next.L}.{lit}", "gold"),
            ForgeData.Outcome.Success => ($"Thành công: {slotName} +{next.L}.{lit}", "good"),
            ForgeData.Outcome.Damage => ($"Tổn hại: {slotName} +{cur.L} → +{next.L}.{lit}", "bad"),
            _ => ($"Bất biến: {slotName} giữ +{cur.L}.{lit}", ""),
        };
    }

    public (string msg, string kind) ForgeUndo(Player p, Random? random = null)
    {
        var save = ForgeData.Load(p);
        if (save.Undo is not { } undo) throw new GameException("Không có nhát rèn nào để hoàn.");
        if (p.Count(ForgeData.Rewind) < 1) throw new GameException("Cần 1 Hồi Thiên Phù.");
        var kept = ForgeRelicData.ModsOf(save.Relics).RewindKeepChance is > 0 and var k && (random ?? Random.Shared).NextDouble() < k;   // Hồi Quang Kính
        if (!kept) p.Add(ForgeData.Rewind, -1);
        var now = save.Slots.GetValueOrDefault(undo.Slot)?.L ?? 0;
        save.Slots[undo.Slot] = undo.Before;
        save.Undo = null;
        var itemId = EquipmentEffects.ValidItems(p).FirstOrDefault(i => i.Slot == undo.Slot)?.Id ?? "";
        save.Log.Insert(0, new(save.N, undo.Slot, itemId, now, undo.Before.L, "undo", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        ForgeData.Store(p, save);
        var slotName = GemData.SlotNames.GetValueOrDefault(undo.Slot, undo.Slot);
        return Log(p, $"Hồi Thiên Phù: {slotName} trở về +{undo.Before.L}.{(kept ? " Hồi Quang Kính giữ lại phù." : "")}", "good");
    }

    public ForgeDto ForgeStateFor(Player p)
    {
        var save = ForgeData.Load(p);
        var mods = ForgeRelicData.ModsOf(save.Relics);
        var items = EquipmentEffects.ValidItems(p).ToDictionary(i => i.Slot!);
        var slots = GameData.Realms[p.Realm].EquipSlots.Select(slot =>
        {
            var s = save.Slots.GetValueOrDefault(slot) ?? new ForgeData.SlotState();
            var item = items.GetValueOrDefault(slot);
            var max = s.L >= ForgeData.MaxLevel;
            return new ForgeSlotDto(slot, item?.Id, s.L, s.C, s.F,
                ForgeData.BonusPct(s.L) * mods.BonusMult, ForgeData.BonusPct(Math.Min(ForgeData.MaxLevel, s.L + 1)) * mods.BonusMult,
                item is null || max ? 0 : ForgeData.StoneCost(item, s.L, mods), max ? 0 : ForgeData.IronCost(s.L),
                ForgeData.OddsAt(s.L, s.F > 0, mods).ToArray());
        }).ToList();
        var shelf = ForgeShelfOf(p, save)
            .Select(id => new ForgeShelfDto(id, save.Bought.Contains(id), save.Relics.Contains(id))).ToList();
        var log = save.Log.Select(l => new ForgeLogDto(l.N, l.Slot, l.Item, l.From, l.To, l.Kind, l.At)).ToList();
        return new ForgeDto(slots, save.Undo is not null, log, ForgeData.MaxLevel, ForgeData.FrenzyCharge,
            save.Points, save.Relics.ToList(), shelf, ForgeRelicData.RerollStone, ForgeRelicData.Slots);
    }
}
