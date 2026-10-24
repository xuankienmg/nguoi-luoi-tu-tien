using Microsoft.EntityFrameworkCore;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>
/// Toàn bộ luật chơi chạy ở server. Client chỉ hiển thị và gửi hành động,
/// nên người chơi không thể sửa tu vi hay vật phẩm từ trình duyệt.
/// </summary>
public partial class GameService(GameDbContext db, LeaderboardService? lb = null, MarketService? market = null, VersionService? version = null, ArenaService? arena = null)
{
    private readonly MarketService? _market = market;
    private readonly ArenaService? _arena = arena;
    private readonly VersionService? _version = version;   // giờ máy chủ cho sự kiện
    private const int LogKeep = 100;
    private const int LogShow = 40;

    // ================= Truy vấn =================

    public async Task<Player> CreateAsync(string? name, Guid? id = null)
    {
        var p = new Player { Id = id ?? Guid.NewGuid(), Name = string.IsNullOrWhiteSpace(name) ? "Đạo hữu" : name.Trim()[..Math.Min(64, name.Trim().Length)], LbSecret = NewLbSecret(), ManualVersion = 1 };
        foreach (var (item, n) in GameData.StartingInventory) p.Add(item, n);
        p.Add(PetData.StarterEgg, 1);
        db.Players.Add(p);
        p.ExploreReadyAt = DateTime.UtcNow.AddSeconds(ExploreSeconds(p));
        AddLog(p, "Đạo tâm vừa khởi. Nhân vật sẽ tự đi thám hiểm, nhặt linh tài và tích tu vi.");
        AddLog(p, "Nhận quà khởi đầu: Trứng Hỏa Hồ ×1. Đem ấp để kết duyên cùng Hỏa Linh Hồ.", "gold");
        AddLog(p, "Nhận Xích Diễm Phần Thiên Kinh. Dùng trong hành trang để mở cây kỹ năng Hỏa.", "gold");
        await db.SaveChangesAsync();
        return p;
    }

    /// <summary>
    /// Tu sĩ đang chơi trên máy này: người có tiến độ cao nhất (cảnh giới, tầng, số vật phẩm),
    /// hoà thì lấy người chơi gần nhất; chưa có thì tạo mới.
    /// Không dựa vào localStorage vì cửa sổ desktop chạy server ở cổng ngẫu nhiên mỗi lần mở
    /// (bản cũ vì vậy đã lỡ tạo thêm các tu sĩ trống — cách chọn này lấy lại đúng nhân vật thật).
    /// </summary>
    public async Task<Player> CurrentAsync()
    {
        var id = await db.Players
            .Select(x => new { x.Id, x.Realm, x.Stage, Items = x.Inventory.Sum(i => i.Count), x.LastTickAt })
            .OrderByDescending(x => x.Realm).ThenByDescending(x => x.Stage).ThenByDescending(x => x.Items).ThenByDescending(x => x.LastTickAt)
            .Select(x => x.Id).FirstOrDefaultAsync();
        return id == Guid.Empty ? await CreateAsync(null) : await LoadAsync(id);
    }

    /// <summary>Nạp người chơi và cộng tu vi theo thời gian thực đã trôi qua.</summary>
    public async Task<Player> LoadAsync(Guid id)
    {
        var p = await db.Players.Include(x => x.Inventory).Include(x => x.Equipment).Include(x => x.Pets).AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new KeyNotFoundException();
        // Retired guard slots return their pets to rest; only one active pet contributes.
        foreach (var pet in p.Pets.Where(pet => pet.Slot > 1)) pet.Slot = 0;
        // trang bị bản cũ (chưa có phẩm / sai cấp) đang mặc → món đúng cấp cùng phẩm, mặc nguyên tại chỗ
        foreach (var e in p.Equipment) if (GameData.UpgradeGear(e.ItemId) is { } up) e.ItemId = up;
        // món đang mặc đã bị bỏ khỏi game → tháo vào túi để ConvertLegacy quy đổi ra linh thạch
        foreach (var e in p.Equipment.Where(e => !GameData.ItemById.ContainsKey(e.ItemId)).ToList())
        {
            p.Add(e.ItemId, 1);
            p.Equipment.Remove(e);
            db.Equipment.Remove(e);
        }
        ConvertLegacy(p);
        CultivationManuals.Migrate(p);
        p.SkillSlots = string.Join(',', p.Slots().Select(id => SkillData.ById.TryGetValue(id, out var skill)
            && CultivationManuals.IsUnlocked(p, skill.Element) ? id : ""));
        // bảng xếp hạng: nhân vật cũ chưa có khóa → tạo; đang bật cờ / còn điểm admin → loại khỏi bảng xếp hạng
        if (p.LbSecret.Length == 0) p.LbSecret = NewLbSecret();
        if (p.Admin.Length > 0 || p.AdminPoints > 0) p.Tainted = true;
        // đòn thường đã bỏ (Hỏa Tiễn, Thủy Châm) → đổi sang đòn thường còn lại cùng hệ
        if (p.SkillSlots.Length > 0)
            p.SkillSlots = string.Join(',', p.SkillSlots.Split(',').Select(x => SkillData.RemovedBasics.TryGetValue(x.Trim(), out var to) ? to : x));
        // công pháp kích hoạt: bản cũ chưa chọn → hệ cấp cao nhất; ô đòn thường luôn theo hệ đang kích hoạt
        CultivationManuals.SyncActive(p);
        // luật điểm mới (tầng/kỳ +1, đại cảnh giới +12, hạ Boss không cho điểm): đã dùng quá số điểm → tẩy kỹ năng, hoàn điểm
        if (SkillPoints(p) < 0)
        {
            p.SetSkillLevels(new Dictionary<string, int> { [SkillData.StartSkill] = 1 });
            p.SkillSlots = "";
            Log(p, $"Luật điểm kỹ năng đã đổi (+{SkillData.PointsPerStage} mỗi tầng/kỳ, +{SkillData.PointsPerRealm} mỗi đại cảnh giới): kỹ năng đã được tẩy, hoàn lại {SkillPoints(p)} điểm để lĩnh ngộ lại.", "gold");
        }
        Tick(p, DateTime.UtcNow);
        SyncEvent(p);   // sự kiện Thiên Cơ: sang đợt mới thì gửi quà còn sót, đặt lại tiến độ
        // ô "Nhẫn 2" cũ (ring2) → ô Hộ phù / Nhẫn: món đang mặc sai loại ô thì chuyển sang ô đúng nếu trống, không thì trả về túi
        foreach (var e in p.Equipment.Where(e => GameData.ItemById.TryGetValue(e.ItemId, out var d) && d.Slot != e.Slot).ToList())
        {
            var want = GameData.ItemById[e.ItemId].Slot!;
            p.Equipment.Remove(e); db.Equipment.Remove(e);
            if (Realm(p).EquipSlots.Contains(want) && p.Equipment.All(x => x.Slot != want))
                p.Equipment.Add(new EquippedItem { PlayerId = p.Id, Slot = want, ItemId = e.ItemId });
            else p.Add(e.ItemId, 1);
        }
        // ô trang bị chưa mở ở cảnh giới hiện tại (dữ liệu bản cũ) → trả món về túi
        foreach (var e in p.Equipment.Where(e => !Realm(p).EquipSlots.Contains(e.Slot)).ToList())
        {
            p.Add(e.ItemId, 1);
            p.Equipment.Remove(e);
            db.Equipment.Remove(e);
        }
        return p;
    }

    /// <summary>Số ô túi đang dùng: mỗi món trang bị một ô, mỗi loại vật phẩm một ô.</summary>
    public static int BagUsed(Player p) =>
        p.Inventory.Where(i => i.Count > 0).Sum(i => GameData.ItemById.TryGetValue(i.ItemId, out var d) && d.Slot is not null ? i.Count : 1);

    public static int BagCap(Player p) => Realm(p).BagSlots;

    /// <summary>Thêm vật phẩm rơi vào túi nếu còn chỗ; trả về số lượng thực nhận.</summary>
    private static int TryAddLoot(Player p, string itemId, int n)
    {
        var free = BagCap(p) - BagUsed(p);
        var isGear = GameData.ItemById[itemId].Slot is not null;
        if (isGear) n = Math.Max(0, Math.Min(n, free));
        else if (p.Count(itemId) == 0 && free <= 0) n = 0;   // loại vật phẩm mới cần thêm một ô
        if (n > 0) p.Add(itemId, n);
        return n;
    }

    public async Task<StateDto> BuildStateAsync(Player p)
    {
        var logs = await db.Logs.Where(l => l.PlayerId == p.Id)
            .OrderByDescending(l => l.Id).Take(LogShow)
            .Select(l => new LogDto(DateTime.SpecifyKind(l.At, DateTimeKind.Utc), l.Text, l.Kind))
            .ToListAsync();
        // log mới thêm trong request này chưa lưu thì chưa có trong DB
        logs.InsertRange(0, p.Logs.Where(l => l.Id == 0).OrderByDescending(l => l.At)
            .Select(l => new LogDto(l.At, l.Text, l.Kind)));

        var r = Realm(p);
        var peak = IsPeak(p);
        var next = NextRealm(p);
        var cost = TargetCost(p);

        string title, kind;
        if (peak && next is null) { title = $"{r.Name} {r.Stages[p.Stage]}"; kind = "Đỉnh phong hiện tại"; }
        else if (peak) { title = $"{next!.Name} · {next.Stages[0]}"; kind = "Đột phá cảnh giới"; }
        else { title = $"{r.Name} · {r.Stages[p.Stage + 1]}"; kind = "Đột phá tầng tiếp theo"; }

        return new StateDto(
            p.Id, p.Name, p.Realm, p.Stage, r.Name, r.Stages[p.Stage],
            p.Qi, r.QiReq(p.Stage), Rate(p), Rate(p) * GameData.ChannelMultiplier,
            peak, next is not null, title, kind,
            cost.Select(kv => new CostDto(kv.Key, kv.Value, p.Count(kv.Key))).ToList(),
            Chance(p), p.Bonus,
            Math.Max(0, (long)(p.ExploreReadyAt - DateTime.UtcNow).TotalMilliseconds),
            p.AutoExplore,
            (long)(ExploreSeconds(p) * 1000),
            ExploreQi(p),
            p.Equipment.ToDictionary(e => e.Slot, e => e.ItemId),
            new BonusDto(Bonus(p, i => i.QiPct), Bonus(p, i => i.SpeedPct)),
            Stats(p),
            Battle(p),
            r.Cn, next?.Name ?? GameData.LockedRealms[0], next?.Cn ?? GameData.LockedRealmsCn[0], next is null && peak,
            BagUsed(p), BagCap(p), r.EquipSlots, Level(p),
            p.Inventory.Where(i => i.Count > 0).ToDictionary(i => i.ItemId, i => i.Count),
            MapState(p),
            SkillLevels(p), p.Slots(), SkillPoints(p), SkillData.PointsEarned(Level(p), p.Realm, p.BossKills) + p.ElixirPoints,
            new BreakStateDto(Realm(p).BreakChance, Math.Max(0, (long)(p.BreakCooldownUntil - DateTime.UtcNow).TotalMilliseconds),
                Math.Max(0, (long)(p.DebuffUntil - DateTime.UtcNow).TotalMilliseconds), BreakPreview(p)),
            logs.Take(LogShow).ToList(), Player.AdminEnabled ? p.Admin : "", Player.AdminEnabled ? ChestOdds(p) : [],   // tỉ lệ rương chỉ hiện ở admin mode: bản phát hành khỏi tính + gửi ~11 KB mỗi lần đồng bộ
            p.PillBonus, CurrentSkin(p), p.SpinHistory, EquipmentEffects.Calculate(p), StatSources(p), CombatLimits(p), PetStateFor(p), SpinDailyFor(p), SpinDailyFor(p, pet: true), CultivationManuals.Unlocked(p), CultivationManuals.Active(p),
            UnlockedSkins(p).ToArray(), CurrentGender(p), EventStateFor(p), ArenaReplay(p));
    }

    /// <summary>Tỉ lệ phần thưởng khi mở từng loại rương, tính đúng như OpenOne (dùng cho chế độ admin).</summary>
    public static List<ChestOddsDto> ChestOdds(Player p) => GameData.AllItems.Where(i => i.Type == "chest").Select(chest =>
    {
        var kv=new KeyValuePair<string,GameData.ChestDef>(chest.Id,GameData.Chests[GameData.ChestBase(chest.Id)]);
        var cd=kv.Value;var table=ChestRewards.Table(p.Realm,kv.Key,GameData.ChestDiff(kv.Key),p.Chapter);
        List<ItemQtyPctDto> Group(string group)=>table.Where(x=>x.Group==group).GroupBy(x=>x.BaseItem??x.Item)
            .Select(g=>new ItemQtyPctDto(g.Key,g.Sum(x=>x.Probability)*100)).ToList();
        double Chance(string group)=>table.Where(x=>x.Group==group).Sum(x=>x.Probability)*100;
        return new ChestOddsDto(kv.Key,GameData.GearLevels[Math.Clamp(p.Realm,0,GameData.GearLevels.Length-1)],
            GameData.ChestQuality(GameData.ChestBase(kv.Key),p.Realm,GameData.ChestDiff(kv.Key)).Select(q=>q/cd.QualityAt(p.Realm).Sum()*100).ToArray(),Group("treasure"),Group("gear"),
            GameData.Realms[p.Realm].StageCost(0).Keys.First(k=>k!="linh_thach"),cd.PillMin,cd.PillMax,Chance("pill"),
            Group("herb"),cd.HerbMin,cd.HerbMax,Chance("herb"),Chance("enlightenment"),Group("pet"),Group("manual"));
    }).ToList();

    // ================= Hành động =================

    public string Channel(Player p)
    {
        var gain = Rate(p) * GameData.ChannelMultiplier;
        p.Qi = Math.Min(QiReq(p), p.Qi + gain);
        return $"+{gain:0.#} tu vi";
    }

    public (string msg, string kind) Equip(Player p, string itemId, string? targetSlot = null)
    {
        if (!GameData.ItemById.TryGetValue(itemId, out var def) || def.Slot is null) throw new GameException("Vật phẩm này không trang bị được.");
        if (p.Count(itemId) < 1) throw new GameException("Không có vật phẩm này trong hành trang.");
        if (p.Realm < def.MinRealm) throw new GameException($"Cần cảnh giới {GameData.Realms[def.MinRealm].Name} để mặc {def.Name}.");
        // mỗi món một ô (Hộ phù và Nhẫn là 2 ô riêng từ 10/2026)
        var options = new[] { def.Slot };
        var open = options.Where(o => Realm(p).EquipSlots.Contains(o)).ToArray();
        if (open.Length == 0)
        {
            var opens = GameData.Realms.FirstOrDefault(r => r.EquipSlots.Contains(def.Slot));
            throw new GameException($"Ô {GameData.Slots.First(x => x.Id == def.Slot).Name} mở ở cảnh giới {opens?.Name}.");
        }
        string target;
        if (targetSlot is not null)
        {
            if (!open.Contains(targetSlot)) throw new GameException($"{def.Name} không mặc được vào ô này.");
            target = targetSlot;
        }
        else target = open.FirstOrDefault(o => p.Equipment.All(e => e.Slot != o)) ?? open[0];

        var slot = p.Equipment.FirstOrDefault(e => e.Slot == target);
        if (slot is null) p.Equipment.Add(slot = new EquippedItem { PlayerId = p.Id, Slot = target });
        else p.Add(slot.ItemId, 1); // trả món cũ về túi
        slot.ItemId = itemId;
        p.Add(itemId, -1);
        return ($"Đã trang bị {def.Name}.", "good");
    }

    /// <summary>Trang bị nhanh: mỗi ô đang mở chọn món (trong túi hoặc đang mặc) cho Chiến lực cao nhất.
    /// Chạy 2 vòng để tính cả thưởng cùng bộ (đổi một ô có thể làm ô khác đáng đổi theo). Không tháo món nào ra để trống.</summary>
    public (string msg, string kind) EquipBest(Player p)
    {
        var before = Power(p); var changed = new List<string>();
        long Try(string slotId, string itemId)                      // Chiến lực nếu ô slotId mặc itemId (thử tạm, trả lại như cũ)
        {
            var cur = p.Equipment.FirstOrDefault(e => e.Slot == slotId);
            if (cur is not null) { var old = cur.ItemId; cur.ItemId = itemId; var v = Power(p); cur.ItemId = old; return v; }
            var tmp = new EquippedItem { PlayerId = p.Id, Slot = slotId, ItemId = itemId };
            p.Equipment.Add(tmp); var w = Power(p); p.Equipment.Remove(tmp); return w;
        }
        for (var pass = 0; pass < 2; pass++)
            foreach (var s in Realm(p).EquipSlots)
            {
                var cands = p.Inventory.Where(i => i.Count > 0 && GameData.ItemById.TryGetValue(i.ItemId, out var d) && d.Slot == s && d.MinRealm <= p.Realm)
                    .Select(i => i.ItemId).ToList();
                if (cands.Count == 0) continue;
                var best = Power(p); string? pick = null;
                foreach (var id in cands) { var v = Try(s, id); if (v > best) { best = v; pick = id; } }
                if (pick is null) continue;
                Equip(p, pick, s);
                changed.Add(GameData.ItemById[pick].Name);
            }
        if (changed.Count == 0) return Log(p, "Trang bị nhanh: đồ đang mặc đã là tốt nhất trong hành trang.", "");
        var after = Power(p);
        return Log(p, $"Trang bị nhanh {changed.Count} món ({string.Join(", ", changed.Distinct())}): Chiến lực {before:N0} → {after:N0} (+{after - before:N0}).", "good");
    }

