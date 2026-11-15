using VanDao.Data;
using static VanDao.Game.BiCanhData;
namespace VanDao.Game;

/// <summary>Thái Hư Bí Cảnh: vào (2 lượt/ngày) → chọn cửa mỗi tầng → trận tự đánh / kỳ ngộ / thương nhân / linh tuyền → cổ vật → Boss.
/// Máu giữ qua tầng (lưu theo tỉ lệ). Kết thúc (chết / rút lui / qua tầng 15) thì trao thưởng theo số tầng đã qua.</summary>
public partial class GameService
{
    private static void BiCanhRollDay(Save s, string day)
    {
        if (s.Day == day) return;
        s.Day = day; s.Entries = 0;
    }

    /// <summary>Lượt đang chơi (chưa kết thúc) ở đúng pha; sai thì báo lỗi.</summary>
    private static Run BiCanhActive(Save s, string phase)
    {
        if (s.Run is not { } run || run.Phase == "over") throw new GameException("Chưa vào Thái Hư Bí Cảnh.");
        if (run.Phase != phase) throw new GameException("Thao tác không hợp lệ ở bước này.");
        return run;
    }

    private static int BiCanhGain(Run run, string element, int stones) =>
        (int)Math.Round(stones * (1 + Total(run.Relics, element).StonePct / 100));

    /// <summary>Vào bí cảnh: tốn 1 lượt. Lượt cũ còn dở dang được kết thúc trước (vẫn nhận thưởng tầng đã qua).</summary>
    public (string, string) EnterBiCanh(Player p, Random? random = null)
    {
        if (EventNow is not { } now) throw new GameException("Cần kết nối mạng để vào Bí Cảnh.");
        if (!p.StarterChosen) throw new GameException("Chọn khởi đầu trước đã.");
        var s = Load(p);
        BiCanhRollDay(s, LoginDayOf(now));
        if (s.Run is { Phase: not "over" }) BiCanhFinish(p, s, "retreat", random);
        if (s.Entries >= DailyEntries && !Player.AdminEnabled)   // admin: không giới hạn lượt
            throw new GameException($"Hôm nay đã vào Thái Hư {DailyEntries}/{DailyEntries} lần; quay lại sau 00:00 giờ Việt Nam.");
        if (BagCap(p) - BagUsed(p) < BagNeed) throw new GameException($"Hành trang cần {BagNeed} ô trống để nhận thưởng Bí Cảnh.");
        s.Entries++;
        s.Run = new Run { Seed = (random ?? Random.Shared).Next(1, int.MaxValue), Entry = s.Entries, Realm = p.Realm, Stage = p.Stage };
        s.Run.Note($"Tiến vào Thái Hư Bí Cảnh (lượt {s.Entries}/{DailyEntries}).");
        Store(p, s);
        return ($"Tiến vào Thái Hư Bí Cảnh (lượt {s.Entries}/{DailyEntries} hôm nay).", "good");
    }