    /// <summary>Dùng vật phẩm: rương thì mở (kèm kết quả cho thông báo), còn lại như Use.</summary>
    /// <param name="n">Số rương mở một lần (Ctrl + chuột phải: 0 = mở hết); vật phẩm khác luôn dùng 1.</param>
    public (string msg, string kind, CraftResultDto? craft) UseItem(Player p, string itemId, int n = 1)
    {
        if (CultivationManuals.All.FirstOrDefault(m => m.Item == itemId) is { } manual)
        {
            if (p.Count(itemId) < 1) throw new GameException("Không có bí kíp này trong hành trang.");
            if (!CultivationManuals.Available.Contains(manual)) throw new GameException("Công pháp này chưa mở. Bí kíp được giữ nguyên.");
            if (CultivationManuals.IsUnlocked(p, manual.Element)) throw new GameException("Đã học công pháp hệ này; bí kíp được giữ nguyên.");
            CultivationManuals.Unlock(p, manual.Element);
            p.Add(itemId, -1);
            var (message, kind) = Log(p, $"Lĩnh ngộ {manual.Name}: mở vĩnh viễn cây kỹ năng {SkillData.Elements.First(e => e.Id == manual.Element).Name}.", "gold");
            return (message, kind, null);
        }
        if (GameData.ItemById.TryGetValue(itemId, out var d) && d.Type == "chest") return OpenChests(p, d, n);
        if (d?.Type == "eventchest") return OpenEventChest(p, d, n);
        if (d?.Type == "skin") { var (sm, sk) = OpenSkinChest(p, itemId); return (sm, sk, null); }
        if (d?.Type == "egg")
        {
            var occupied=PetStateFor(p).Hatch.Select(h=>h.Slot).ToHashSet();
            var slot=Enumerable.Range(0,PetData.HatchSlots(p.Realm)).FirstOrDefault(s=>!occupied.Contains(s),-1);
            if(slot<0)throw new GameException("Không còn ổ ấp trống.");
            var (message,kind)=Hatch(p,itemId,slot);
            return (message,kind,null);
        }
        var (m, k) = Use(p, itemId);
        return (m, k, null);
    }

    /// <summary>
    /// Mở rương: 1 phần thưởng. Pháp bảo (ô offhand) với tỉ lệ nhỏ; còn lại chia giữa trang bị
    /// trong bảng rơi của cảnh giới hiện tại và đan dược dùng để lên tầng/kỳ của cảnh giới đó.
    /// Rương hiếm: pháp bảo nhiều hơn, trang bị chỉ lấy phẩm cao nhất, đan nhiều hơn.
    /// </summary>
    /// <summary>Mở n rương cùng loại (0 = mở hết), dừng khi hết rương hoặc hành trang đầy; gộp phần thưởng vào một thông báo.</summary>
    private (string msg, string kind, CraftResultDto craft) OpenChests(Player p, ItemDef chest, int n)
    {
        if (p.Count(chest.Id) < 1) throw new GameException("Không có rương này trong hành trang.");
        var want = n <= 0 ? p.Count(chest.Id) : Math.Min(n, p.Count(chest.Id));
        var got = new List<ItemQtyDto>();
        var opened = 0; var full = false;
        while (opened < want)
        {
            var res = OpenOne(p, chest);
            if (res is null) { full = true; break; }
            opened++;
            var i = got.FindIndex(g => g.Item == res.Item);
            if (i >= 0) got[i] = got[i] with { Qty = got[i].Qty + res.Qty }; else got.Add(res);
        }
        if (opened == 0) throw new GameException("Hành trang đầy, dọn bớt rồi mở rương.");
        // món giá trị nhất lên đầu (pháp bảo → phẩm cao → đan)
        int Rank(ItemQtyDto x) { var d = GameData.ItemById[x.Item]; return (d.Slot == "offhand" ? 10 : 0) + Array.IndexOf(GameData.RarityOrder, d.Rarity); }
        got = got.OrderByDescending(Rank).ToList();
        var best = GameData.ItemById[got[0].Item];
        var text = string.Join(", ", got.Select(g => $"{g.Qty} {GameData.Label(GameData.ItemById[g.Item])}{(GameData.ItemById[g.Item].Slot == "offhand" ? " (pháp bảo)" : "")}"));
        var (msg, kind) = Log(p, $"Mở {(opened > 1 ? $"{opened} " : "")}{chest.Name}: nhận {text}.{(full ? " Hành trang đầy, dừng lại." : "")}",
            best.Slot == "offhand" || Array.IndexOf(GameData.RarityOrder, best.Rarity) >= 2 ? "gold" : "loot");
        return (msg, kind, new CraftResultDto("chest:" + chest.Id, got[0], new() { new(chest.Id, opened) }, got));
    }

    /// <summary>Mở 1 rương; null = hành trang đầy (rương được giữ lại).</summary>
    private ItemQtyDto? OpenOne(Player p, ItemDef chest)
    {
        var reward=ChestRewards.Roll(p.Realm,chest.Id,Random.Shared,GameData.ChestDiff(chest.Id),p.Chapter);   // theo mức của rương, không theo mức đang đánh
        var item=reward.Item;var n=reward.Qty;
        p.Add(chest.Id, -1);
        var added = TryAddLoot(p, item, n);
        if (added < n)
        {
            if (added > 0) p.Add(item, -added);
            p.Add(chest.Id, 1);
            return null;
        }
        return new ItemQtyDto(item, n);
    }

    /// <summary>Dùng vật phẩm tiêu hao (đan dược hồi linh khí).</summary>
    public (string msg, string kind) Use(Player p, string itemId)
    {
        if (GameData.ItemById.TryGetValue(itemId, out var el) && el.Type == "elixir") return UseElixir(p, el);
        if (!GameData.ItemById.TryGetValue(itemId, out var def) || def.UseQiSeconds <= 0)
            throw new GameException("Đan dược chỉ dùng khi đột phá.");
        if (p.Count(itemId) < 1) throw new GameException("Không có vật phẩm này trong hành trang.");
        var gain = Rate(p) * def.UseQiSeconds;
        var before = p.Qi;
        p.Qi = Math.Min(QiReq(p), p.Qi + gain);
        p.Add(itemId, -1);
        return Log(p, p.Qi > before ? $"Dùng {def.Name}: +{Math.Floor(p.Qi - before):N0} tu vi." : $"Dùng {def.Name}: tu vi đã đầy bình cảnh.", "good");
    }

    /// <summary>Dùng đan đặc biệt: Tẩy Tủy Đan (tẩy công pháp, hoàn điểm) · Phá Chướng Đan (+tỉ lệ đột phá đại cảnh giới).</summary>
    private (string msg, string kind) UseElixir(Player p, ItemDef def)
    {
        if (p.Count(def.Id) < 1) throw new GameException("Không có vật phẩm này trong hành trang.");
        if (def.Id == "ngo_dao_dan")
        {
            if (p.Realm < def.MinRealm) throw new GameException("Ngộ Đạo Đan chỉ dùng được từ cảnh giới Nguyên Anh.");
            p.ElixirPoints++;
            p.Add(def.Id, -1);
            return Log(p, $"Dùng {def.Name}: +1 điểm công pháp vĩnh viễn.", "gold");
        }
        if (def.Id == "tay_tuy_dan")
        {
            var refund = SkillPointsSpent(p);
            if (refund <= 0) throw new GameException("Chưa lĩnh ngộ công pháp nào để tẩy.");
            p.SetSkillLevels(new());                                   // linh căn bẩm sinh tự giữ cấp 1 (SkillLevels)
            var lv = SkillLevels(p);
            p.SkillSlots = string.Join(',', p.Slots().Select(id => id.Length > 0 && SkillData.ById.TryGetValue(id, out var sk)
                && (sk.Tree == "basic" || lv.GetValueOrDefault(id) > 0) ? id : ""));
            p.Add(def.Id, -1);
            return Log(p, $"Dùng {def.Name}: kinh mạch gột sạch, hoàn lại {refund} điểm kỹ năng để lĩnh ngộ lại.", "gold");
        }
        var r = GameData.BreakPillRealm(def.Id);
        if (r >= 0)
        {
            if (p.Realm != r)
                throw new GameException($"{def.Name} chỉ dùng được ở cảnh giới {GameData.Realms[r].Name}.");
            if (NextRealm(p) is null) throw new GameException($"Cảnh giới {GameData.LockedRealms[0]} chưa khai mở.");
            if (p.PillBonus >= GameData.BreakPillMax - 1e-9)
                throw new GameException($"Dược lực đã đạt tối đa +{GameData.BreakPillMax * 100:0}%, đột phá trước rồi dùng tiếp.");
            p.PillBonus = Math.Round(Math.Min(GameData.BreakPillMax, p.PillBonus + GameData.BreakPillStep), 2);
            p.Add(def.Id, -1);
            return Log(p, $"Dùng {def.Name}: tỉ lệ đột phá {GameData.Realms[r + 1].Name} +{p.PillBonus * 100:0}% (tối đa +{GameData.BreakPillMax * 100:0}%).", "good");
        }
        throw new GameException("Đan này chưa dùng được.");
    }

    /// <summary>Trang phục đang hiệu lực: bỏ qua skin không còn tồn tại hoặc chưa đủ cảnh giới (vd. admin hạ cảnh giới).</summary>
    private static string CurrentSkin(Player p) =>
        GameData.SkinById.TryGetValue(p.Skin ?? "", out var s) && SkinWearable(p, s) ? s.Id : "";
    private static string CurrentGender(Player p) =>
        GameData.SkinById.TryGetValue(CurrentSkin(p), out var s) ? s.Gender : p.Gender == "female" ? "female" : "male";