    /// <summary>Chọn cửa thứ i của tầng hiện tại.</summary>
    public (string, string) BiCanhDoor(Player p, int i, Random? random = null)
    {
        var s = Load(p);
        var run = BiCanhActive(s, "doors");
        var doors = Doors(run.Seed, run.Floor);
        if (i < 0 || i >= doors.Length) throw new GameException("Cửa không hợp lệ.");
        var kind = doors[i];
        var element = CultivationManuals.Active(p);
        run.Fights.Clear();
        string msg; var tone = "good";
        switch (kind)
        {
            case Fight or Elite:
            {
                var mon = MonAt(run.Seed, run.Floor, i, kind);
                var f = BiCanhBattle(p, run, mon, kind, i, 0);
                if (!f.Win) return BiCanhDeath(p, s, f, random);
                var gain = BiCanhGain(run, element, kind == Elite ? StonesElite : StonesFight);
                run.Stones += gain;
                msg = $"Tầng {run.Floor}: hạ {f.Name} (+{gain} Hư Thạch).";
                run.Note(msg);
                if (kind == Elite && BiCanhOffer(run, 20)) break;
                BiCanhAdvance(p, s, random);
                break;
            }
            case Boss:
            {
                var boss = BossAt(run.Floor);
                BiCanhData.Fought? f = null;
                if (boss.Mechanic == "summon")
                    for (var g = 0; g < 2; g++)
                    {
                        f = BiCanhBattle(p, run, BiCanhData.Guard, Elite, i, g + 1, "Triệu hồi", hpMul: GuardMult, atkMul: GuardMult);
                        if (!f.Win) return BiCanhDeath(p, s, f, random);
                    }
                var bossMon = new Mon(boss.Name, boss.Image);
                var note = ""; double hpMul = 1, atkMul = 1, reflect = 0;
                if (boss.Mechanic == "mirror") { reflect = MirrorReflect; note = "Phản kính"; }
                if (boss.Mechanic == "leech" && ElementCounter.Edge(MonsterElements.Of(boss.Image), element) == 1)
                {
                    hpMul = LeechHp; atkMul = LeechAtk;
                    note = "Hút máu (khắc hệ)";
                }
                f = BiCanhBattle(p, run, bossMon, Boss, i, 0, note, hpMul, atkMul, reflect);
                if (!f.Win) return BiCanhDeath(p, s, f, random);
                var gain = BiCanhGain(run, element, StonesBoss);
                run.Stones += gain;
                msg = $"Tầng {run.Floor}: hạ Boss {boss.Name} (+{gain} Hư Thạch)!";
                run.Note(msg); tone = "gold";
                if (run.Floor < MaxFloor && BiCanhOffer(run, 20)) break;
                BiCanhAdvance(p, s, random);
                break;
            }
            case Event:
                run.Phase = Event; run.EventId = EventAt(run.Seed, run.Floor).Id;
                msg = $"Tầng {run.Floor}: kỳ ngộ · {EventById[run.EventId].Title}.";
                break;
            case Shop:
                run.Phase = Shop; run.Offer = OfferRelics(run.Seed, run.Floor, 30, run.Relics, 2); run.ShopBought.Clear();
                msg = $"Tầng {run.Floor}: gặp thương nhân Thái Hư.";
                break;
            default:   // Spring
            {
                var before = run.Hp;
                run.Hp = Math.Min(1, run.Hp + SpringHeal);
                msg = $"Tầng {run.Floor}: Linh tuyền hồi {Math.Round((run.Hp - before) * 100)}% máu.";
                run.Note(msg);
                BiCanhAdvance(p, s, random);
                break;
            }
        }
        Store(p, s);
        return (msg, tone);
    }

    /// <summary>Chọn cổ vật thứ i trong 3 món mời (i = -1: bỏ qua).</summary>
    public (string, string) BiCanhRelic(Player p, int i, Random? random = null)
    {
        var s = Load(p);
        var run = BiCanhActive(s, "relic");
        if (i < -1 || i >= run.Offer.Count) throw new GameException("Cổ vật không hợp lệ.");
        string msg;
        if (i < 0) msg = "Bỏ qua cổ vật.";
        else
        {
            var r = RelicById[run.Offer[i]];
            var sets = ActiveSets(run.Relics).Select(x => x.Id).ToHashSet();
            run.Relics.Add(r.Id);
            msg = $"Nhận cổ vật {r.Name}.";
            foreach (var set in ActiveSets(run.Relics).Where(x => !sets.Contains(x.Id))) msg += $" Đủ bộ {set.Name}: {set.Text}!";
        }
        run.Note(msg);
        run.Offer.Clear();
        BiCanhAdvance(p, s, random);
        Store(p, s);
        return (msg, i < 0 ? "" : "gold");
    }

    /// <summary>Trả lời kỳ ngộ: c = 0 / 1.</summary>
    public (string, string) BiCanhEvent(Player p, int c, Random? random = null)
    {
        var s = Load(p);
        var run = BiCanhActive(s, Event);
        if (c is not (0 or 1) || !EventById.TryGetValue(run.EventId, out var ev)) throw new GameException("Lựa chọn không hợp lệ.");
        var ch = c == 0 ? ev.A : ev.B;
        var win = ch.Chance >= 1 || (random ?? Random.Shared).NextDouble() < ch.Chance;
        var o = win ? ch.Win : ch.Lose ?? ch.Win;
        var parts = new List<string>();
        if (o.Hp != 0)
        {
            var before = run.Hp;
            run.Hp = Math.Clamp(run.Hp + o.Hp, .01, 1);   // kỳ ngộ không giết người chơi
            parts.Add($"máu {(run.Hp - before) * 100:+0;-0}%");
        }
        if (o.Stones > 0) { var g = BiCanhGain(run, CultivationManuals.Active(p), o.Stones); run.Stones += g; parts.Add($"+{g} Hư Thạch"); }
        if (o.Relic && OfferRelics(run.Seed, run.Floor, 40, run.Relics, 1).FirstOrDefault() is { } rid)
        { run.Relics.Add(rid); parts.Add($"cổ vật {RelicById[rid].Name}"); }
        var msg = $"{ev.Title}: {o.Text}{(parts.Count > 0 ? $" ({string.Join(", ", parts)})" : "")}";
        run.Note(msg);
        run.EventId = "";
        BiCanhAdvance(p, s, random);
        Store(p, s);
        return (msg, win ? "good" : "bad");
    }

    /// <summary>Thương nhân: i = chỉ số cổ vật trên kệ, i = số món trên kệ: hồi máu, i = -1: rời đi.</summary>
    public (string, string) BiCanhShop(Player p, int i, Random? random = null)
    {
        var s = Load(p);
        var run = BiCanhActive(s, Shop);
        if (i < 0)
        {
            run.Note($"Tầng {run.Floor}: rời thương nhân.");
            run.Offer.Clear(); run.ShopBought.Clear();
            BiCanhAdvance(p, s, random);
            Store(p, s);
            return ("Rời thương nhân.", "");
        }
        if (i > run.Offer.Count) throw new GameException("Món hàng không hợp lệ.");
        if (run.ShopBought.Contains(i)) throw new GameException("Món này đã mua.");
        var heal = i == run.Offer.Count;
        var cost = heal ? ShopHealCost : ShopRelicCost;
        if (run.Stones < cost) throw new GameException($"Cần {cost} Hư Thạch.");
        run.Stones -= cost; run.ShopBought.Add(i);
        string msg;
        if (heal) { run.Hp = Math.Min(1, run.Hp + ShopHeal); msg = $"Mua linh dược: hồi {ShopHeal * 100:0}% máu (−{cost} Hư Thạch)."; }
        else { var r = RelicById[run.Offer[i]]; run.Relics.Add(r.Id); msg = $"Mua cổ vật {r.Name} (−{cost} Hư Thạch)."; }
        run.Note(msg);
        Store(p, s);
        return (msg, "good");
    }

    /// <summary>Rút lui: kết thúc lượt sớm, nhận thưởng các tầng đã qua.</summary>
    public (string, string) BiCanhLeave(Player p, Random? random = null)
    {
        var s = Load(p);
        if (s.Run is not { Phase: not "over" }) throw new GameException("Chưa vào Thái Hư Bí Cảnh.");
        var r = BiCanhFinish(p, s, "retreat", random);
        Store(p, s);
        return r;
    }

    /// <summary>Đóng bảng tổng kết của lượt đã kết thúc.</summary>
    public (string, string) BiCanhClose(Player p)
    {
        var s = Load(p);
        if (s.Run is { Phase: "over" }) { s.Run = null; Store(p, s); }
        return ("", "");
    }

    /// <summary>Mời chọn 3 cổ vật; false nếu đã có đủ mọi cổ vật.</summary>
    private static bool BiCanhOffer(Run run, int salt)
    {
        run.Offer = OfferRelics(run.Seed, run.Floor, salt, run.Relics);
        if (run.Offer.Count == 0) return false;
        run.Phase = "relic";
        return true;
    }

    /// <summary>Qua tầng hiện tại: sang tầng sau, hoặc kết thúc khi đã qua tầng 15.</summary>
    private void BiCanhAdvance(Player p, Save s, Random? random)
    {
        var run = s.Run!;
        run.Cleared = Math.Max(run.Cleared, run.Floor);
        if (run.Floor >= MaxFloor) { BiCanhFinish(p, s, "clear", random); return; }
        run.Floor++; run.Phase = "doors"; run.Offer.Clear(); run.EventId = "";
    }

    private (string, string) BiCanhDeath(Player p, Save s, BiCanhData.Fought f, Random? random)
    {
        s.Run!.Note($"Tầng {s.Run.Floor}: bại dưới tay {f.Name}.");
        var r = BiCanhFinish(p, s, "dead", random);
        Store(p, s);
        return r;
    }