    public (string msg, string kind) SetGender(Player p, string? gender)
    {
        if (gender is not ("male" or "female")) throw new GameException("Giới tính không hợp lệ.");
        var current = CurrentSkin(p);
        if (GameData.SkinById.TryGetValue(current, out var s) && s.Gender != gender)
        {
            var counterpart = gender == "female" ? s.Id + "_nu" : s.Id.Replace("_nu", "");
            p.Skin = GameData.SkinById.TryGetValue(counterpart, out var next) && SkinWearable(p, next) ? next.Id : "";
        }
        else if (current.Length == 0) p.Skin = "";
        p.Gender = gender;
        return (gender == "female" ? "Đã chuyển ngoại hình nữ." : "Đã chuyển ngoại hình nam.", "");
    }
    private static IEnumerable<string> UnlockedSkins(Player p) => (p.SkinsUnlocked ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
    /// <summary>Trang phục không cần rương thì luôn mở; cần rương thì phải đã mở rương đó.</summary>
    private static bool SkinOpen(Player p, SkinDef s) => s.UnlockItem is null || UnlockedSkins(p).Contains(s.Id);
    /// <summary>Mặc được: trang phục mở bằng rương thì không cần cảnh giới; trang phục theo cảnh giới thì phải đạt MinRealm.</summary>
    private static bool SkinWearable(Player p, SkinDef s) => SkinOpen(p, s) && (s.UnlockItem is not null || p.Realm >= s.MinRealm);

    /// <summary>Mở rương trang phục: mở khóa vĩnh viễn trang phục tương ứng, tốn 1 rương.</summary>
    private (string msg, string kind) OpenSkinChest(Player p, string itemId)
    {
        if (p.Count(itemId) < 1) throw new GameException("Không có rương này trong hành trang.");
        var s = GameData.Skins.FirstOrDefault(x => x.UnlockItem == itemId) ?? throw new GameException("Rương trang phục này chưa gắn với trang phục nào.");
        if (SkinOpen(p, s)) throw new GameException($"Đã mở khóa {s.Name}; rương được giữ nguyên.");
        p.SkinsUnlocked = string.Join(',', UnlockedSkins(p).Append(s.Id));
        p.Add(itemId, -1);
        return Log(p, $"Mở {GameData.ItemById[itemId].Name}: mở khóa trang phục {s.Name}, mặc được ngay.", "gold");
    }

    /// <summary>Đổi trang phục (rỗng = tự đổi theo cảnh giới).</summary>
    public (string msg, string kind) SetSkin(Player p, string? skin)
    {
        skin ??= "";
        if (skin.Length == 0) { p.Gender = CurrentGender(p); p.Skin = ""; return ("Trang phục: tự đổi theo cảnh giới.", ""); }
        if (!GameData.SkinById.TryGetValue(skin, out var s)) throw new GameException("Trang phục không tồn tại.");
        if (!SkinOpen(p, s)) throw new GameException($"Mở {GameData.ItemById[s.UnlockItem!].Name} để dùng trang phục {s.Name}.");
        if (!SkinWearable(p, s)) throw new GameException($"Cần đạt cảnh giới {GameData.Realms[s.MinRealm].Name} để mặc {s.Name}.");
        p.Skin = s.UnlockItem is null && s.HeroStand == Realm(p).HeroStand ? "" : s.Id;
        p.Gender = s.Gender;
        return ($"Đã thay trang phục {s.Name}.", "");
    }

    /// <summary>Bỏ vật phẩm khỏi túi.</summary>
    public (string msg, string kind) Drop(Player p, string itemId, int n)
    {
        if (!GameData.ItemById.TryGetValue(itemId, out var def)) throw new GameException("Vật phẩm không tồn tại.");
        n = Math.Clamp(n, 1, p.Count(itemId));
        if (p.Count(itemId) < 1) throw new GameException("Không có vật phẩm này trong hành trang.");
        p.Add(itemId, -n);
        return ($"Đã bỏ {n} {def.Name}.", "");
    }

    /// <summary>Bán trang bị trong hành trang lấy linh thạch (món đang mặc không bán được).</summary>
    public (string msg, string kind) Sell(Player p, string itemId, int n)
    {
        if (!GameData.ItemById.TryGetValue(itemId, out var def) || def.Sell <= 0) throw new GameException("Vật phẩm này không bán được.");
        if (p.Count(itemId) < 1) throw new GameException("Không có món này trong hành trang.");
        n = Math.Clamp(n, 1, p.Count(itemId));
        var gain=(long)def.Sell*n;
        if(gain>int.MaxValue-(long)p.Count("linh_thach"))throw new GameException("Tổng linh thạch vượt giới hạn. Hãy bán ít món hơn.");
        p.Add(itemId, -n);
        p.Add("linh_thach", (int)gain);
        return Log(p, $"Bán {(n > 1 ? n + " " : "")}{def.Name}: +{gain:N0} linh thạch.", "loot");
    }

    /// <summary>Bán hết trang bị trong hành trang có độ hiếm từ maxRarity trở xuống (0 = Phàm cấp … 4 = Thiên cấp).</summary>
    /// <summary>Bán hàng loạt theo độ hiếm (≤ maxRarity). gearOnly: chỉ trang bị.
    /// items: giới hạn trong các món "id:số,id:số" (vd. trang hành trang đang xem); null = cả hành trang.
    /// Đồ đang mặc không nằm trong hành trang nên không bao giờ bị bán.</summary>
    /// pick: các món người chơi tự chọn (chọn nhiều trong hành trang).
    public (string msg, string kind) SellBulk(Player p, int maxRarity, bool gearOnly = false, string? items = null, bool pick = false)
    {
        Dictionary<string, int>? scope = null;
        if (!string.IsNullOrWhiteSpace(items))
        {
            scope = new();
            foreach (var part in items.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split(':');
                if (kv.Length == 2 && int.TryParse(kv[1], out var c) && c > 0) scope[kv[0]] = scope.GetValueOrDefault(kv[0]) + c;
            }
        }
        var list = p.Inventory.Where(i => i.Count > 0 && GameData.ItemById.TryGetValue(i.ItemId, out var d) && d.Sell > 0
                && (!gearOnly || d.Slot is not null)
                && (pick || d.Tier == 0)          // đan dược chỉ bán khi người chơi tự chọn, không bán theo phẩm hàng loạt
                && (pick || d.Type != "skin")      // rương trang phục cũng vậy, tránh bán nhầm khi bán nhanh theo phẩm
                && (scope is null || scope.ContainsKey(i.ItemId))
                && Array.IndexOf(GameData.RarityOrder, d.Rarity) <= maxRarity).ToList();
        long count = 0, gain = 0;
        foreach (var i in list)
        {
            var d = GameData.ItemById[i.ItemId];
            var n = scope is null ? i.Count : Math.Min(i.Count, scope[i.ItemId]);
            count += n; gain += (long)d.Sell * n;
        }
        if (count == 0) throw new GameException(gearOnly ? "Không có trang bị nào để bán." : "Không có món nào để bán.");
        if(gain>int.MaxValue-(long)p.Count("linh_thach"))throw new GameException("Tổng linh thạch vượt giới hạn. Hãy bán ít món hơn.");
        foreach(var i in list){var n=scope is null?i.Count:Math.Min(i.Count,scope[i.ItemId]);p.Add(i.ItemId,-n);}
        p.Add("linh_thach", (int)gain);
        return Log(p, $"Bán {count} {(gearOnly ? "món trang bị" : "món")}{(scope is null ? "" : pick ? " đã chọn" : " (trang đang xem)")}: +{gain:N0} linh thạch.", "loot");
    }

    /// <summary>Cấp hiển thị: đếm liên tục qua các tầng/kỳ của mọi cảnh giới.</summary>
    public static int Level(Player p) => GameData.Realms.Take(p.Realm).Sum(r => r.Stages.Length) + p.Stage + 1;

    public (string msg, string kind) Unequip(Player p, string slotId)
    {
        var slot = p.Equipment.FirstOrDefault(e => e.Slot == slotId) ?? throw new GameException("Ô này đang trống.");
        if (BagUsed(p) >= BagCap(p)) throw new GameException("Hành trang đầy, không tháo được.");
        p.Add(slot.ItemId, 1);
        p.Equipment.Remove(slot);
        db.Equipment.Remove(slot);
        return ($"Đã tháo {GameData.ItemById[slot.ItemId].Name}.", "");
    }

    public (string msg, string kind) Advance(Player p)
    {
        var r = Realm(p);
        if (p.Qi < QiReq(p)) throw new GameException("Tu vi chưa đủ.");
        var cost = TargetCost(p);

        if (!IsPeak(p))
        {
            if (!p.Has(cost)) throw new GameException("Thiếu vật liệu.");
            p.Pay(cost);
            p.Stage++;
            p.Qi = 0;
            return Log(p, $"Đột phá thành công: {r.Name} {r.Stages[p.Stage]}, +{SkillData.PointsForBreak(false)} điểm kỹ năng.", "good");
        }

        var next = NextRealm(p) ?? throw new GameException($"Cảnh giới {GameData.LockedRealms[0]} chưa khai mở.");
        if (!p.Has(cost)) throw new GameException(cost.TryGetValue(r.BossItem, out var need) && p.Count(r.BossItem) < need
            ? $"Thiếu {GameData.ItemById[r.BossItem].Name} ({p.Count(r.BossItem)}/{need}): đánh bại Boss {(GameData.ChapterDropping(r.BossItem)?.Boss.Name ?? r.Boss.Name)} (chương {(GameData.ChapterDropping(r.BossItem)?.Index ?? p.Realm) + 1}{(GameData.BossItemDifficulty(r.BossItem) > 0 ? " · " + GameData.DiffNames[GameData.BossItemDifficulty(r.BossItem)] : "")}) để nhận."
            : "Thiếu vật liệu.");
        var now = DateTime.UtcNow;
        if (p.Cheat("break")) { p.DebuffUntil = p.BreakCooldownUntil = DateTime.MinValue; }   // admin: bỏ điều tức
        if (now < p.DebuffUntil) throw new GameException($"Kinh mạch còn hỗn loạn sau tẩu hỏa nhập ma, cần điều tức thêm {Math.Ceiling((p.DebuffUntil - now).TotalSeconds)} giây.");
        if (now < p.BreakCooldownUntil) throw new GameException($"Khí tức chưa ổn định, chờ thêm {Math.Ceiling((p.BreakCooldownUntil - now).TotalSeconds)} giây.");
        var chance = Chance(p);
        var roll = Random.Shared.NextDouble();

        if (roll >= chance)
        {
            // thất bại: mất đan và linh thạch, yêu hạch Boss được giữ lại
            p.Pay(cost.Where(kv => kv.Key != r.BossItem).ToDictionary(kv => kv.Key, kv => kv.Value));
            var major = roll >= 1 - (1 - chance) * GameData.BreakMajorShare;
            var loss = major ? GameData.BreakMajorLoss : GameData.BreakFailLoss;
            p.Qi = Math.Max(0, p.Qi - QiReq(p) * loss);
            p.Bonus = Math.Round(p.Bonus + 0.1, 2);
            p.BreakCooldownUntil = now.AddSeconds(major ? GameData.BreakMajorCooldown : GameData.BreakFailCooldown);
            if (major) p.DebuffUntil = now.AddSeconds(GameData.DebuffSeconds);
            return Log(p, major
                ? $"Tẩu hỏa nhập ma! Đột phá {next.Name} thất bại, tổn {loss * 100:0}% tu vi, suy yếu {GameData.DebuffSeconds / 60:0} phút. Lần sau +{p.Bonus * 100:0}% tỉ lệ."
                : $"Linh khí phản phệ, đột phá {next.Name} thất bại. Tổn {loss * 100:0}% tu vi, lần sau +{p.Bonus * 100:0}% tỉ lệ.", major ? "major" : "bad");
        }

        p.Pay(cost);
        var crit = roll < chance * GameData.BreakCritShare;
        {
            // Standard realm outfits follow the new realm; chest costumes stay equipped.
            if (GameData.SkinById.TryGetValue(p.Skin ?? "", out var outfit) && outfit.UnlockItem is null)
                p.Skin = "";
            p.Realm++;
            p.Stage = 0;
            p.Qi = 0;
            p.Bonus = 0;
            p.PillBonus = 0;
            p.ExploreReadyAt = DateTime.UtcNow.AddSeconds(ExploreSeconds(p));
            if (crit)
            {
                var nb = GameData.BaseStats(next, 0);
                p.BodyHp += (int)Math.Round(nb.Hp * GameData.BreakCritBonus);
                p.BodyDef += (int)Math.Round(nb.Def * GameData.BreakCritBonus);
                return Log(p, $"Đại thành công! Đạo cơ vững chắc, bước vào {next.Name}: vĩnh viễn +{nb.Hp * GameData.BreakCritBonus:0} máu, +{nb.Def * GameData.BreakCritBonus:0} phòng thủ, +{SkillData.PointsForBreak(true)} điểm kỹ năng.", "crit");
            }
            return Log(p, $"Thiên địa dị tượng! Đột phá thành công cảnh giới {next.Name}, +{SkillData.PointsForBreak(true)} điểm kỹ năng.", "gold");
        }
    }

    /// <summary>Thay đổi khi đột phá kế tiếp: chỉ số gốc, tốc độ tu luyện, ô hành trang, ô trang bị và chỉ số phụ mới mở.</summary>
    private static BreakPreviewDto BreakPreview(Player p)
    {
        var r = Realm(p);
        var next = IsPeak(p) ? NextRealm(p) : r;
        if (next is null) return new BreakPreviewDto(0, 0, 0, 0, 0, Array.Empty<string>(), Array.Empty<string>());
        var ns = IsPeak(p) ? 0 : p.Stage + 1;
        var a = GameData.BaseStats(r, p.Stage); var b = GameData.BaseStats(next, ns);
        var rate = GameData.QiRate(r, p.Stage); var rate2 = GameData.QiRate(next, ns);
        var slots = next.EquipSlots.Except(r.EquipSlots).Select(s => GameData.Slots.First(x => x.Id == s).Name).ToArray();
        var stats = next == r ? Array.Empty<string>() : GameData.ExtraStats.Where(x => x.MinRealm == p.Realm + 1).Select(x => x.Name).ToArray();
        return new BreakPreviewDto(b.Hp - a.Hp, b.Atk - a.Atk, b.Def - a.Def, Math.Round((rate2 / rate - 1) * 100), next.BagSlots - r.BagSlots, slots, stats,
            SkillData.PointsForBreak(next != r));
    }

    // ================= Admin mode =================

    public static readonly string[] AdminFlags = { "god", "break", "fast", "fast05", "fast2", "fast3", "fast5", "boss", "map", "petevo" };
    public async Task<(string? msg, string? kind)> AdminClearDataFlagAsync(Player p, string? targetId = null)
    {
        if (!Player.AdminEnabled) throw new GameException("Chỉ admin được gỡ cờ dữ liệu.");
        if (!string.IsNullOrWhiteSpace(targetId))
        {
            if (!Guid.TryParse(targetId, out var target) || target == Guid.Empty) throw new GameException("ID người chơi không hợp lệ.");
            if (target != p.Id)
            {
                if (lb is null) throw new GameException("Chưa cấu hình bảng xếp hạng online.");
                await lb.ClearDataFlagAsync(target);
                var local = await db.Players.SingleOrDefaultAsync(x => x.Id == target);
                if (local is not null) local.Flagged = false;
                return Log(p, $"[Admin] Đã gỡ cờ cảnh báo dữ liệu cho người chơi {target}.", "good");
            }
        }
        if (lb is not null) await lb.ClearDataFlagAsync(p);
        p.Flagged = false;
        return Log(p, "[Admin] Đã gỡ cờ cảnh báo dữ liệu. Giữ nguyên trạng thái từng sử dụng admin.", "good");
    }
    /// <summary>Admin tốc độ thám hiểm: hệ số chia thời gian chuyến (×0.5 chậm gấp đôi · ×2 · ×3 · ×5; cờ cũ "fast" = ×3), 1 = tắt.</summary>
    private static double FastMult(Player p) => p.Cheat("fast5") ? 5 : p.Cheat("fast3") || p.Cheat("fast") ? 3 : p.Cheat("fast2") ? 2 : p.Cheat("fast05") ? .5 : 1;

    /// <summary>Lệnh admin: give · petkit · petstar · stones · qi · realm · clearcd · points · maxfire · resetskills · resetall · unlock · explore · flag.</summary>
    public (string msg, string kind) AdminAction(Player p, string action, string? arg, int n)
    {
        n = Math.Clamp(n, 1, 1_000_000_000);
        // mọi lệnh admin (trừ reset về từ đầu) loại nhân vật khỏi bảng xếp hạng
        if (action != "resetall") p.Tainted = true;
        switch (action)
        {
            case "give":
                if (arg is null || !GameData.ItemById.TryGetValue(arg, out var it)) throw new GameException("Vật phẩm không tồn tại.");
                p.Add(arg, n);
                return Log(p, $"[Admin] +{n:N0} {GameData.Label(it)}.", "good");
            case "petkit":
                // mọi Thú Đản + đan dược / nguyên liệu linh thú, mỗi loại n cái
                foreach (var pi in PetData.Items.Concat(PetTraining.Items)) p.Add(pi.Id, n);
                return Log(p, $"[Admin] +{n:N0} mỗi loại Thú Đản, đan dược và trang bị linh thú ({PetData.Items.Length + PetTraining.Items.Length} loại).", "good");
            case "petstar":
                return AdminPetStar(p, arg, n);
            case "petlevel":
                return AdminPetLevel(p, arg, n);
            case "giveset":
            {
                // arg = "<tên bộ>|<phẩm 0–3>": nhận n món mỗi loại của cả bộ (kể cả pháp bảo trong bộ) ở phẩm đã chọn
                var parts = (arg ?? "").Split('|');
                if (parts.Length != 2 || !int.TryParse(parts[1], out var q) || q < 0 || q >= GameData.QualityMult.Length) throw new GameException("Bộ / phẩm không hợp lệ.");
                var bases = GameData.GearBases.Where(b => b.Set == parts[0]).ToList();
                if (bases.Count == 0) throw new GameException("Bộ trang bị không tồn tại.");
                foreach (var b in bases) p.Add(GameData.GearId(b, q), n);
                return Log(p, $"[Admin] +{n:N0} bộ {parts[0]} [cấp {GameData.GearLevelOf(bases[0])} · {GameData.QualityName[q]}] ({bases.Count} món).", "good");
            }
            case "eventpts":
                SyncEvent(p);
                p.EventPoints += n;
                return Log(p, $"[Admin] +{n:N0} điểm sự kiện Thiên Cơ (đang có {p.EventPoints:N0}).", "good");
            case "stones":
                p.Add("linh_thach", n);
                return Log(p, $"[Admin] +{n:N0} linh thạch.", "good");
            case "qi":
                p.Qi = QiReq(p);
                return Log(p, "[Admin] Tu vi đầy bình cảnh.", "good");
            case "realm":
            {
                var parts = (arg ?? "").Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[0], out var ri) || !int.TryParse(parts[1], out var si)
                    || ri < 0 || ri >= GameData.Realms.Count || si < 0 || si >= GameData.Realms[ri].Stages.Length)
                    throw new GameException("Cảnh giới / tầng không hợp lệ.");
                p.Realm = ri; p.Stage = si; p.Qi = 0; p.Bonus = 0; p.PillBonus = 0;
                p.ExploreReadyAt = DateTime.UtcNow.AddSeconds(ExploreSeconds(p));
                foreach (var e in p.Equipment.Where(e => !Realm(p).EquipSlots.Contains(e.Slot)).ToList()) { p.Add(e.ItemId, 1); p.Equipment.Remove(e); db.Equipment.Remove(e); }
                return Log(p, $"[Admin] Chuyển tới {Realm(p).Name} {Realm(p).Stages[si]}.", "gold");
            }
            case "clearcd":
                p.BreakCooldownUntil = p.DebuffUntil = DateTime.MinValue;
                return Log(p, "[Admin] Đã xóa điều tức và suy yếu.", "good");
            case "points":
                p.AdminPoints += n;
                return Log(p, $"[Admin] +{n} điểm kỹ năng.", "good");
            case "maxfire":
            case "maxskill":
            {
                // học tối đa cây kỹ năng một hệ (arg = id hệ, "all" = mọi hệ đã mở); maxfire giữ cho nút cũ = Hỏa
                var el = action == "maxfire" ? "hoa" : arg ?? "";
                var picked = SkillData.Elements.Where(e => e.Open && (el == "all" || e.Id == el)).ToArray();
                if (picked.Length == 0) throw new GameException("Hệ công pháp không hợp lệ.");
                var lv = SkillLevels(p);
                foreach (var e in picked)
                {
                    CultivationManuals.Unlock(p, e.Id);
                    foreach (var s in SkillData.Skills.Where(s => s.Element == e.Id)) lv[s.Id] = s.MaxLv;
                }
                p.SetSkillLevels(lv);
                if (SkillPoints(p) < 0) p.AdminPoints += -SkillPoints(p);
                return Log(p, $"[Admin] Học tối đa toàn bộ cây {(el == "all" ? "mọi hệ" : picked[0].Name + " hệ")}.", "good");
            }
            case "resetskills":
                p.SetSkillLevels(new Dictionary<string, int> { [SkillData.StartSkill] = 1 });
                p.SkillSlots = "";
                p.AdminPoints = 0;                                   // về đúng số điểm kiếm được
                return Log(p, "[Admin] Đã reset cây kỹ năng, hoàn toàn bộ điểm.", "good");
            case "petmax":
                foreach(var pet in p.Pets){pet.Level=100;pet.Exp=0;pet.Form=2;}
                return Log(p,"[Admin] Linh thú đạt cấp 100, thần thú.","gold");
            case "resetall":
            {
                // về đúng trạng thái nhân vật mới tạo: giữ Id, tên, ngày tạo và cờ admin mode
                var now = DateTime.UtcNow;
                p.Realm = 0; p.Stage = 0; p.Qi = 0; p.Bonus = 0; p.PillBonus = 0; p.LearnedManuals = ""; p.ManualVersion = 1; p.ActiveManual = "";
                p.Area = 1; p.AreaMax = 1; p.Chapter = 0; p.ChapterMax = 0; p.Diff = 0; p.DiffProg = ""; p.Cleared = ""; p.BossKills = 0;
                p.BreakCooldownUntil = p.DebuffUntil = DateTime.MinValue;
                p.Skills = ""; p.SkillSlots = ""; p.AdminPoints = 0; p.Redeemed = ""; p.SpinHistory = ""; p.SkinsUnlocked = "";
                p.Hatch=""; db.Pets.RemoveRange(p.Pets); p.Pets.Clear();
                p.ElixirPoints = 0;
                p.AlchemyExp = 0; p.RefineRecipe = -1; p.RefineLeft = 0; p.RefineCatalyst = false;
                p.RefineStartAt = p.RefineEndAt = now; p.LastRefine = ""; p.LastRefineAt = DateTime.MinValue;
                p.BodyHp = 0; p.BodyDef = 0;
                p.LastTickAt = now;
                foreach (var e in p.Equipment.ToList()) { p.Equipment.Remove(e); db.Equipment.Remove(e); }
                // giữ lại dòng của vật phẩm khởi đầu (tránh EF theo dõi trùng khóa khi xóa rồi thêm lại), xóa phần còn lại
                foreach (var i in p.Inventory.Where(i => !GameData.StartingInventory.ContainsKey(i.ItemId)).ToList()) { p.Inventory.Remove(i); db.Inventory.Remove(i); }
                foreach (var i in p.Inventory) i.Count = 0;
                foreach (var (item, cnt) in GameData.StartingInventory) p.Add(item, cnt);
                p.Logs.Clear();
                db.Logs.Where(l => l.PlayerId == p.Id).ExecuteDelete();
                p.ExploreReadyAt = now.AddSeconds(ExploreSeconds(p));
                p.Flagged = false;                // tu lại từ đầu: bỏ dấu "từng mở bằng bản cũ"
                p.Tainted = p.Admin.Length > 0;   // tu lại từ đầu sạch (không còn cờ admin) → được lên bảng xếp hạng lại
                AddLog(p, "Đạo tâm vừa khởi. Nhân vật sẽ tự đi thám hiểm, nhặt linh tài và tích tu vi.");
                return Log(p, "[Admin] Đã reset nhân vật về từ đầu.", "gold");
            }
            case "unlock":
            {
                // mở hết ải chương đang đánh; đang ở chương cao nhất đã mở và đã mở hết ải thì mở thêm chương kế
                var c = Chap(p);
                if (p.Diff > 0)                                             // Khó / Ác mộng: mở hết ải chương đang đánh, đã mở hết thì đánh dấu đã hạ Boss (mở mức kế)
                {
                    if (MapAreaMax(p) >= GameData.Areas) { MarkCleared(p, p.Diff, c.Index); return Log(p, $"[Admin] Đánh dấu đã hạ Boss chương {c.Index + 1} mức {GameData.DiffNames[p.Diff]}.", "good"); }
                    SetDiffProg(p, p.Diff, c.Index, GameData.Areas); p.Add(c.KeyItem, 1);
                    return Log(p, $"[Admin] Mở toàn bộ ải {DiffTag(p)}{c.Index + 1}-1 → {c.Index + 1}-{GameData.Areas}, +1 {GameData.ItemById[c.KeyItem].Name}.", "good");
                }
                if (p.Chapter == p.ChapterMax && p.AreaMax >= GameData.Areas && !IsCleared(p, 0, c.Index)) { MarkCleared(p, 0, c.Index); }
                if (p.Chapter == p.ChapterMax && p.AreaMax >= GameData.Areas && p.ChapterMax + 1 < GameData.OpenChapters) { p.ChapterMax++; p.AreaMax = 1; p.Chapter = p.ChapterMax; p.Area = 1; return Log(p, $"[Admin] Mở chương {p.ChapterMax + 1} · {Chap(p).Name}.", "good"); }
                if (p.Chapter == p.ChapterMax) p.AreaMax = GameData.Areas;
                p.Add(c.KeyItem, 1);
                return Log(p, $"[Admin] Mở toàn bộ ải {c.Index + 1}-1 → {c.Index + 1}-{GameData.Areas}, +1 {GameData.ItemById[c.KeyItem].Name}.", "good");
            }
            case "explore":
                p.ExploreReadyAt = DateTime.UtcNow;
                return Log(p, "[Admin] Chuyến thám hiểm kết thúc ngay.", "good");
            case "flag":
            {
                if (arg is null || !AdminFlags.Contains(arg)) throw new GameException("Cờ admin không hợp lệ.");
                var set = p.Admin.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                var on = set.Add(arg); if (!on) set.Remove(arg);
                var fast = arg.StartsWith("fast");
                if (fast && on) set.RemoveWhere(f => f.StartsWith("fast") && f != arg);   // ×0.5 / ×2 / ×3 / ×5: chỉ bật một mức
                p.Admin = string.Join(',', set);
                if (fast) p.ExploreReadyAt = DateTime.UtcNow.AddSeconds(ExploreSeconds(p));
                if (arg == "map" && !on)                                    // tắt full bản đồ: đang đứng ở chương / ải chưa mở thật → về chỗ đã mở
                {
                    if (!ChapterOpen(p, p.Diff, p.Chapter)) { p.Diff = 0; p.Chapter = Math.Clamp(p.Chapter, 0, p.ChapterMax); }
                    if (p.Area > MapAreaMax(p)) p.Area = MapAreaMax(p);
                }
                var name = arg == "god" ? "Bất tử" : arg == "break" ? "Đột phá luôn thành công" : arg == "boss" ? "Đánh Boss không cần chìa khóa"
                    : arg == "map" ? "Mở full bản đồ" : arg == "petevo" ? "Tiến hóa linh thú không cần điều kiện"
                    : $"Tốc độ thám hiểm ×{(arg == "fast" ? "3" : arg == "fast05" ? "0.5" : arg[4..])}";
                return Log(p, $"[Admin] {name}: {(on ? "BẬT" : "TẮT")}.", on ? "good" : "");
            }
            default: throw new GameException("Lệnh admin không tồn tại.");
        }
    }

    // ================= Công pháp =================

    public static Dictionary<string, int> SkillLevels(Player p)
    {
        var d = p.SkillLevels().Where(kv => SkillData.ById.TryGetValue(kv.Key, out var skill)
            && SkillData.Elements.Any(e => e.Id == skill.Element && e.Open)).ToDictionary(kv => kv.Key, kv => kv.Value);
        if (!d.ContainsKey(SkillData.StartSkill)) d[SkillData.StartSkill] = 1;   // linh căn bẩm sinh
        return d;
    }

    public static int SkillPointsSpent(Player p) =>
        SkillLevels(p).Sum(kv => SkillData.ById.TryGetValue(kv.Key, out var s) && s.Tree != "basic"   // đòn thường không dùng điểm
            ? Enumerable.Range(0, kv.Value).Sum(l => s.Id == SkillData.StartSkill && l == 0 ? 0 : s.CostAt(l)) : 0);

    public static int SkillPoints(Player p) => SkillData.PointsEarned(Level(p), p.Realm, p.BossKills) + p.ElixirPoints + p.AdminPoints - SkillPointsSpent(p);

    /// <summary>Nâng một kỹ năng lên 1 cấp: hệ đã mở, đủ cảnh giới, đủ kỹ năng tiền đề, đủ điểm.</summary>
    public (string msg, string kind) UpgradeSkill(Player p, string id)
    {
        if (!SkillData.ById.TryGetValue(id, out var s)) throw new GameException("Kỹ năng không tồn tại.");
        if (!CultivationManuals.IsUnlocked(p, s.Element)) throw new GameException("Cần học bí kíp công pháp của hệ này trước.");
        var lv = SkillLevels(p);
        if (s.Tree == "basic") throw new GameException($"{s.Name} là đòn đánh thường, luôn sẵn có, không dùng điểm kỹ năng.");
        var cur = lv.GetValueOrDefault(id);
        if (cur >= s.MaxLv) throw new GameException($"{s.Name} đã đạt cấp tối đa.");
        if (p.Realm < s.MinRealm) throw new GameException($"Cần cảnh giới {GameData.Realms[s.MinRealm].Name}.");
        foreach (var r in s.Req)
        {
            var (rid, rlv) = (r.Split(':')[0], int.Parse(r.Split(':')[1]));
            if (lv.GetValueOrDefault(rid) < rlv) throw new GameException($"Cần {SkillData.ById[rid].Name} cấp {rlv}.");
        }
        var cost = s.CostAt(cur);
        if (SkillPoints(p) < cost) throw new GameException($"Cần {cost} điểm kỹ năng.");
        lv[id] = cur + 1;
        p.SetSkillLevels(lv);
        if (cur == 0)
        {
            // lĩnh ngộ chiêu mới: tự đặt vào ô trống phù hợp
            var slots = p.Slots();
            var free = Enumerable.Range(0, SkillData.SkillSlotCount).FirstOrDefault(i => slots[i] == "" && SkillData.FitsSlot(s, i), -1);
            if (free >= 0) { slots[free] = id; p.SkillSlots = string.Join(',', slots); }
        }
        return Log(p, cur == 0 ? $"Lĩnh ngộ công pháp {s.Name}." : $"{s.Name} tăng lên cấp {cur + 1}.", "good");
    }

    /// <summary>Đặt kỹ năng vào ô (skill rỗng = tháo). Kỹ năng đã nằm ở ô khác sẽ được đổi chỗ.</summary>
    public (string msg, string kind) SetSkillSlot(Player p, int slot, string? skill)
    {
        if (slot < 0 || slot >= SkillData.SkillSlotCount) throw new GameException("Ô kỹ năng không hợp lệ.");
        if (slot == SkillData.BasicSlot)
        {
            // ô đòn thường đi theo công pháp đang kích hoạt: chọn đòn thường hệ khác = kích hoạt hệ đó
            if (string.IsNullOrEmpty(skill)) throw new GameException("Đòn thường đi theo công pháp đang kích hoạt, không tháo được.");
            if (!SkillData.ById.TryGetValue(skill, out var b) || b.Tree != "basic") throw new GameException("Ô đòn thường chỉ đặt đòn đánh thường.");
            return ActivateManual(p, b.Element);
        }
        var slots = p.Slots();
        if (string.IsNullOrEmpty(skill))
        {
            var old = slots[slot]; slots[slot] = "";
            p.SkillSlots = string.Join(',', slots);
            return (old.Length > 0 && SkillData.ById.TryGetValue(old, out var od) ? $"Tháo {od.Name}." : "", "");
        }
        if (!SkillData.ById.TryGetValue(skill, out var s)) throw new GameException("Kỹ năng không tồn tại.");
        if (!CultivationManuals.IsUnlocked(p, s.Element)) throw new GameException("Cần học bí kíp công pháp của hệ này trước.");
        if (s.Tree != "basic" && SkillLevels(p).GetValueOrDefault(skill) < 1) throw new GameException($"Chưa lĩnh ngộ {s.Name}.");
        if (!SkillData.FitsSlot(s, slot))
            throw new GameException(s.Category is "Căn cơ" or "Bị động" ? $"{s.Name} là bị động, luôn có hiệu lực, không cần đặt vào ô."
                : slot == SkillData.BasicSlot ? "Ô đòn thường chỉ đặt đòn đánh thường của công pháp đã học." : $"{s.Name} là đòn đánh thường, đặt ở ô Đòn thường.");
        var at = Array.IndexOf(slots, skill);
        if (at >= 0 && at != slot) slots[at] = SkillData.FitsSlot(SkillData.ById.GetValueOrDefault(slots[slot]) ?? s, at) ? slots[slot] : "";
        slots[slot] = skill;
        p.SkillSlots = string.Join(',', slots);
        return ($"Trang bị {s.Name} vào ô {(slot == SkillData.BasicSlot ? "Đòn thường" : "Kỹ năng")}.", "good");
    }

    /// <summary>Kích hoạt công pháp một hệ: đổi đòn đánh thường sang hệ đó; bị động hệ khác chỉ còn InactiveRate. Đổi tự do.</summary>
    public (string msg, string kind) ActivateManual(Player p, string element)
    {
        var manual = CultivationManuals.All.FirstOrDefault(m => m.Element == element) ?? throw new GameException("Công pháp không tồn tại.");
        if (!CultivationManuals.IsUnlocked(p, element)) throw new GameException("Cần học bí kíp công pháp của hệ này trước.");
        if (CultivationManuals.BasicOf(element) is not { } basic) throw new GameException("Hệ này chưa có đòn đánh thường.");
        if (CultivationManuals.Active(p) == element)
        {
            // A first learned manual can be the computed fallback before a state refresh.
            // Persist it and its basic even when no manual switch is necessary.
            p.ActiveManual = element;
            CultivationManuals.SyncActive(p);
            return ($"Đang kích hoạt {manual.Name}.", "");
        }
        var before = Power(p);
        p.ActiveManual = element;
        CultivationManuals.SyncActive(p);
        var after = Power(p);
        return Log(p, $"Kích hoạt {manual.Name}: đòn thường đổi thành {basic.Name}; bị động hệ khác còn {CultivationManuals.InactiveRate:P0}. Chiến lực {before:N0} → {after:N0}.", "good");
    }

    /// <summary>Tổng hiệu quả bị động của các kỹ năng đã học (đơn vị %); hệ không kích hoạt chỉ tính InactiveRate.</summary>
    private static (double atk, double def, double hp, double crit, double speed, double qi, double monAtk) SkillBonus(Player p)
    {
        double a = 0, d = 0, h = 0, c = 0, sp = 0, q = 0, m = 0;
        var active = CultivationManuals.Active(p);
        foreach (var (id, l) in SkillLevels(p))
        {
            if (!SkillData.ById.TryGetValue(id, out var s) || s.Tree == "basic") continue;
            var f = s.Factor(l) * CultivationManuals.Rate(active, s.Element);
            a += s.AtkPct * f; d += s.DefPct * f; h += s.HpPct * f; c += s.Crit * f; sp += s.Speed * f; q += s.QiPct * f; m += SkillSecondaryEffects.DebuffEnabled(active, s.Element) ? s.MonAtkDown * f : 0;
        }
        return (a, d, h, c, sp, q, Math.Min(40, m));
    }

    /// <summary>Bị động cho chiêu thức: % sát thương Hỏa, % xuyên thấu, cấp mở hiệu ứng thiêu đốt.</summary>
    private static (double skill, double pen, double burn) SkillExtra(Player p)
    {
        double k = 0, pe = 0, bu = 0;
        var active = CultivationManuals.Active(p);
        foreach (var (id, l) in SkillLevels(p))
            if (SkillData.ById.TryGetValue(id, out var s) && s.Tree != "basic")
            {
                var f = s.Factor(l) * CultivationManuals.Rate(active, s.Element);
                k += s.SkillPct * f; pe += s.Pen * f; bu += SkillSecondaryEffects.DebuffEnabled(active, s.Element) ? s.BurnPct * f : 0;   // thiêu đốt chỉ cần > 0 để mở, không bị giảm tác dụng
            }
        return (k, Math.Min(80, pe), bu);
    }

    // ================= Đan dược: luyện chế theo đan phương =================

    /// <summary>Đường dẫn cũ không còn nhận hợp thành đan.</summary>
    public (string msg, string kind, CraftResultDto craft) Synthesize(Player p, string fromId, int n) =>
        throw new GameException("Đã bỏ hợp thành đan. Hãy chọn đan phương trong Luyện chế · Đan dược.");

    /// <summary>Luyện đan theo đan phương n lần (0 = tối đa): tiêu nguyên liệu, nhận n viên. Luôn thành công.</summary>
    public (string msg, string kind, CraftResultDto craft) Brew(Player p, string outId, int n)
    {
        if (n < 0) throw new GameException("Số lần luyện chế không hợp lệ.");
        var b = GameData.Brews.FirstOrDefault(x => x.Out == outId) ?? throw new GameException("Không có đan phương này.");
        if (p.Realm < GameData.ItemById[outId].MinRealm)
            throw new GameException($"Cần đạt cảnh giới {GameData.Realms[GameData.ItemById[outId].MinRealm].Name} để luyện {GameData.ItemById[outId].Name}.");
        var max = MaxBrew(p, b);
        if (max < 1)
        {
            var miss = b.In.First(kv => p.Count(kv.Key) < kv.Value);
            throw new GameException($"Thiếu {GameData.ItemById[miss.Key].Name}: cần {miss.Value:N0}, đang có {p.Count(miss.Key):N0}.");
        }
        if (n > max) throw new GameException($"Chỉ đủ nguyên liệu luyện {max} viên, chưa đủ cho ×{n}.");
        n = n == 0 ? max : n;
        // túi đầy: chỉ luyện được khi đã có sẵn chồng thành phẩm hoặc một nguyên liệu bị dùng hết (nhả ô)
        if (p.Count(outId) == 0 && BagUsed(p) >= BagCap(p) && !b.In.Any(kv => kv.Key != "linh_thach" && p.Count(kv.Key) == kv.Value * n))
            throw new GameException("Hành trang đầy.");
        foreach (var (k, c) in b.In) p.Add(k, -c * n);
        p.Add(outId, n);
        var (msg, kind) = Log(p, $"Luyện thành {n} {GameData.ItemById[outId].Name}.", "gold");
        return (msg, kind, new CraftResultDto($"brew>{outId}", new(outId, n), b.In.Select(kv => new ItemQtyDto(kv.Key, kv.Value * n)).ToList()));
    }

    private static int MaxBrew(Player p, BrewDef b) => b.In.Min(kv => kv.Value > 0 ? p.Count(kv.Key) / kv.Value : int.MaxValue);

    // ================= Luyện khí: ghép 9 trang bị cùng phẩm → 1 phẩm cao hơn =================

    /// <summary>Ghép đúng GameData.FuseCount trang bị trong túi (items: danh sách id, lặp lại khi lấy nhiều món cùng id), cùng một phẩm.
    /// Thành công (GameData.FuseChance[phẩm]): 1 món phẩm kế tiếp; thất bại: 1 món cùng phẩm. Loại món gieo ngẫu nhiên trong các món đã bỏ vào.</summary>
    public (string msg, string kind, CraftResultDto craft) Fuse(Player p, string? items, Random? random = null)
    {
        var ids = (items ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (ids.Count != GameData.FuseCount) throw new GameException($"Cần đúng {GameData.FuseCount} trang bị để luyện khí.");
        var defs = ids.Select(id => GameData.ItemById.TryGetValue(id, out var d) && d.Slot is not null && d.Base is not null ? d
            : throw new GameException("Chỉ ghép được trang bị.")).ToList();
        var q = Array.IndexOf(GameData.RarityOrder, defs[0].Rarity);
        if (defs.Any(d => d.Rarity != defs[0].Rarity)) throw new GameException("Các trang bị phải cùng phẩm.");
        if (q < 0 || q >= GameData.FuseChance.Length) throw new GameException($"{GameData.QualityName[^1]} đã là phẩm cao nhất.");
        foreach (var g in ids.GroupBy(x => x))
            if (p.Count(g.Key) < g.Count()) throw new GameException($"Không đủ {GameData.ItemById[g.Key].Name} trong hành trang.");
        foreach (var g in ids.GroupBy(x => x)) p.Add(g.Key, -g.Count());
        var rng = random ?? Random.Shared;
        var ok = rng.NextDouble() < GameData.FuseChance[q];
        var baseDef = GameData.GearBaseById[defs[rng.Next(defs.Count)].Base!];
        var outId = GameData.GearId(baseDef, ok ? q + 1 : q);
        p.Add(outId, 1);
        var label = GameData.Label(GameData.ItemById[outId]);
        var (msg, kind) = ok
            ? Log(p, $"Luyện khí thành công: {GameData.FuseCount} trang bị {GameData.QualityName[q]} → {label}.", "gold")
            : Log(p, $"Luyện khí thất bại: {GameData.FuseCount} trang bị {GameData.QualityName[q]} chỉ còn lại {label}.", "bad");
        var inputs = ids.GroupBy(x => x).Select(g => new ItemQtyDto(g.Key, g.Count())).ToList();
        return (msg, kind, new CraftResultDto(ok ? "fuse:ok" : "fuse:fail", new(outId, 1), inputs));
    }

    // ================= Vòng quay: linh thạch → vé → quay vòng 10 phẩm trang bị =================

    /// <summary>Nhập code quà tặng: mỗi code nhận 1 lần cho mỗi nhân vật (Player.Redeemed, xóa khi tu lại từ đầu).</summary>
    public (string msg, string kind) RedeemCode(Player p, string? code)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        if (c.Length == 0) throw new GameException("Hãy nhập code.");
        if (!GameData.GiftCodes.TryGetValue(c, out var g)) throw new GameException("Code không đúng.");
        var used = p.Redeemed.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (used.Contains(c)) throw new GameException("Code này đã được nhận cho nhân vật hiện tại.");
        if (p.Count(g.Item) == 0 && BagUsed(p) >= BagCap(p)) throw new GameException("Hành trang đầy, dọn bớt một ô rồi nhập lại.");
        p.Redeemed = string.Join(',', used.Append(c));
        p.Add(g.Item, g.Qty);
        return Log(p, $"Nhập code {c}: +{g.Qty} {GameData.ItemById[g.Item].Name}.", "gold");
    }

    /// <summary>Đổi linh thạch lấy vé quay (n = số vé, 0 = đổi tối đa).</summary>
    public (string msg, string kind) BuySpinTicket(Player p, int n, bool pet = false)
    {
        var price = pet ? GameData.PetSpinTicketPrice : GameData.SpinTicketPrice;
        var ticket = pet ? GameData.PetSpinTicket : GameData.SpinTicket;
        var max = p.Count("linh_thach") / price;
        if (n <= 0) n = max;
        if (max <= 0 || n > max) throw new GameException($"Cần {(long)price * Math.Max(1, n):N0} linh thạch để đổi {Math.Max(1, n)} vé quay.");
        if (p.Count(ticket) == 0 && BagUsed(p) >= BagCap(p)) throw new GameException("Hành trang đầy, không nhận được vé quay.");
        p.Add("linh_thach", -n * price);
        p.Add(ticket, n);
        return Log(p, $"Đổi {(long)n * price:N0} linh thạch lấy {n} {GameData.ItemById[ticket].Name}.", "good");
    }

    /// <summary>Quay n lượt (1..SpinMaxBatch), mỗi lượt tốn 1 vé: nhận 1 trang bị bộ cảnh giới hiện tại, phẩm gieo theo GameData.SpinWeights.</summary>
    public (string msg, string kind, CraftResultDto craft) Spin(Player p, int n, DateTimeOffset? now = null)
    {
        n = Math.Clamp(n, 1, GameData.SpinMaxBatch);
        var quota = CheckSpinDaily(p, n, now);
        var have = p.Count(GameData.SpinTicket);
        if (have < n) throw new GameException(have == 0 ? "Chưa có vé quay. Đổi vé bằng linh thạch trước." : $"Cần {n} vé quay (đang có {have}).");
        var free = BagCap(p) - BagUsed(p) + (have == n ? 1 : 0);   // dùng hết vé thì trả lại ô của vé
        if (free < n) throw new GameException($"Hành trang cần {n} ô trống để nhận trang bị.");
        var pool = GameData.SpinPool(p.Realm);
        var outs = Enumerable.Range(0, n)
            .Select(_ => GameData.GearId(pool[Random.Shared.Next(pool.Length)], GameData.RollQuality(GameData.SpinWeights))).ToList();
        RecordSpinDaily(p, n, quota);
        var ev = AddEventPoints(p, "gear", n);   // sự kiện Thiên Cơ tuần Trang bị
        p.Add(GameData.SpinTicket, -n);
        foreach (var id in outs) p.Add(id, 1);
        p.SpinHistory = string.Join(';', p.SpinHistory.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Prepend($"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}|{string.Join(',', outs)}").Take(GameData.SpinHistoryKeep));
        int Q(string id) => Array.IndexOf(GameData.RarityOrder, GameData.ItemById[id].Rarity);
        var best = outs.MaxBy(Q)!;
        var labels = string.Join(", ", outs.OrderByDescending(Q).Select(id => GameData.Label(GameData.ItemById[id])));
        var (msg, kind) = Log(p, $"Vòng quay {n} lượt: {labels}.{(ev > 0 ? $" Thiên Cơ +{ev} điểm." : "")}", Q(best) >= 4 ? "gold" : "good");
        var got = outs.GroupBy(x => x).Select(g => new ItemQtyDto(g.Key, g.Count())).ToList();
        return (msg, kind, new CraftResultDto($"spin:{Q(best)}", new(best, 1), new List<ItemQtyDto> { new(GameData.SpinTicket, n) }, got));
    }

    // ================= Bản đồ: 10 chương × (15 ải + ải Boss) =================

    /// <summary>Chọn ải để tự động thám hiểm trong chương đang đánh. Ải Boss (BossArea) cần lệnh Boss và đã mở ải cuối.</summary>
    public (string msg, string kind) SelectArea(Player p, int area)
    {
        var c = Chap(p); var C = DiffTag(p) + (c.Index + 1);
        if (area == GameData.BossArea)
        {
            if (MapAreaMax(p) < GameData.Areas) throw new GameException($"Mở ải {C}-{GameData.Areas} trước khi khiêu chiến Boss.");
            if (p.Count(c.KeyItem) < 1 && !p.Cheat("boss")) throw new GameException($"Cần {GameData.ItemById[c.KeyItem].Name} để vào {c.BossLair}. Rơi ở mọi ải thường của chương.");
        }
        else if (area < 1 || area > MapAreaMax(p)) throw new GameException("Ải này chưa mở.");
        if (p.Area == area) return ("", "");
        p.Area = area;
        p.ExploreReadyAt = DateTime.UtcNow.AddSeconds(ExploreSeconds(p));   // bắt đầu chuyến mới ở ải vừa chọn
        return area == GameData.BossArea
            ? Log(p, p.Count(c.KeyItem) > 0 ? $"Cầm {GameData.ItemById[c.KeyItem].Name} tiến vào {c.BossLair}, khiêu chiến {c.Boss.Name}!" : $"[Admin] Vào {c.BossLair} không cần lệnh, khiêu chiến {c.Boss.Name}!", "gold")
            : ($"Chuyển sang ải {C}-{area} · {GameData.AreaPlace(c, area)}.", "");
    }

    public MapDto PreviewMap(Player p, int chapter)
    {
        if (!ChapterOpen(p, p.Diff, chapter)) throw new GameException("Chương này chưa mở.");
        // Detached scalar copy; map calculations only read the shared collections.
        var view = (Player)db.Entry(p).CurrentValues.ToObject();
        view.Inventory = p.Inventory; view.Equipment = p.Equipment; view.Pets = p.Pets;
        view.Chapter = chapter;
        return MapState(view) with { Area = chapter == p.Chapter ? p.Area : 0 };
    }

    public async Task<MapDto> PreviewMapAsync(Guid id, int chapter)
    {
        var p = await db.Players.Include(x => x.Inventory).Include(x => x.Equipment).Include(x => x.Pets)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException();
        return PreviewMap(p, chapter);
    }

    public (string msg, string kind) SelectMapArea(Player p, int chapter, int area)
    {
        var view = PreviewMap(p, chapter);
        if (area < 1 || area > GameData.BossArea || !view.Areas[area - 1].Open) throw new GameException("Ải này chưa mở.");
        if (area == GameData.BossArea && p.Count(view.KeyItem) < 1 && !p.Cheat("boss"))
            throw new GameException($"Cần {GameData.ItemById[view.KeyItem].Name} để khiêu chiến Boss.");
        SelectMap(p, chapter);
        return SelectArea(p, area);
    }

    /// <summary>Chọn chương (0-based) đã mở: chương đã qua mở hết ải; vào ải cao nhất đã mở của chương đó.</summary>
    public (string msg, string kind) SelectMap(Player p, int chapter)
    {
        if (chapter < 0 || chapter >= GameData.Chapters.Count || !GameData.Chapters[chapter].Enabled) throw new GameException("Chương này tạm thời chưa mở.");
        if (!ChapterOpen(p, p.Diff, chapter)) throw new GameException(p.Diff == 0 ? $"Đánh bại Boss chương {chapter} để mở chương {chapter + 1}."
            : $"Đánh bại Boss chương {chapter} ở mức {GameData.DiffNames[p.Diff]} để mở chương {chapter + 1}.");
        if (p.Chapter == chapter) return ("", "");
        p.Chapter = chapter;
        p.Area = MapAreaMax(p);
        p.ExploreReadyAt = DateTime.UtcNow.AddSeconds(ExploreSeconds(p));
        var c = Chap(p);
        return Log(p, $"Chuyển sang chương {chapter + 1} · {c.Name}: ải {DiffTag(p)}{chapter + 1}-{p.Area} · {GameData.AreaPlace(c, p.Area)}.", "");
    }

    /// <summary>Chọn mức độ khó (0 Thường · 1 Khó · 2 Ác mộng). Giữ chương đang đánh nếu đã mở ở mức đó, không thì sang chương cao nhất đã mở.</summary>
    public (string msg, string kind) SelectDiff(Player p, int d)
    {
        if (d < 0 || d >= GameData.DiffNames.Length) throw new GameException("Mức độ không hợp lệ.");
        if (p.Diff == d) return ("", "");
        var ch = ChapterOpen(p, d, p.Chapter) ? p.Chapter
            : Enumerable.Range(0, GameData.Chapters.Count).Where(c => ChapterOpen(p, d, c)).DefaultIfEmpty(-1).Max();
        if (ch < 0) throw new GameException($"Đánh bại Boss chương {GameData.OpenChapters} ở mức {GameData.DiffNames[d - 1]} để mở mức {GameData.DiffNames[d]}.");
        p.Diff = d; p.Chapter = ch;
        p.Area = MapAreaMax(p);
        p.ExploreReadyAt = DateTime.UtcNow.AddSeconds(ExploreSeconds(p));
        var c = Chap(p);
        return Log(p, $"Chuyển sang mức {GameData.DiffNames[d]}: chương {ch + 1} · {c.Name}, ải {DiffTag(p)}{ch + 1}-{p.Area}.", d > 0 ? "gold" : "");
    }

    // ---- tiến độ theo mức độ khó ----
    private static string DiffTag(Player p) => p.Diff > 0 ? GameData.DiffNames[p.Diff] + " " : "";
    private static HashSet<string> ClearedSet(Player p) => p.Cleared.Split(';', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    /// <summary>Đã hạ Boss chương c ở mức d (mức Thường: mọi chương dưới chương cao nhất đã mở cũng tính là đã qua).</summary>
    private static bool IsCleared(Player p, int d, int c) => (d == 0 && c < p.ChapterMax) || ClearedSet(p).Contains($"{d}.{c}");
    private static void MarkCleared(Player p, int d, int c) { var s = ClearedSet(p); if (s.Add($"{d}.{c}")) p.Cleared = string.Join(";", s); }
    /// <summary>Chương c đã mở ở mức d. Thường: theo ChapterMax. Khó / Ác mộng: mở cả mức khi đã hạ Boss chương cuối ở mức trước,
    /// rồi đi lần lượt từ chương 1 (hạ Boss chương N ở mức đó mở chương N+1).</summary>
    private static bool ChapterOpen(Player p, int d, int c) =>
        c >= 0 && c < GameData.Chapters.Count && GameData.Chapters[c].Enabled
        && (p.Cheat("map")                                          // admin: full bản đồ, mọi chương ở mọi mức
            || (d == 0 ? c <= p.ChapterMax : DiffUnlocked(p, d) && (c == 0 || IsCleared(p, d, c - 1))));
    /// <summary>Mức d (Khó / Ác mộng) mở khi đã hạ Boss chương cuối đang mở ở mức d−1.</summary>
    private static bool DiffUnlocked(Player p, int d) => d == 0 || IsCleared(p, d - 1, GameData.OpenChapters - 1);
    private static Dictionary<string, int> DiffProgMap(Player p) => p.DiffProg.Split(';', StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Split('=')).Where(x => x.Length == 2 && int.TryParse(x[1], out _)).ToDictionary(x => x[0], x => int.Parse(x[1]));
    private static void SetDiffProg(Player p, int d, int c, int a)
    {
        var m = DiffProgMap(p); m[$"{d}.{c}"] = a;
        p.DiffProg = string.Join(";", m.Select(kv => $"{kv.Key}={kv.Value}"));
    }

    private static MapDto MapState(Player p)
    {
        var c = Chap(p); var amax = MapAreaMax(p);
        var areas = Enumerable.Range(1, GameData.BossArea).Select(a =>
        {
            var b = BattleAt(p, a);
            var ws = GameData.AreaWaves(c, a, p.Diff);
            return new AreaDto(a, b.Monster, GameData.AreaPlace(c, a),
                b.MonsterHp, b.MonsterAtk, b.MonsterDef, b.Win, b.Hits, a <= amax || (a == GameData.BossArea && amax >= GameData.Areas), b.Image,
                ws.Select(w => { var (lo, hi) = GameData.StoneRange(c, a, w.Tier, p.Diff); return new WaveInfoDto(w.Name, w.Image, w.Tier, w.Hp, w.Atk, w.Def, lo, hi); }).ToList(),
                ws.Any(w => w.Tier == "elite"),a==GameData.BossArea?
                    PetLoot.BossMaterials(c.Index,p.Diff).Select(d=>new DropInfoDto(d.Item,d.Min,d.Max,d.Chance,false)).ToList():
                    PetLoot.StageOdds(c,p.Diff,a));
        }).ToList();
        return new MapDto(p.Area, amax, c.KeyItem, c.BossItem ?? "", c.BossLair, GameData.KeyChance(1), GameData.KeyChance(GameData.Areas), areas,
            new List<SynthStateDto>(), c.BossItem is not null && p.Count(c.BossItem) > 0, c.Index,
            GameData.DiffDrops(c, p.Diff).Select(d => new DropInfoDto(d.Item, d.Min, d.Max, GameData.DropChance(d), GameData.GearBaseById.ContainsKey(d.Item)))
                .Concat(GameData.HerbDrops.Where(h => h.Chapters.Contains(GameData.DiffHerbChapter(c, p.Diff))).Select(h => new DropInfoDto(h.Item, h.Min, h.Max, h.Chance, false, h.MinArea))).ToList(),
            GameData.ChestChance, GameData.RareChestChance, GameData.RareChestMinArea, GameData.ChestId(GameData.BossChest, p.Diff),
            p.ChapterMax, c.Boss.Name, c.Boss.Image, GameData.DiffQualityOf(c, p.Diff),
            p.Diff, Enumerable.Range(0, GameData.DiffNames.Length).Select(d => Enumerable.Range(0, GameData.Chapters.Count).Any(x => ChapterOpen(p, d, x))).ToArray(),
            GameData.Chapters.Select(x => ChapterOpen(p, p.Diff, x.Index)).ToArray(), GameData.Chapters.Select(x => IsCleared(p, p.Diff, x.Index)).ToArray(),
            GameData.DiffReward[p.Diff], GameData.DiffStatScale(p.Diff, c.Index));
    }

    /// <summary>Quy đổi vật phẩm của bản cũ (3 loại linh thạch, nguyên liệu luyện đan, đan phẩm chất…).</summary>
    private static void ConvertLegacy(Player p)
    {
        foreach (var inv in p.Inventory.Where(i => i.Count > 0 && !GameData.ItemById.ContainsKey(i.ItemId)).ToList())
        {
            var id = inv.ItemId;
            var q = id.LastIndexOf("_q", StringComparison.Ordinal);
            if (GameData.RemovedGearRefund(id) is > 0 and var lt) p.Add("linh_thach", inv.Count * lt);       // trang bị đã bỏ khỏi game → hoàn linh thạch
            else if (GameData.UpgradeGear(id) is { } up) p.Add(up, inv.Count);                                      // trang bị bản cũ → đúng cấp, cùng phẩm
            else if (q > 0 && GameData.ItemById.ContainsKey(id[..q])) p.Add(id[..q], inv.Count);              // đan phẩm chất → đan gốc
            else if (q > 0 && GameData.Legacy.TryGetValue(id[..q], out var lq)) p.Add(lq.To, inv.Count * lq.Factor);
            else if (GameData.Legacy.TryGetValue(id, out var l)) p.Add(l.To, inv.Count * l.Factor);
            inv.Count = 0;
        }
        p.RefineRecipe = -1;
        // bản đồ theo cảnh giới (bản cũ) → bản đồ 10 chương: mọi tu sĩ bắt đầu lại từ ải 1-1
        if (p.MapVer < 1) { p.Chapter = p.ChapterMax = 0; p.Area = p.AreaMax = 1; p.MapRealm = -1; p.MapVer = 1; p.ExploreReadyAt = DateTime.UtcNow; }
        p.ChapterMax = Math.Clamp(p.ChapterMax, 0, GameData.OpenChapters - 1);
        p.Chapter = Math.Clamp(p.Chapter, 0, p.ChapterMax);
        p.Area = Math.Clamp(p.Area, 1, GameData.BossArea);
        p.AreaMax = Math.Clamp(p.AreaMax, 1, GameData.Areas);
        p.Diff = Math.Clamp(p.Diff, 0, GameData.DiffNames.Length - 1);
        if (!ChapterOpen(p, p.Diff, p.Chapter)) { p.Diff = 0; p.Chapter = Math.Clamp(p.Chapter, 0, p.ChapterMax); }
        if (p.Area < GameData.BossArea) p.Area = Math.Min(p.Area, MapAreaMax(p));
        // bỏ kỳ "Đại viên mãn" (Trúc Cơ, Kim Đan còn 3 kỳ): tu sĩ đang ở kỳ cũ cuối → Hậu kỳ, giữ tu vi tối đa đủ đột phá
        if (p.Stage > Realm(p).Stages.Length - 1) { p.Stage = Realm(p).Stages.Length - 1; p.Qi = Math.Min(p.Qi, QiReq(p)); }
    }

    public async Task<Player> ResetAsync(Player p)
    {
        var spinQuota = SpinDailyFor(p);
        var petSpinQuota = SpinDailyFor(p, pet: true);
        db.Players.Remove(p);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        // giữ nguyên Id để client không phải đổi
        var fresh = await CreateAsync(p.Name, p.Id);
        RecordSpinDaily(fresh, 0, spinQuota);
        RecordSpinDaily(fresh, 0, petSpinQuota, pet: true);
        fresh.LbSecret = p.LbSecret;   // giữ khóa bảng xếp hạng để ghi đè đúng dòng cũ
        AddLog(fresh, "Tán công trùng tu, bắt đầu lại từ Luyện Khí tầng 1.");
        await db.SaveChangesAsync();
        return fresh;
    }

    // ================= Bảng xếp hạng =================

    private static string NewLbSecret() => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));

    /// <summary>Lực chiến: cùng công thức với ô "Lực chiến" trong Hành trang (inventory.js → power).</summary>
    public static long Power(Player p)
    {
        var st = Stats(p);
        return (long)Math.Round(st.Atk * 2.0 + st.Def * 3.0 + st.Hp * .4 + (st.Crit + st.Speed) * 4);
    }

    /// <summary>Đặt đạo hiệu hiển thị trên bảng xếp hạng (2–20 ký tự).</summary>
    public (string msg, string kind) Rename(Player p, string? name)
    {
        var n = string.Join(' ', (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (n.Length is < 2 or > 20) throw new GameException("Đạo hiệu cần 2–20 ký tự.");
        if (n.Any(char.IsControl) || n.Any(c => "<>\"'`".Contains(c))) throw new GameException("Đạo hiệu có ký tự không hợp lệ.");
        p.Name = n;
        return Log(p, $"Đổi đạo hiệu thành {n}.", "good");
    }

    public async Task SaveAsync(Player p)
    {
        await db.SaveChangesAsync();
        // giới hạn nhật ký để DB không phình
        var cutoff = await db.Logs.Where(l => l.PlayerId == p.Id).OrderByDescending(l => l.Id)
            .Skip(LogKeep).Select(l => l.Id).FirstOrDefaultAsync();
        if (cutoff > 0) await db.Logs.Where(l => l.PlayerId == p.Id && l.Id <= cutoff).ExecuteDeleteAsync();
        lb?.Track(p);   // bảng xếp hạng online: gửi nền, có giới hạn tần suất
        _arena?.TrackLoadout(p, lb?.Excluded(p) == true);   // Lôi đài: ảnh chụp kỹ năng cho người khác đánh
    }

    public Task<LeaderboardDto> LeaderboardAsync(Player p, string? by) => lb?.GetAsync(p, by) ?? Task.FromResult(new LeaderboardDto(false, by ?? "realm", [], null, p.Tainted, p.Name));
    public void PushLeaderboard(Player p) => lb?.Track(p, force: true);

    // ================= Nội bộ =================

    /// <summary>
    /// Cộng tu vi theo thời gian thực và chạy các chuyến thám hiểm tự động đã đến hạn
    /// (cả lúc người chơi vắng mặt, tối đa MaxOfflineSeconds).
    /// </summary>
    private void Tick(Player p, DateTime now)
    {
        var gap = Math.Clamp((now - p.LastTickAt).TotalSeconds, 0, GameData.MaxOfflineSeconds);
        var before = p.Qi;
        p.Qi = Math.Min(QiReq(p), p.Qi + Rate(p) * gap);
        p.LastTickAt = now;

        var trips = 0;
        var losses = 0;
        var lost = 0;   // vật phẩm bỏ lại vì túi đầy
        var loot = new Dictionary<string, int>();
        string? place = null;
        // luôn tự động thám hiểm (tu sĩ của bản cũ đang bế quan thì cho đi tiếp)
        if (!p.AutoExplore)
        {
            p.AutoExplore = true;
            p.ExploreReadyAt = now.AddSeconds(ExploreSeconds(p));
        }
        string? special = null, specialKind = "gold";
        var bossWins = 0;
        string? lostTo = null;                                      // quái (đợt) đã khiến chuyến thua                                            // số lần hạ Boss trong lượt cập nhật này (khiêu chiến liên tục đến hết chìa khóa)
        var startArea = p.Area;
        var mapTag = DiffTag(p) + (p.Chapter + 1);
        var dif = p.Diff; var rm = GameData.DiffReward[dif];          // mức Khó / Ác mộng: thưởng nhiều hơn
        var stay = p.Cheat("map");   // admin full bản đồ: giữ nguyên ải đã chọn (không lùi khi thua, không tự sang ải / chương / mức mới); tiến độ thật vẫn ghi
        // linh thạch rơi khi hạ từng quái: thắng = mọi đợt, thua = các đợt đã hạ trước đợt cuối
        void AddStones(BattleDto bt, bool win)
        {
            var ws = bt.Waves ?? new List<WaveDto>();
            var n = ws.Take(win ? ws.Count : Math.Max(0, ws.Count - 1)).Sum(w => w.Stones);
            if (n <= 0) return;
            var got = TryAddLoot(p, "linh_thach", n);
            if (got > 0) loot["linh_thach"] = loot.GetValueOrDefault("linh_thach") + got;
        }
        var startChapter = p.Chapter;
        {
            var r = Chap(p);
            var t = p.ExploreReadyAt < now.AddSeconds(-GameData.MaxOfflineSeconds) ? now.AddSeconds(-GameData.MaxOfflineSeconds) : p.ExploreReadyAt;
            while (t <= now)
            {
                var area = p.Area;
                var battle = BattleAt(p, area, t);
                trips++;
                place = GameData.AreaPlace(r, area);
                if (area == GameData.BossArea)
                {
                    // ải Boss: mỗi lần khiêu chiến tốn 1 lệnh (thắng hay thua). Thắng: rương Boss (+ nguyên liệu đột phá ở chương 1–3),
                    // lần đầu hạ Boss chương cao nhất thì mở chương kế và chuyển sang ải 1 của chương đó; còn lệnh thì khiêu chiến tiếp.
                    var freeBoss = p.Cheat("boss") && p.Count(r.KeyItem) < 1;            // admin: không cần lệnh (không tiêu hao, đánh liên tục)
                    if (!freeBoss && p.Count(r.KeyItem) < 1) { p.Area = GameData.Areas; t = t.AddSeconds(ExploreSeconds(p)); continue; }
                    if (!freeBoss) p.Add(r.KeyItem, -1);
                    var keysLeft = p.Count(r.KeyItem);
                    var keyName = GameData.ItemById[r.KeyItem].Name;
                    if (battle.Win)
                    {
                        bossWins++;
                        AddStones(battle, true);
                        if (r.BossItem is not null) p.Add(r.BossItem, 1);
                        p.BossKills++;
                        p.Qi = Math.Min(QiReq(p), p.Qi + ExploreQi(p) * 5 * rm);
                        var bossChest = GameData.ChestId(GameData.BossChest, dif);   // Rương Boss của mức đang đánh
                        var nChest = TryAddLoot(p, bossChest, GameData.DiffBossChests[dif]);   // Boss chắc chắn rơi rương (Khó 2 · Ác mộng 3)
                        var chestOk = nChest > 0;
                        if (chestOk) loot[bossChest] = loot.GetValueOrDefault(bossChest) + nChest;
                        foreach(var material in PetLoot.BossMaterials(r.Index,dif)){
                            if(Random.Shared.NextDouble()>=material.Chance)continue;
                            var quantity=Random.Shared.Next(material.Min,material.Max+1);
                            var received=TryAddLoot(p,material.Item,quantity);
                            if(received>0)loot[material.Item]=loot.GetValueOrDefault(material.Item)+received;
                            lost+=quantity-received;
                        }
                        var firstClear = !IsCleared(p, dif, r.Index);
                        MarkCleared(p, dif, r.Index);
                        var times = bossWins > 1 ? $" (lần {bossWins})" : "";
                        var gotText = string.Join(", ", new[] { r.BossItem is null ? null : GameData.ItemById[r.BossItem].Name, chestOk ? GameData.ItemById[bossChest].Name : null }.Where(x => x is not null));
                        special = $"Đánh bại {r.Boss.Name} ở {r.BossLair}{times}!{(gotText.Length > 0 ? $" Nhận {gotText}." : "")}{(chestOk ? "" : " Hành trang đầy, bỏ lại Rương Boss.")}"
                            + (freeBoss ? " [Admin: không cần lệnh]" : $" Tiêu hao {keyName}, còn {keysLeft}.");
                        specialKind = "gold";
                        if (firstClear && dif > 0 && r.Index + 1 < GameData.OpenChapters) special += $" Mở chương {r.Index + 2} mức {GameData.DiffNames[dif]}!";
                        // lần đầu hạ Boss chương cuối: mở mức kế và chuyển luôn sang ải 1-1 của mức đó
                        if (firstClear && r.Index == GameData.OpenChapters - 1 && dif + 1 < GameData.DiffNames.Length)
                        {
                            if (stay) special += $" Mở mức {GameData.DiffNames[dif + 1]}!";
                            else
                            {
                                p.Diff = dif + 1; p.Chapter = 0; p.Area = MapAreaMax(p);
                                special += $" Mở mức {GameData.DiffNames[p.Diff]}! Chuyển sang ải {DiffTag(p)}1-{p.Area}.";
                            }
                            t = t.AddSeconds(ExploreSeconds(p));
                            break;                                                   // sang mức mới: các chuyến sau tính ở lượt cập nhật kế
                        }
                        // lần đầu hạ Boss của chương cao nhất (mức Thường): mở chương kế
                        if (dif == 0 && r.Index == p.ChapterMax && p.ChapterMax + 1 < GameData.OpenChapters)
                        {
                            p.ChapterMax++; p.AreaMax = 1;
                            if (!stay) { p.Chapter = p.ChapterMax; p.Area = 1; }
                            var nc = GameData.Chapters[p.ChapterMax];
                            special += $" Mở chương {nc.Index + 1} · {nc.Name}!";
                            t = t.AddSeconds(ExploreSeconds(p));
                            break;                                                   // sang chương mới: các chuyến sau tính ở lượt cập nhật kế
                        }
                        if (keysLeft > 0 || p.Cheat("boss")) { t = t.AddSeconds(ExploreSeconds(p)); continue; }   // còn lệnh: ở lại ải Boss, đánh tiếp
                        special += $" Trở về ải {mapTag}-{GameData.Areas}.";
                    }
                    else
                    {
                        losses++;
                        special = $"Không địch nổi {r.Boss.Name}, rút khỏi {r.BossLair}{(freeBoss ? "" : $" (tiêu hao {keyName}, còn {keysLeft})")}. Tu luyện thêm rồi quay lại.";
                        specialKind = "bad";
                    }
                    p.Area = GameData.Areas;
                    t = t.AddSeconds(ExploreSeconds(p));
                    continue;
                }
                t = t.AddSeconds(ExploreSeconds(p));
                if (!battle.Win)
                {
                    // thua trận: rút lui về ải trước, chỉ nhận nửa tu vi
                    losses++;
                    AddStones(battle, false);                                    // quái đã hạ trước khi thua vẫn rơi linh thạch
                    lostTo = battle.Waves is { Count: > 0 } lw ? lw[^1].Name : battle.Monster;
                    p.Qi = Math.Min(QiReq(p), p.Qi + ExploreQi(p) / 2 * rm);
                    if (p.Area > 1 && !stay) p.Area--;
                    continue;
                }
                AddStones(battle, true);                                          // linh thạch: rơi theo từng quái gục
                foreach (var group in PetLoot.StageGroups(r,dif,area))
                {
                    if(group.Choices[0].Item=="linh_thach")continue;
                    if(Random.Shared.NextDouble()>=group.Chance)continue;
                    var d=PetLoot.Choose(group,Random.Shared);
                    var isGear = GameData.GearBaseById.TryGetValue(d.Item, out var gb);
                    if (isGear && dif > 0) gb = GameData.RealmGear(gb!, p.Realm);   // Khó / Ác mộng: đồ theo bộ cảnh giới của người chơi (Hợp Thể → Thiên Hợp)
                    var n = PetLoot.DropQuantity(d.Item, Random.Shared.Next(d.Min, d.Max + 1), dif);
                    // trang bị: cấp theo cảnh giới yêu cầu của món, cấp bậc theo GameData.DropQuality (Khó / Ác mộng nâng phẩm)
                    var dropId = isGear ? GameData.RollGear(gb!.Id, GameData.DiffQuality(GameData.DiffQualityOf(r, dif) ?? GameData.DropQualityOf(gb!.MinRealm), dif)) : d.Item;
                    var got = TryAddLoot(p, dropId, n);
                    if (got > 0) loot[dropId] = loot.GetValueOrDefault(dropId) + got;
                    lost += n - got;
                }
                p.Qi = Math.Min(QiReq(p), p.Qi + ExploreQi(p) * rm);
                // thắng ở ải cao nhất → mở và tiến sang ải kế
                if (dif == 0 && p.Chapter == p.ChapterMax && area == p.AreaMax && p.AreaMax < GameData.Areas) { p.AreaMax++; if (!stay) p.Area = p.AreaMax; }
                else if (dif > 0 && area == MapAreaMax(p) && area < GameData.Areas) { SetDiffProg(p, dif, p.Chapter, area + 1); p.Area = area + 1; }
            }
            p.ExploreReadyAt = t;
        }

        var gained = Math.Floor(p.Qi - before);
        var qiText = p.Qi >= QiReq(p) && gained <= 0 ? "tu vi đã đầy bình cảnh, cần đột phá" : $"+{gained:N0} tu vi";
        var lootText = string.Join(", ", loot.Select(kv => $"+{kv.Value} {GameData.Label(GameData.ItemById[kv.Key])}"))
            + (lost > 0 ? $"{(loot.Count > 0 ? ", " : "")}hành trang đầy nên bỏ lại {lost} món" : "");
        var gotEquip = loot.Keys.Any(k => GameData.ItemById[k].Slot is not null || GameData.ItemById[k].Type == "key");
        if (special is not null) AddLog(p, special, specialKind);
        var areaTag = $"ải {mapTag}-{startArea}";
        if (p.Chapter == startChapter && p.Area != startArea && startArea <= GameData.Areas && trips > 0 && gap <= 60)
            AddLog(p, p.Area > startArea ? $"Qua {areaTag}, mở ải {mapTag}-{p.Area} · {GameData.AreaPlace(Chap(p), p.Area)}." : $"Thua ở {areaTag}, lùi về ải {mapTag}-{p.Area}.", p.Area > startArea ? "good" : "bad");
        if (gap > 60)
            AddLog(p, trips > 0
                ? $"Vắng mặt {Math.Round(gap / 60)} phút: thám hiểm {trips} chuyến{(losses > 0 ? $" (thua {losses})" : "")}, {qiText}{(lootText.Length > 0 ? ", " + lootText : "")}."
                : $"Vắng mặt {Math.Round(gap / 60)} phút, thu được {gained:N0} tu vi.", "gold");
        else if (special is not null && trips == 1) { }
        else if (trips > 0 && losses == trips)
            AddLog(p, $"Thám hiểm {areaTag} · {place}: bị {lostTo ?? GameData.AreaWaves(GameData.Chapters[startChapter], startArea, dif)[0].Name} đánh trọng thương, phải rút lui. Cần thêm tấn công, máu hoặc phòng thủ.{(lootText.Length > 0 ? $" Nhặt được {lootText}." : "")}", "bad");
        else if (trips > 0)
            AddLog(p, $"Thám hiểm {areaTag} · {place}: hạ {WaveNames(GameData.Chapters[startChapter], startArea)}: {(p.Qi >= QiReq(p) && gained <= 0 ? "tu vi đã đầy" : $"+{gained:N0} tu vi")}{(lootText.Length > 0 ? ", " + lootText : "")}.", gotEquip ? "gold" : "loot");
    }

    /// <summary>Tên các đợt quái của ải, gộp loài trùng: "Yêu Lang ×2, Độc Xà" · tinh anh ghi kèm.</summary>
    private static string WaveNames(ChapterDef r, int area) =>
        string.Join(", ", GameData.AreaWaves(r, area).GroupBy(w => w.Name)
            .Select(g => g.First().Name + (g.Count() > 1 ? $" ×{g.Count()}" : "") + (g.First().Tier == "elite" ? " (tinh anh)" : "")));

    /// <summary>Tổng chỉ số % từ trang bị đang mặc.</summary>
    private static double Bonus(Player p, Func<ItemDef, double> stat) =>
        EquipmentEffects.ValidItems(p).Sum(stat);

    /// <summary>Tu vi mỗi giây đã tính trang bị (gồm Hấp linh).</summary>
    private static double Rate(Player p) =>
        GameData.QiRate(Realm(p), p.Stage) * (1 + (Bonus(p, i => i.QiPct) + SkillBonus(p).qi + GearEffects(p).Qi + PetTalent(p,"qi")) / 100) * (DateTime.UtcNow < p.DebuffUntil ? GameData.DebuffRate : 1);

    /// <summary>Tổng hiệu ứng riêng của trang bị đang mặc, đã áp trần (xem plan "Hiệu ứng riêng theo cấp trang bị").</summary>
    public record GearFx(double Qi, double ExploreQi, double Pen, double SkillPct, double Rage, double Guard, double ExtraHit, double HpPct,
        double Dodge, double Regen, double Shield, double FirstHit, double Reflect, double Lightning, double LightningPct, double MonAtkDown,
        double CritBonus, double SetPct, double SetCrit, double SetSpeed);
    public static GearFx GearEffects(Player p)
    {
        var snapshot = EquipmentEffects.Calculate(p);
        double S(string id) => snapshot.Effects.FirstOrDefault(e => e.Id == id)?.Effective ?? 0;
        var boltPct = EquipmentEffects.ValidItems(p).SelectMany(i => i.Effects ?? Array.Empty<ItemEffect>())
            .Where(e => e.Id == "thien_loi").Select(e => e.Value2).DefaultIfEmpty(0).Max();
        return new GearFx(S("hap_linh"), S("tu_linh"), S("pha_giap"), S("linh_ap"), S("tinh_tam"), S("ho_the"), S("lien_kich"),
            S("vung_than"), S("than_phap"), S("hoi_khi"), S("tho_thuan"), S("dia_chan"), S("dia_mach"),
            S("thien_loi"), boltPct, S("thien_uy"), S("thien_menh"), S("set_pct"), S("crit"), S("speed"));
    }

    /// <summary>Thời lượng một chuyến: đi đường + số lượt đánh + kết thúc. Tấn công cao thì hạ quái nhanh hơn.</summary>
    private static double ExploreSeconds(Player p)
    {
        var tempoFactor = 1 - Math.Clamp(Stats(p).Speed, 0, 60) / 100;
        var t = Math.Max(GameData.MinExploreSeconds,
            (GameData.WalkSeconds
            + Battle(p).Hits * GameData.HitSeconds
            + (Math.Max(1, Battle(p).Waves?.Count ?? 1) - 1) * GameData.WaveSeconds
            + GameData.EndSeconds) * tempoFactor);
        var k = FastMult(p);                                   // admin: tốc độ thám hiểm ×0.5 / ×2 / ×3 / ×5
        return k != 1 ? Math.Max(1, t / k) : t;
    }

    private static EffectState[] CombatLimits(Player p)
    {
        var snapshot = EquipmentEffects.Calculate(p);
        double Raw(string id) => snapshot.Effects.FirstOrDefault(e => e.Id == id)?.Raw ?? 0;
        var sk = SkillBonus(p);
        var extra = SkillExtra(p);
        var items = EquipmentEffects.ValidItems(p);
        var lv = SkillLevels(p);
        double guard = 0, dodge = 0;
        foreach (var id in p.Slots().Skip(1).Where(id => lv.GetValueOrDefault(id) > 0 && SkillData.ById.ContainsKey(id)))
        {
            var active = SkillData.Active(SkillData.ById[id], lv[id]); guard += active.shield; dodge += active.dodge;
        }
        EffectState Limit(string id, string name, double value, double cap) => new(id, name,
            Math.Round(value, 4), Math.Round(Math.Min(value, cap), 4), cap, "%");
        var crit = 5 + PetTalent(p,"crit") + items.Sum(i => i.CritPct) + sk.crit + Raw("crit") + Raw("thien_menh");
        var basic = SkillData.ById.GetValueOrDefault(p.Slots()[SkillData.BasicSlot]);
        var dao = DivineAbility.For(p).Tier == 8;
        return new[]
        {
            Limit("crit", "Bạo kích chung", crit, 100),
            Limit("basic_crit", "Bạo kích đòn thường", Math.Min(100, crit) + (basic?.Crit ?? 0), 100),
            Limit("lien_kich", "Liên kích (gồm thần thông)", Math.Min(30,Raw("lien_kich") + PetTalent(p,"extra")) + (dao ? 20 : 0), dao ? 50 : 30),
            Limit("speed", "Thân pháp: tăng tốc di chuyển và giao chiến", (p.Realm >= 1 ? GameData.BaseSpeed[Math.Min(p.Realm, GameData.BaseSpeed.Length - 1)] + items.Sum(i => i.SpeedPct) + Raw("speed") : 0) + sk.speed + PetTalent(p,"speed"), 60),
            Limit("pha_giap", "Tổng bỏ qua thủ", Raw("pha_giap") + extra.pen + PetTalent(p,"pen"), 80),
            Limit("ho_the", "Tổng giảm sát thương", Raw("ho_the") + guard + PetTalent(p,"guard"), 60),
            Limit("than_phap", "Tổng né tránh (Thân pháp + Vân bộ)", Stats(p).Speed + Raw("than_phap") + dodge, CombatDodge.MaxChance),
            Limit("thien_uy", "Tổng giảm công quái", Raw("thien_uy") + sk.monAtk, 40)
        };
    }

    private static StatSourceDto[] StatSources(Player p)
    {
        var b = GameData.BaseStats(Realm(p), p.Stage);
        var items = EquipmentEffects.ValidItems(p);
        var sk = SkillBonus(p);
        var fx = GearEffects(p);
        var sets = EquipmentEffects.Calculate(p).SetBonuses;
        return new[]
        {
            new StatSourceDto("Nền cảnh giới", b.Atk, b.Def, b.Hp, Crit: 5,
                Speed: p.Realm >= 1 ? GameData.BaseSpeed[Math.Min(p.Realm, GameData.BaseSpeed.Length - 1)] : 0),
            new StatSourceDto("Trang bị trực tiếp", items.Sum(i => i.Atk), items.Sum(i => i.Def), items.Sum(i => i.Hp),
                Crit: items.Sum(i => i.CritPct), Speed: items.Sum(i => i.SpeedPct), Qi: items.Sum(i => i.QiPct)),
            new StatSourceDto("Linh thú", PetBonus(p).Atk, PetBonus(p).Def, PetBonus(p).Hp, AtkPct: PetTalent(p,"atk"), DefPct: PetTalent(p,"def"), HpPct: PetTalent(p,"hp"), Crit: PetTalent(p,"crit"), Speed: PetTalent(p,"speed"), Qi: PetTalent(p,"qi")),
            new StatSourceDto("Luyện thể", Def: p.BodyDef, Hp: p.BodyHp),
            new StatSourceDto("Công pháp", AtkPct: sk.atk, DefPct: sk.def, HpPct: sk.hp, Crit: sk.crit, Speed: sk.speed, Qi: sk.qi),
            new StatSourceDto("Hiệu ứng món", HpPct: fx.HpPct, Qi: fx.Qi - sets.GetValueOrDefault("hap_linh")),
            new StatSourceDto("Thưởng bộ", AtkPct: fx.SetPct, DefPct: fx.SetPct, HpPct: fx.SetPct,
                Crit: fx.SetCrit, Speed: fx.SetSpeed, Qi: sets.GetValueOrDefault("hap_linh"))
        };
    }

    /// <summary>Chỉ số chiến đấu = chỉ số gốc theo cảnh giới/tầng + trang bị.</summary>
    public static CombatStats Stats(Player p)
    {
        var b = GameData.BaseStats(Realm(p), p.Stage);
        var items = EquipmentEffects.ValidItems(p);
        int Sum(Func<ItemDef, int> f) => items.Sum(f);
        double SumD(Func<ItemDef, double> f) => items.Sum(f);
        var baseSpeed = GameData.BaseSpeed[Math.Min(p.Realm, GameData.BaseSpeed.Length - 1)];
        bool On(string id) => p.Realm >= GameData.ExtraStats.First(x => x.Id == id).MinRealm;
        var weak = DateTime.UtcNow < p.DebuffUntil ? GameData.DebuffStat : 1;   // tẩu hỏa nhập ma
        var sk = SkillBonus(p);                                                  // công pháp bị động
        var fx = GearEffects(p);                                                 // Vững thân (máu) + thưởng cùng bộ
        return new CombatStats((int)((b.Atk + Sum(i => i.Atk) + PetBonus(p).Atk) * weak * (1 + (sk.atk + fx.SetPct + PetTalent(p,"atk")) / 100)), (int)((b.Hp + Sum(i => i.Hp) + p.BodyHp + PetBonus(p).Hp) * (1 + (sk.hp + fx.HpPct + fx.SetPct + PetTalent(p,"hp")) / 100)),
            (int)((b.Def + Sum(i => i.Def) + p.BodyDef + PetBonus(p).Def) * weak * (1 + (sk.def + fx.SetPct + PetTalent(p,"def")) / 100)),
            Math.Min(60, (On("speed") ? baseSpeed + SumD(i => i.SpeedPct) + fx.SetSpeed : 0) + sk.speed + PetTalent(p,"speed")),
            Math.Min(100, 5 + SumD(i => i.CritPct) + sk.crit + fx.SetCrit + fx.CritBonus + PetTalent(p,"crit")));
    }

    /// <summary>
    /// Trận đánh theo lượt với hạt giống cố định theo chuyến: tu sĩ đánh trước, quái phản đòn sau mỗi lượt.
    /// Thắng nếu hạ được quái trước khi hết máu.
    /// </summary>
    public static BattleDto Battle(Player p) => BattleAt(p, p.Area);

    /// <param name="trip">Thời điểm kết thúc chuyến (mặc định ExploreReadyAt): làm hạt giống ngẫu nhiên cố định
    /// để trận client đang diễn và trận server chốt kết quả ra cùng một kết quả (thiêu đốt 30%).</param>
    public static BattleDto BattleAt(Player p, int area, DateTime? trip = null)
    {
        var tripSec = (trip ?? p.ExploreReadyAt).Ticks / TimeSpan.TicksPerSecond;
        var rng = new Random(unchecked((p.Id.GetHashCode() * 31 + area) * 31 + (int)(tripSec % 1_000_000_007)));
        var stoneRng = new Random(unchecked((p.Id.GetHashCode() * 37 + area) * 41 + (int)(tripSec % 1_000_000_007)));   // linh thạch mỗi quái: cùng chuyến ra cùng số ở client và server
        var dodgeRng = new Random(unchecked((p.Id.GetHashCode() * 53 + area) * 59 + (int)(tripSec % 1_000_000_007)));
        var activePet=p.Pets.FirstOrDefault(x=>x.Slot==1 && PetData.ById.ContainsKey(x.DefId));
        var petDef=activePet is null?null:PetData.ById[activePet.DefId];
        var petCasts=new List<PetCastDto>();
        var st = Stats(p);
        var petCombat = activePet is null || petDef is null ? null : new PetCombat(petDef, activePet.Form, st, PetData.StarsOf(activePet),
            new Random(unchecked((p.Id.GetHashCode()*71+area)*73+(int)(tripSec%1_000_000_007))),PetData.QualityOf(activePet,petDef)==5,
            PetStats(p, activePet), PetData.QualityOf(activePet, petDef));   // Linh Uy: chỉ số riêng + phẩm riêng của con
        var fx = GearEffects(p);                               // hiệu ứng riêng của trang bị theo cấp
        var down = Math.Min(40, SkillBonus(p).monAtk + fx.MonAtkDown);   // công pháp khống chế + Thiên uy làm yếu đòn quái
        var (skillPct, pen, burnPct) = SkillExtra(p);                   // Hỏa Tụ / Liệt Diễm Tâm / Xích Diễm Đạo Thể · Hỏa Linh Chân Ý
        pen = Math.Min(80, pen + fx.Pen + PetTalent(p,"pen")); skillPct += fx.SkillPct;      // Phá giáp · Linh áp
        // các đợt quái của ải (ải thường 3 đợt, ải Boss 1): đã trừ công (khống chế) và thủ (phá giáp)
        var waves = GameData.AreaWaves(Chap(p), area, p.Diff).Select(w => w with {
            Atk = down > 0 ? (int)Math.Round(w.Atk * (1 - down / 100)) : w.Atk,
            Def = pen > 0 ? (int)Math.Floor(w.Def * (1 - pen / 100)) : w.Def }).ToArray();   // làm tròn xuống: quái thủ thấp vẫn thấy tác dụng
        var wave = 0; var m = waves[0];
        var crit = Math.Min(100, st.Crit) / 100;
        double critMult = 0.5 + PetTalent(p,"critdmg")/100; // bạo kích gây ×1.5; Thiên mệnh cộng tỉ lệ trong Stats
        var power = st.Atk * (1 + crit * critMult);
        // đòn đánh thường = chiêu ở ô 1 (Hỏa Cầu / Thủy Cầu), chưa đặt thì đánh 100% công
        var lv = SkillLevels(p);
        var basicId = p.Slots()[SkillData.BasicSlot];
        var basicOk = SkillData.ById.TryGetValue(basicId, out var bdef) && bdef.Tree == "basic";
        if (!basicOk) { basicId = "hoa_cau"; bdef = SkillData.ById[basicId]; }        // mặc định: Hỏa Cầu
        var bcrit = Math.Min(100, st.Crit + bdef!.Crit) / 100;                       // đòn thường có thể cộng thêm chí mạng riêng
        var bpower = (double)st.Atk;
        var heroBase = (int)Math.Round(bpower * bdef.DmgBase * (1 + skillPct / 100) / 100);
        int HeroDmgVs(MonsterDef x) => Math.Max(1, heroBase - x.Def);
        var heroDmg = HeroDmgVs(m);

        // kỹ năng trong ô: chiêu công tự thi triển theo lượt, chiêu thủ giảm sát thương, khống làm quái đứng yên, thân pháp né đòn
        var equipped = p.Slots().Skip(1).Where(id => id.Length > 0 && lv.GetValueOrDefault(id) > 0 && SkillData.ById.ContainsKey(id))
            .Select(id => (s: SkillData.ById[id], l: lv[id])).ToList();
        var activeManual = CultivationManuals.Active(p);
        double shield = 0, dodge = 0, burn = 0; int stun = 0;
        foreach (var (s, l) in equipped)
        {
            var a = SkillData.Active(s, l);
            shield += a.shield; dodge += a.dodge; stun += SkillSecondaryEffects.DebuffEnabled(activeManual, s.Element) ? a.stun : 0;
            burn += SkillSecondaryEffects.DebuffEnabled(activeManual, s.Element) && a.burn > 0 ? SkillData.Damage(s, l) * a.burn / 100 : 0;
        }
        shield = Math.Min(60, shield + fx.Guard + PetTalent(p,"guard")); dodge = CombatDodge.Chance(st.Speed, fx.Dodge, dodge); stun = Math.Min(4, stun);
        var secondary = new SkillEffectCombat(activeManual); var currentTurn = 0;
        var secondaryRng = new Random(unchecked((p.Id.GetHashCode() * 83 + area) * 89 + (int)(tripSec % 1_000_000_007)));
        int MonHit(double atk, bool withSecondary = true) => p.Cheat("god") ? 0 : Math.Max(1, (int)Math.Round(Math.Max(1, atk * (1 - Math.Min(40, (petCombat?.AttackDown ?? 0) + (withSecondary ? secondary.AttackDown(currentTurn) : 0)) / 100) - st.Def) * (1 - Math.Min(60, shield + (petCombat?.GuardBonus ?? 0)) / 100)));   // Né xử lý riêng từng đòn.
        var monDmg = MonHit(m.Atk);
        // Boss: kỹ năng ẩn — Yêu thân (giảm sát thương nhận) · Cuồng bạo (máu thấp thì công tăng)
        var isBoss = area >= GameData.BossArea;
        var trait = isBoss ? GameData.DiffTrait(Chap(p), p.Diff) : null;
        if (trait is not null)                                                    // Boss: kháng bạo trừ thẳng tỉ lệ bạo kích
        {
            crit = Math.Max(0, crit - trait.CritRes / 100);
            bcrit = Math.Max(0, bcrit - trait.CritRes / 100);
        }
        double TakeK(MonsterDef x) => x.Tier == "elite" ? 1 - GameData.EliteDmgReduce / 100 : trait is null ? 1 : 1 - trait.DmgReduce / 100;   // tinh anh: giảm 5% sát thương nhận
        var takeK = TakeK(m);
        var monDmgEnr = trait is null ? monDmg : MonHit(m.Atk * (1 + trait.EnrageAtk / 100));
        var enraged = false; var enrageAt = -1;

        // Thiêu đốt: 5% máu tối đa, hai lượt kể cả lượt kích hoạt; không cộng dồn.
        var passiveBurn = burnPct > 0 || burn > 0;                                       // bị động: 30% mỗi lượt từ lượt 2
        var canBurn = passiveBurn || equipped.Any(e => SkillData.ActiveBurnChance(e.s, e.l, activeManual) > 0);   // kỹ năng Hỏa: % theo cấp khi tung
        var burning = new CombatBurn();
        var burnDmg = canBurn ? Math.Max(1, (int)Math.Round(m.Hp * .05)) : 0;
        var burnDamage = new List<int>();
        // hiệu ứng trang bị: khiên đầu trận · đòn Địa chấn · tia Thiên lôi · hồi máu mỗi lượt
        var guard = (int)Math.Round(st.Hp * fx.Shield / 100) + (petCombat?.InitialShield() ?? 0); var guardLeft = guard;
        var firstHit = fx.FirstHit > 0 ? Math.Max(1, (int)Math.Round(power * fx.FirstHit / 100)) : 0;
        var boltDmg = fx.Lightning > 0 ? Math.Max(1, (int)Math.Round(power * fx.LightningPct / 100)) : 0;
        var regen = (int)Math.Round(st.Hp * (fx.Regen + PetTalent(p,"regen")) / 100);
        var woodLevels = SkillLevels(p);
        double WoodBonus(Func<SkillDef, double> value) => woodLevels.Sum(kv => SkillData.ById.TryGetValue(kv.Key, out var def) && def.Element == "moc" ? value(def) * def.Factor(kv.Value) * CultivationManuals.Rate(CultivationManuals.Active(p), "moc") : 0);
        var woodHeal = WoodBonus(s => s.HealOnCastPct);
        var woodEmergency = WoodBonus(s => s.EmergencyHealPct);
        var woodRecovered = false;
        var ability = DivineAbility.For(p);
        var divine = new DivineCombat(ability.Tier);
        var divineRng = new Random(unchecked((p.Id.GetHashCode()*71+area)*79+(int)(tripSec%1_000_000_007)));
        var extraChance = Math.Max(0, divine.ExtraChance(Math.Min(30,fx.ExtraHit + PetTalent(p,"extra"))) - (trait?.ExtraRes ?? 0));   // Boss: kháng liên kích
        var divineTurns = new List<DivineTurnDto>();
        var hitBoosted = false;
        int monHp = m.Hp, heroHp = st.Hp, turns = 0, rage = (int)fx.Rage, tw = 0; var win = false;   // Tĩnh tâm: vào trận đã có nộ · tw: lượt trong đợt
        var waveFirst = new List<int> { 0 };                                  // lượt (thật) bắt đầu mỗi đợt
        bool NextWave(int t)                                                  // hạ xong đợt này: quái đợt sau bước vào, máu tu sĩ giữ nguyên
        {
            if (wave + 1 >= waves.Length) return false;
            wave++; m = waves[wave]; heroDmg = HeroDmgVs(m); monDmg = MonHit(m.Atk); monDmgEnr = monDmg; takeK = TakeK(m);
            monHp = m.Hp; burning.Reset(); secondary.ClearEnemy(); petCombat?.NextWave(); tw = -1; waveFirst.Add(t + 1);
            return true;
        }
        var monLeft = new List<int>(); var heroLeft = new List<int>(); var skip = new List<bool>(); var casts = new List<CastDto>(); var rages = new List<int>(); var burns = new List<bool>();
        var bolts = new List<bool>(); var extras = new List<bool>(); var heals = new List<int>(); var reflects = new List<int>(); var guards = new List<int>();
        var basicDamage = new List<int>(); var basicCritical = new List<bool>();
        var extraDamage = new List<int>(); var extraCritical = new List<bool>();
        var dodged = new List<bool>();
        var monControl = new List<string>(); var shatters = new List<bool>();   // loại khống chặn phản công · lượt đánh vỡ băng (+10%)
        var weakened = new List<bool>(); var windGuard = new List<bool>(); var counterDamage = new List<int>();
        var heroBefore = new List<int>(); var guardBefore = new List<int>();
        var petTargetBurning = false;
        int HitDamage(double raw, bool critical, bool boost = true)
        {
            var damage=Math.Max(1, (int)Math.Round(Math.Max(1, (int)Math.Round(raw * (critical ? 1 + critMult : 1)) - m.Def) * takeK));
            damage = petCombat?.AmplifyOwner(damage, critical, petTargetBurning) ?? damage;
            if(!boost)return damage;
            var amplified=divine.Amplify(damage);
            hitBoosted |= amplified!=damage;
            return amplified;
        }
        // thứ tự mỗi lượt (giữ cố định để gieo số giống nhau): thiêu đốt → thiên lôi → đòn thường / kỹ năng → liên kích → quái đánh → phản đòn → hồi máu
        for (var t = 0; t < 150; t++, tw++)
        {
            currentTurn = t; var pendingSkillHeal = 0; counterDamage.Add(0);
            heroBefore.Add(heroHp); guardBefore.Add(guardLeft);
            divine.BeginTurn(); hitBoosted=false;
            divineTurns.Add(new(false,false,false,divine.Charged));
            var dmg = 0;
            petTargetBurning = petCombat?.Burning == true;
            var petPulse = petCombat?.BeginTurn(t, ref heroHp, monHp, m.Def, takeK);
            if (petPulse is not null) petCasts.Add(petPulse);
            var preMiracle=petCombat?.TryMiracle(t,ref heroHp,monHp,m.Def,takeK);
            if (petCombat?.NewDebuff.Length > 0) { burning.Reset(); secondary.ClearEnemy(); } // debuff mới nhất đè debuff cũ
            if(preMiracle is not null)petCasts.Add(preMiracle);
            var burnTrig = passiveBurn && tw > 0 && rng.Next(100) < SkillData.BurnChance;
            if (burnTrig) { petCombat?.ClearDebuffs(); secondary.ClearEnemy(); } // thiêu đốt mới đè khống / suy yếu
            var burnHit = burning.Tick(m.Hp, burnTrig);
            var burned = burnHit > 0;
            petTargetBurning |= burned;
            burnDamage.Add(burnHit);
            burns.Add(burned);
            var bolt = boltDmg > 0 && rng.NextDouble() * 100 < fx.Lightning;   // tỉ lệ có phần lẻ (vd 15.8%): so số thực, không làm tròn lên           // Thiên lôi
            if (bolt) dmg += boltDmg;
            bolts.Add(bolt);
            if (t == 0) dmg += firstHit;                                       // Địa chấn
            var full = rage >= SkillData.RageMax; var fired = false;          // đầu lượt đã đủ 100 nộ → lượt này tung kỹ năng chủ động
            foreach (var (s, l) in equipped)
            {
                var a = SkillData.Active(s, l);
                var pct = SkillData.Damage(s, l);
                if (pct == 0 || a.stun > 0 && a.burn > 0)
                {
                    if (t == 0) casts.Add(new CastDto(0, s.Id, s.Name, s.Anim, 0));   // chiêu thủ / khống / thân pháp: thi triển đầu trận
                    continue;
                }
                if (!full) continue;                                           // đủ 100 nộ mới tung kỹ năng chủ động
                fired = true;
                var critical = rng.NextDouble() < crit;
                var cd = HitDamage(st.Atk * pct * (1 + skillPct / 100) / 100, critical);
                // Phần sát thương phụ được giảm theo Yêu thân một lần trước khi cộng các đòn.
                casts.Add(new CastDto(t, s.Id, s.Name, s.Anim, cd, critical));
                var targetAlive = SkillSecondaryEffects.TargetSurvives(monHp, dmg, takeK,
                    burnHit + (petPulse?.Dmg ?? 0) + (preMiracle?.Dmg ?? 0) + casts.Where(c => c.Turn == t).Sum(c => c.Dmg));
                var proc = secondary.TryApply(s, l, t, secondaryRng, targetAlive);
                if (proc is not null)
                {
                    casts[^1] = casts[^1] with { Secondary = proc.Kind };
                    if (proc.Target == "monster") { burning.Reset(); petCombat?.ClearDebuffs(); petTargetBurning = proc.Kind == "burn"; }
                    if (proc.Kind == "burn") {
                        burnHit += burning.Ignite(m.Hp, burnHit > 0);
                        petTargetBurning = true;
                        burnDamage[^1] = burnHit; burns[^1] = true;
                    }
                    if (proc.Kind == "heal") pendingSkillHeal += (int)Math.Round(st.Hp * proc.Value / 100);
                }
            }
            if (takeK < 1) dmg = (int)Math.Round(dmg * takeK);
            dmg += burnHit + (petPulse?.Dmg ?? 0) + (preMiracle?.Dmg ?? 0); // sát thương linh thú đã tính giảm riêng
            dmg += casts.Where(c => c.Turn == t).Sum(c => c.Dmg);
            var basicCrit = !fired && rng.NextDouble() < bcrit;
            var basicHit = fired ? 0 : HitDamage(bpower * bdef.DmgBase * (1 + skillPct / 100) / 100, basicCrit);
            basicDamage.Add(basicHit); basicCritical.Add(basicCrit);
            if (fired) rage = 0;                                               // tung chiêu thay cho đòn thường, nộ về 0
            else { dmg += basicHit; rage = Math.Min(SkillData.RageMax, rage + SkillData.RagePerHit); }
            var extra = extraChance > 0 && rng.NextDouble() * 100 < extraChance; // Đạo: thêm 20 điểm %, trần 50
            var extraCrit = extra && rng.NextDouble() < bcrit;
            var extraHit = extra ? HitDamage(bpower * bdef.DmgBase * (1 + skillPct / 100) / 100, extraCrit, false) : 0;
            dmg += extraHit;
            var shatter = petCombat?.Shatter(t) == true || secondary.Shatter(t); // băng từ skill hoặc linh thú, chỉ một lần +10%
            if (shatter) dmg = (int)Math.Round(dmg * (1 + PetData.FreezeDamageBonus));
            shatters.Add(shatter);
            extraDamage.Add(extraHit); extraCritical.Add(extraCrit);
            extras.Add(extra);
            rages.Add(rage);
            petCombat?.RecordOwnerTurn();
            if(petCombat is not null && petDef is not null && monHp > dmg && petCombat.SkillReady)
            {
                var cast = petCombat.Cast(t, ref heroHp, monHp - dmg, m.Hp, m.Def, takeK);
                if (petCombat.NewDebuff.Length > 0) { burning.Reset(); secondary.ClearEnemy(); }
                dmg += cast.Dmg; guardLeft += cast.Guard;
                petCasts.Add(cast);
            }
            monHp -= dmg; turns = t + 1;
            divineTurns[t] = new(false,false,hitBoosted,divine.Charged);
            monLeft.Add(Math.Max(0, monHp));
            weakened.Add(monHp > 0 && secondary.AttackDown(t) > 0); windGuard.Add(secondary.DodgeBonus(t) > 0);
            if (monHp <= 0) {
                var finalHeal = heroHp > 0 ? Math.Min(pendingSkillHeal, Math.Max(0, st.Hp - heroHp)) : 0;
                heroHp += finalHeal;
                heroLeft.Add(heroHp); skip.Add(true); monControl.Add(""); heals.Add(finalHeal); reflects.Add(0); guards.Add(guardLeft); dodged.Add(false); if (NextWave(t)) continue; win = true; break;
            }
            var skillControl = secondary.ControlKind(t);
            var stunned = tw < stun || petCombat?.Paralyzed == true || skillControl.Length > 0;
            monControl.Add(petCombat?.Paralyzed == true ? petCombat.ControlKind : skillControl.Length > 0 ? skillControl : tw < stun ? "stun" : "");
            var evaded = false;
            var nullified = false;
            int refl = 0, heal = 0;
            if (!stunned)
            {
                if (!enraged && trait is not null && trait.EnrageAtk > 0 && monHp < m.Hp * trait.EnrageAt / 100) { enraged = true; enrageAt = t; }   // Cuồng bạo
                evaded = !p.Cheat("god") && CombatDodge.Roll(dodgeRng, Math.Min(CombatDodge.MaxChance, dodge + (petCombat?.DodgeBonus ?? 0) + secondary.DodgeBonus(t)));
                nullified = !evaded && !p.Cheat("god") && divine.Nullify(divineRng);
                if(evaded || nullified) divine.Avoided();
                var actualMonDmg = MonHit(m.Atk * (enraged && trait is not null ? 1 + trait.EnrageAtk / 100 : 1));
                counterDamage[t] = actualMonDmg;
                var hit = evaded || nullified ? 0 : actualMonDmg;
                if (guardLeft > 0) { var ab = Math.Min(guardLeft, hit); guardLeft -= ab; petCombat?.Absorb(ab); hit -= ab; }   // Khiên linh thú đỡ trước phần khiên trang bị
                heroHp = divine.Receive(heroHp,hit);
                var dealt = actualMonDmg;
                if (!evaded && !nullified && heroHp > 0 && fx.Reflect > 0 && dealt > 0) { refl = Math.Max(1, (int)Math.Round(dealt * fx.Reflect / 100 * takeK)); monHp -= refl; monLeft[^1] = Math.Max(0, monHp); }   // Địa mạch
                if (!evaded && !nullified && heroHp > 0 && actualMonDmg > 0 && petCombat?.Reflect(t, monHp, m.Def, takeK) is { } counter)
                { monHp -= counter.Dmg; monLeft[^1] = Math.Max(0, monHp); petCasts.Add(counter); }
            }
            var postMiracle=petCombat?.TryMiracle(t,ref heroHp,monHp,m.Def,takeK,false);
            if (petCombat?.NewDebuff.Length > 0) { burning.Reset(); secondary.ClearEnemy(); }
            if(postMiracle is not null){petCasts.Add(postMiracle);monHp-=postMiracle.Dmg;monLeft[^1]=Math.Max(0,monHp);}
            var woodCast = fired && equipped.Any(e => e.s.Element == "moc" && SkillData.Damage(e.s, e.l) > 0);
            var woodRecovery = woodCast ? (int)Math.Round(st.Hp * woodHeal / 100) : 0;
            if (heroHp > 0 && heroHp < st.Hp * .3 && !woodRecovered && woodEmergency > 0 && postMiracle?.Kind!="butterfly_rebirth")
            {
                woodRecovery += (int)Math.Round(st.Hp * woodEmergency / 100);
                woodRecovered = true;
            }
            var turnRecovery = regen + woodRecovery + pendingSkillHeal;
            if (heroHp > 0 && turnRecovery > 0 && postMiracle?.Kind!="butterfly_rebirth") { heal = petCombat?.Healing(turnRecovery, heroHp) ?? Math.Min(turnRecovery, st.Hp - heroHp); heroHp += heal; }
            heroLeft.Add(Math.Max(0, heroHp)); skip.Add(stunned); heals.Add(heal); reflects.Add(refl); guards.Add(guardLeft); dodged.Add(evaded);
            divineTurns[t] = new(nullified,divine.Saved,hitBoosted,divine.Charged);
            if (heroHp <= 0) break;
            if (monHp <= 0) { if (NextWave(t)) continue; win = true; break; }   // quái gục vì phản đòn
        }
        // hoạt ảnh tối đa MaxHits lượt: trận dài hơn thì lấy mẫu đều các lượt
        // (ải 3 đợt: mỗi đợt tối đa MaxWaveHits lượt, luôn giữ lượt hạ gục để client đổi quái đúng lúc)
        LastTurns = turns;
        var cap = waves.Length > 1 ? GameData.MaxWaveHits : GameData.MaxHits;
        var idxL = new List<int>(); var waveDtos = new List<WaveDto>();
        for (var w = 0; w < waveFirst.Count; w++)
        {
            int s0 = waveFirst[w], n = (w + 1 < waveFirst.Count ? waveFirst[w + 1] : turns) - s0;
            if (n <= 0) break;
            var h = Math.Min(cap, n); var start = idxL.Count;
            for (var i = 0; i < h; i++) idxL.Add(s0 + (n <= h ? i : (int)Math.Round((i + 1) * (double)n / h) - 1));
            // Chống chết, thần thông và tê liệt của linh thú: luôn giữ lượt này trong hoạt ảnh lấy mẫu
            // (tê liệt hiện ở lượt khác thì quái trông như vẫn phản công dù đã bị chặn).
            var criticalTurns=Enumerable.Range(s0,n).Where(i=>divineTurns[i].Saved)
                .Concat(casts.Where(c=>c.Secondary.Length>0&&c.Turn>=s0&&c.Turn<s0+n).Select(c=>c.Turn))
                .Concat(petCasts.Where(c=>(c.IsMiracle||c.ParalyzeTurns>0)&&c.Turn>=s0&&c.Turn<s0+n).Select(c=>c.Turn)).Distinct().ToHashSet();
            foreach(var critical in criticalTurns.OrderBy(i=>i))if(!idxL.Contains(critical)){
                var replace=Enumerable.Range(start,Math.Max(1,h-1)).Where(j=>!criticalTurns.Contains(idxL[j])).OrderBy(j=>Math.Abs(idxL[j]-critical)).FirstOrDefault(-1);
                if(replace>=0)idxL[replace]=critical;
            }
            idxL.Sort(start,h,Comparer<int>.Default);
            var wm = waves[w];
            var (slo, shi) = GameData.StoneRange(Chap(p), area, wm.Tier, p.Diff);
            waveDtos.Add(new WaveDto(wm.Name, wm.Image, wm.Tier, wm.Hp, wm.Atk, wm.Def, HeroDmgVs(wm), MonHit(wm.Atk, false), start, shi > 0 ? stoneRng.Next(slo, shi + 1) : 0));
        }
        var idx = idxL.ToArray(); var hits = idx.Length; var m0 = waves[0];
        var castShown = casts.Where(c => idx.Contains(c.Turn)).Select(c => c with { Turn = Array.IndexOf(idx, c.Turn) }).ToList();
        return new BattleDto(m0.Name, m0.Image, m0.Hp, m0.Atk, m0.Def, waveDtos[0].HeroDmg, waveDtos[0].MonsterDmg, hits, win,
            idx.Select(i => monLeft[i]).ToArray(), idx.Select(i => heroLeft[i]).ToArray(), idx.Select(i => skip[i]).ToArray(), castShown, st.Hp, basicId,
            idx.Select(i => rages[i]).ToArray(), burnDmg, canBurn ? CombatBurn.Percent : 0, idx.Select(i => burns[i]).ToArray(),
            idx.Select(i => bolts[i]).ToArray(), boltDmg, idx.Select(i => extras[i]).ToArray(), idx.Select(i => heals[i]).ToArray(),
            idx.Select(i => reflects[i]).ToArray(), guard, idx.Select(i => guards[i]).ToArray(), firstHit,
            enrageAt < 0 ? -1 : Array.FindIndex(idx, i => i >= enrageAt), monDmgEnr, waveDtos,
            idx.Select(i => basicDamage[i]).ToArray(), idx.Select(i => basicCritical[i]).ToArray(),
            idx.Select(i => extraDamage[i]).ToArray(), idx.Select(i => extraCritical[i]).ToArray(),
            idx.Select(i => dodged[i]).ToArray(), dodge,
            idx.Select(i => heroBefore[i]).ToArray(), idx.Select(i => guardBefore[i]).ToArray(), idx.Select(i => burnDamage[i]).ToArray(),
            new(ability.Tier,ability.Name,extraChance,idx.Select(i=>divineTurns[i]).ToArray()),
            activePet is null || petDef is null ? null : new PetBattleDto(activePet.Id,petDef.Name,petDef.Forms[PetAppearance(activePet)].Image,petDef.Element,
                petCasts.Select(c=>c with {Turn=Array.FindIndex(idx,i=>i>=c.Turn)}).Where(c=>c.Turn>=0).ToArray()),
            idx.Select(i => monControl[i]).ToArray(), idx.Select(i => shatters[i]).ToArray(),
            idx.Select(i => weakened[i]).ToArray(), idx.Select(i => windGuard[i]).ToArray(), idx.Select(i => counterDamage[i]).ToArray());
    }
    /// <summary>Số lượt thật của trận vừa tính (công cụ mô phỏng cân bằng).</summary>
    [ThreadStatic] public static int LastTurns;

    private static double ExploreQi(Player p) => GameData.QiRate(Realm(p), p.Stage) * GameData.ExploreQiSeconds * (1 + GearEffects(p).ExploreQi / 100);   // + Tụ linh

    internal static RealmDef Realm(Player p) => GameData.Realms[p.Realm];
    /// <summary>Chương đang đánh.</summary>
    private static ChapterDef Chap(Player p) => GameData.Chapters[Math.Clamp(p.Chapter, 0, GameData.Chapters.Count - 1)];
    /// <summary>Ải cao nhất đã mở trong chương đang đánh: chương đã qua mở hết ải.</summary>
    private static int MapAreaMax(Player p) => p.Cheat("map") ? GameData.Areas   // admin: full bản đồ
        : p.Diff == 0 ? (p.Chapter < p.ChapterMax ? GameData.Areas : p.AreaMax)
        : IsCleared(p, p.Diff, p.Chapter) ? GameData.Areas : Math.Clamp(DiffProgMap(p).GetValueOrDefault($"{p.Diff}.{p.Chapter}", 1), 1, GameData.Areas);
    private static double QiReq(Player p) => Realm(p).QiReq(p.Stage);
    private static bool IsPeak(Player p) => p.Stage == Realm(p).Stages.Length - 1;

    private static RealmDef? NextRealm(Player p) =>
        Realm(p).BreakCost is not null && p.Realm + 1 < GameData.Realms.Count ? GameData.Realms[p.Realm + 1] : null;

    private static Dictionary<string, int> TargetCost(Player p) =>
        IsPeak(p) ? Realm(p).BreakCost ?? new() : Realm(p).StageCost(p.Stage);

    private static double Chance(Player p) => p.Cheat("break") || Realm(p).BreakChance >= 1 ? 1 : Math.Clamp(Realm(p).BreakChance + p.Bonus + p.PillBonus, 0, 0.95);

    private void AddLog(Player p, string text, string kind = "")
    {
        var entry = new LogEntry { PlayerId = p.Id, Text = text, Kind = kind, At = DateTime.UtcNow };
        p.Logs.Add(entry);
        db.Logs.Add(entry);
    }

    private (string, string) Log(Player p, string text, string kind)
    {
        AddLog(p, text, kind);
        return (text, kind);
    }
}