    /// <summary>Kết thúc lượt: trao thưởng theo tầng đã qua, giữ bảng tổng kết tới khi đóng.</summary>
    private (string, string) BiCanhFinish(Player p, Save s, string end, Random? random)
    {
        var run = s.Run!;
        var loot = BiCanhData.Rewards(run.Cleared, run.Realm, random ?? Rng(run.Seed, 99, 9));
        foreach (var (item, qty) in loot) p.Add(item, qty);
        run.Rewards = loot; run.Phase = "over"; run.End = end; run.Offer.Clear(); run.EventId = "";
        s.Best = Math.Max(s.Best, run.Cleared);
        var how = end switch { "dead" => "bại trận", "clear" => "chinh phục cả 15 tầng", _ => "rút lui" };
        var gains = string.Join(", ", loot.Select(kv => $"+{kv.Value:N0} {GameData.ItemById[kv.Key].Name}"));
        return Log(p, $"Thái Hư Bí Cảnh: {how} sau {run.Cleared}/{MaxFloor} tầng{(gains.Length > 0 ? $" ({gains})" : "")}.", end == "dead" ? "bad" : "gold");
    }

    /// <summary>Đánh một trận trong bí cảnh theo cơ chế Lôi đài / Boss tông (PvpCombat): quái là bản sao của người chơi nhân hệ số theo loại + tầng
    /// (máu hữu hạn, không linh thú / thần thông), máu người chơi lấy từ máu giữ qua tầng. Cổ vật: công +X% nhân vào công, giảm sát thương Y% ⇒ công quái ×(1−Y),
    /// máu tối đa ×. Diễn biến trận (DuelDto) lưu trong Fought để client hiện panel thông tin trận chiến (js/duel.js).</summary>
    private static BiCanhData.Fought BiCanhBattle(Player p, Run run, Mon mon, string kind, int door, int sub, string note = "",
        double hpMul = 1, double atkMul = 1, double reflect = 0)
    {
        var mods = Total(run.Relics, CultivationManuals.Active(p));
        var me0 = PvpFighterOf(p);
        var maxHp = Math.Max(1m, CombatNumber.Scale(me0.Hp, MaxHpMult(mods)));
        var start = Math.Max(1m, CombatNumber.Scale(maxHp, run.Hp));
        var me = me0 with { Atk = CombatNumber.Scale(me0.Atk, AtkMult(mods, run.Cleared, kind)), Hp = start };
        var (h, a, d) = kind switch { Elite => EliteMult, Boss => BossMult(run.Floor), _ => FightMult };
        var ramp = 1 + FloorRamp * (run.Floor - 1);
        var foe = me0 with
        {
            Name = mon.Name, Pet = null, Divine = 0, Reflect = me0.Reflect + reflect,
            Hp = Math.Max(1m, CombatNumber.Scale(me0.Hp, h * ramp * hpMul * MonsterEase)),
            Atk = Math.Max(1m, CombatNumber.Scale(me0.Atk, a * ramp * atkMul * MonsterEase * (1 - Reduce(mods, kind) / 100))),
            Def = CombatNumber.Scale(me0.Def, d),
        };
        var seed = unchecked(run.Seed * 31 + run.Floor * 1009 + door * 101 + sub * 7);
        var r = PvpCombat.Run(me, foe, seed);
        var last = r.Actions.Count > 0 ? r.Actions[^1].HpA : r.A.Hp;
        var left = Math.Clamp((double)(last / (maxHp * PvpCombat.HpMult)), 0, 1);
        var before = run.Hp;
        var win = r.Win && left > 0;
        var crits = r.Actions.Where(x => x.ByA).Sum(x => (x.BasicCrit ? 1 : 0) + (x.ExtraCrit ? 1 : 0) + x.Casts.Count(c => c.Critical));
        double healed = 0;
        if (win)
        {
            run.Hp = left;
            var heal = Math.Min(30, crits * mods.CritHealPct) + mods.WinHealPct;   // hồi theo đòn bạo tối đa 30%/trận
            heal = heal * (1 + mods.HealMult) / 100;
            var after = Math.Min(1, run.Hp + heal);
            healed = after - run.Hp; run.Hp = after;
        }
        else run.Hp = 0;
        var duel = DuelLog.Of(r);
        duel = duel with { Me = duel.Me with { Cur = start * PvpCombat.HpMult }, Foe = duel.Foe with { Name = mon.Name, Hero = mon.Image } };
        var f = new BiCanhData.Fought
        {
            Name = mon.Name, Image = mon.Image, Kind = kind, Win = win, Turns = r.Actions.Count == 0 ? 0 : r.Actions.Max(x => x.Round), Crits = crits,
            HpBefore = before, HpAfter = run.Hp, Healed = healed, MonsterHp = foe.Hp, MonsterAtk = foe.Atk, Note = note, Duel = duel,
        };
        run.Fights.Add(f);
        return f;
    }

    public BiCanhDto BiCanhStateFor(Player p)
    {
        var s = Load(p);
        var day = LoginDayOf(EventNow ?? DateTimeOffset.UtcNow);
        var used = s.Day == day ? Math.Min(s.Entries, DailyEntries) : 0;
        return new(used, DailyEntries, s.Best, s.Run is null ? null : BiCanhRunState(p, s.Run), Player.AdminEnabled);
    }

    private static BiCanhRelicDto RelicDto(string id)
    {
        var r = RelicById[id];
        return new(r.Id, r.Name, r.Icon, r.Text, Sets.First(x => x.Id == r.Set).Name);
    }

    private static BiCanhRunDto BiCanhRunState(Player p, Run run)
    {
        var element = CultivationManuals.Active(p);
        var mods = Total(run.Relics, element);
        var maxHp = CombatNumber.Round((double)Stats(p).Hp * MaxHpMult(mods));
        var doors = new List<BiCanhDoorDto>();
        if (run.Phase == "doors")
            doors = Doors(run.Seed, run.Floor).Select((k, i) =>
            {
                if (k == Boss) { var b = BossAt(run.Floor); return new BiCanhDoorDto(k, DoorNames[k], DoorHints[k], b.Name, b.Image, b.MechanicText); }
                if (k is Fight or Elite) { var m = MonAt(run.Seed, run.Floor, i, k); return new BiCanhDoorDto(k, DoorNames[k], DoorHints[k], m.Name, m.Image, null); }
                return new BiCanhDoorDto(k, DoorNames[k], DoorHints[k], null, null, null);
            }).ToList();
        BiCanhEventDto? ev = null;
        if (run.Phase == Event && EventById.TryGetValue(run.EventId, out var e))
            ev = new(e.Title, e.Text, new[] { e.A, e.B }.Select(c => new BiCanhChoiceDto(c.Label, (int)Math.Round(c.Chance * 100))).ToList());
        var shop = new List<BiCanhShopDto>();
        if (run.Phase == Shop)
        {
            shop = run.Offer.Select((id, i) => { var r = RelicById[id]; return new BiCanhShopDto("relic", r.Name, r.Text, r.Icon, ShopRelicCost, run.ShopBought.Contains(i)); }).ToList();
            shop.Add(new("heal", "Linh dược", $"Hồi {ShopHeal * 100:0}% máu", "🧪", ShopHealCost, run.ShopBought.Contains(run.Offer.Count)));
        }
        var owned = run.Relics.Where(RelicById.ContainsKey).Select(id => RelicById[id].Set).ToList();
        var sets = Sets.Where(x => owned.Contains(x.Id)).Select(x => new BiCanhSetDto(x.Name, owned.Count(o => o == x.Id), x.Need, x.Text, owned.Count(o => o == x.Id) >= x.Need)).ToList();
        var rewards = run.Rewards ?? BiCanhData.Rewards(run.Cleared, run.Realm, Rng(run.Seed, 99, 9));
        return new(run.Floor, MaxFloor, run.Cleared, run.Hp, maxHp, run.Stones, run.Phase, run.End, doors,
            run.Relics.Where(RelicById.ContainsKey).Select(RelicDto).ToList(),
            run.Phase == "relic" ? run.Offer.Select(RelicDto).ToList() : [], ev, shop, sets,
            run.Fights.Select(f => new BiCanhFightDto(f.Name, f.Image, f.Kind, f.Win, f.Turns, f.Crits, f.HpBefore, f.HpAfter, f.Healed, f.MonsterHp, f.MonsterAtk, f.Note, f.Duel)).ToList(),
            run.Log.ToList(), rewards.Select(kv => new ItemQtyDto(kv.Key, kv.Value)).ToList(),
            Math.Round((AtkMult(mods, run.Cleared, Fight) - 1) * 100), Reduce(mods, Fight));
    }
}
