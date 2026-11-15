using Microsoft.Extensions.Logging.Abstractions;
using VanDao.Data;
using VanDao.Game;
using static VanDao.Game.BiCanhData;

namespace GemSystemTests;

/// <summary>Thái Hư Bí Cảnh: lượt/ngày, sinh cửa, pha, cổ vật, máu giữ qua tầng, chết vẫn nhận thưởng.</summary>
public class BiCanhTests
{
    private sealed class Roll(double value) : Random { public override double NextDouble() => value; }

    private static GameService Game() =>
        new(null!, version: new VersionService(new LeaderboardOptions(), NullLogger<VersionService>.Instance) { UseOwnClock = true });

    /// <summary>Tu sĩ rất mạnh: đánh thắng mọi quái bí cảnh cảnh giới 0, gần như không mất máu.</summary>
    private static Player Strong()
    {
        var p = new Player { Id = Guid.Parse("40000000-0000-0000-0000-0000000000c1"), Realm = 0, BodyHp = 1_000_000_000_000, BodyDef = 1_000_000_000_000,
            LearnedManuals = "hoa", ActiveManual = "hoa", SkillSlots = "hoa_cau," };
        p.SetSkillLevels(SkillData.Skills.Where(s => s.Element == "hoa" && s.Tree != "basic").ToDictionary(s => s.Id, s => s.MaxLv));
        return p;
    }

    private static Run Mutate(Player p, Action<Run> f)
    {
        var s = Load(p); f(s.Run!); Store(p, s); return s.Run!;
    }
    private static Run RunOf(Player p) => Load(p).Run!;
    private static int SeedWith(int floor, string kind) => Enumerable.Range(1, 5000).First(seed => Doors(seed, floor).Contains(kind));

    [Fact]
    public void Two_entries_per_day_and_reset_on_new_day()
    {
        var game = Game(); var p = new Player();
        game.EnterBiCanh(p); game.BiCanhLeave(p);
        game.EnterBiCanh(p); game.BiCanhLeave(p);
        Assert.Throws<GameException>(() => game.EnterBiCanh(p));
        Assert.Equal(2, game.BiCanhStateFor(p).Used);
        var s = Load(p); s.Day = "2000-01-01"; Store(p, s);   // sang ngày mới
        game.EnterBiCanh(p);
        Assert.Equal(1, game.BiCanhStateFor(p).Used);
    }

    [Fact]
    public void Doors_are_deterministic_and_follow_floor_rules()
    {
        for (var seed = 1; seed <= 300; seed++)
            for (var f = 1; f <= MaxFloor; f++)
            {
                var d = Doors(seed, f);
                Assert.Equal(d, Doors(seed, f));
                if (IsBossFloor(f)) { Assert.Equal([Boss], d); continue; }
                Assert.InRange(d.Length, 2, 3);
                Assert.Equal(d.Length, d.Distinct().Count());
                Assert.Contains(d, k => k is Fight or Elite);
                if (f % 5 == 4) Assert.Contains(Spring, d);
                if (f == 1) Assert.DoesNotContain(d, k => k is Elite or Shop);
            }
    }

    [Fact]
    public void Actions_in_wrong_phase_are_rejected()
    {
        var game = Game(); var p = new Player();
        Assert.Throws<GameException>(() => game.BiCanhDoor(p, 0));   // chưa vào
        game.EnterBiCanh(p);
        Assert.Throws<GameException>(() => game.BiCanhRelic(p, 0));
        Assert.Throws<GameException>(() => game.BiCanhEvent(p, 0));
        Assert.Throws<GameException>(() => game.BiCanhShop(p, 0));
        Assert.Throws<GameException>(() => game.BiCanhDoor(p, 5));
    }

    [Fact]
    public void Relic_mods_sets_and_element_seals()
    {
        Assert.Equal(35, Total(["cuong_huyet"], "hoa").AtkPct);
        Assert.Equal(-20, Total(["cuong_huyet"], "hoa").MaxHpPct);
        Assert.Equal(1, Total(["huyet_bao", "am_huyet", "cuong_huyet"], "hoa").HealMult);   // đủ bộ Huyết Hải
        Assert.Equal(0, Total(["huyet_bao", "am_huyet"], "hoa").HealMult);
        Assert.Equal(ElementPct, Total(["tam_an_hoa"], "hoa").AtkPct);
        Assert.Equal(-ElementWeakPct, Total(["tam_an_hoa"], "loi").AtkPct);   // Hỏa khắc Lôi
        Assert.Equal(0, Total(["tam_an_hoa"], "moc").AtkPct);
        // đủ 3 Tâm Ấn: Hỏa +30, Thủy khắc Hỏa −15, Mộc trung tính, bộ Ngũ Hành +20
        Assert.Equal(ElementPct - ElementWeakPct + 20, Total(["tam_an_hoa", "tam_an_thuy", "tam_an_moc"], "hoa").AtkPct);
        var m = Total(["bac_thang"], "hoa");
        Assert.Equal(1 + 3 * 4 / 100.0, AtkMult(m, 4, Fight), 9);
        Assert.Equal(30, Reduce(Total(["pha_ma"], "hoa"), Elite));
        Assert.Equal(0, Reduce(Total(["pha_ma"], "hoa"), Fight));
    }

    [Fact]
    public void Spring_heals_and_hp_carries_over()
    {
        var game = Game(); var p = Strong();
        game.EnterBiCanh(p);
        Mutate(p, r => { r.Floor = 4; r.Hp = .5; });
        game.BiCanhDoor(p, Array.IndexOf(Doors(RunOf(p).Seed, 4), Spring));
        var run = RunOf(p);
        Assert.Equal(.85, run.Hp, 9);
        Assert.Equal(5, run.Floor);
        Assert.Equal(4, run.Cleared);
        Mutate(p, r => { r.Floor = 9; r.Hp = .9; });
        game.BiCanhDoor(p, Array.IndexOf(Doors(RunOf(p).Seed, 9), Spring));
        Assert.Equal(1, RunOf(p).Hp, 9);   // không vượt 100%
    }

    [Fact]
    public void Elite_win_offers_relic_then_advances()
    {
        var game = Game(); var p = Strong();
        game.EnterBiCanh(p);
        var seed = SeedWith(2, Elite);
        Mutate(p, r => { r.Seed = seed; r.Floor = 2; r.Cleared = 1; });
        game.BiCanhDoor(p, Array.IndexOf(Doors(seed, 2), Elite));
        var run = RunOf(p);
        Assert.Equal("relic", run.Phase);
        Assert.Equal(3, run.Offer.Count);
        Assert.Equal(StonesElite, run.Stones);
        Assert.True(run.Fights.Single().Win);
        var pick = run.Offer[1];
        game.BiCanhRelic(p, 1);
        run = RunOf(p);
        Assert.Equal([pick], run.Relics);
        Assert.Equal(("doors", 3, 2), (run.Phase, run.Floor, run.Cleared));
    }

    [Fact]
    public void Death_ends_run_but_keeps_rewards_of_cleared_floors()
    {
        var game = Game(); var p = new Player();
        game.EnterBiCanh(p);
        var seed = SeedWith(3, Fight);
        Mutate(p, r => { r.Seed = seed; r.Floor = 3; r.Cleared = 2; r.Hp = 1e-15; });
        game.BiCanhDoor(p, Array.IndexOf(Doors(seed, 3), Fight));
        var run = RunOf(p);
        Assert.Equal(("over", "dead", 2), (run.Phase, run.End, run.Cleared));
        Assert.Equal(KyDuyen.StoneUnit(0) * 3, p.Count("linh_thach"));
        Assert.Equal(4, p.Count(ForgeData.Iron));
        Assert.Throws<GameException>(() => game.BiCanhDoor(p, 0));
        game.BiCanhClose(p);
        Assert.Null(Load(p).Run);
    }

    [Fact]
    public void Rewards_grow_with_depth_and_floor_15_gives_rare_chest()
    {
        Assert.Empty(Rewards(0, 0, new Random(1)));
        var r5 = Rewards(5, 2, new Random(1)); var r15 = Rewards(15, 2, new Random(1));
        Assert.True(r15["linh_thach"] > r5["linh_thach"]);
        Assert.Equal(2 * 15 + 5 * 3, r15[ForgeData.Iron]);
        Assert.Equal(3, r15.Where(kv => GemData.SoStones.Contains(kv.Key)).Sum(kv => kv.Value));
        Assert.Equal(1, r15["ruong_hiem"]);
        Assert.False(r5.ContainsKey("ruong_hiem"));
        Assert.Equal(5, r15[GameData.PillChain[2]]);
    }

    [Fact]
    public void Entering_again_settles_the_unfinished_run()
    {
        var game = Game(); var p = new Player();
        game.EnterBiCanh(p);
        Mutate(p, r => { r.Floor = 4; r.Cleared = 3; });
        game.EnterBiCanh(p);
        Assert.Equal(6, p.Count(ForgeData.Iron));
        Assert.Equal((1, 0), (RunOf(p).Floor, RunOf(p).Cleared));
        Assert.Equal(3, Load(p).Best);
    }

    [Fact]
    public void Event_choice_uses_chance_and_never_kills()
    {
        var game = Game(); var p = new Player();
        game.EnterBiCanh(p);
        Mutate(p, r => { r.Phase = Event; r.EventId = "linh_qua"; r.Hp = .5; });
        game.BiCanhEvent(p, 0, new Roll(.1));   // 60%: thành công
        Assert.Equal(.8, RunOf(p).Hp, 9);
        Mutate(p, r => { r.Phase = Event; r.EventId = "linh_qua"; r.Hp = .1; });
        game.BiCanhEvent(p, 0, new Roll(.99));  // thất bại: −15% nhưng không chết
        Assert.Equal(.01, RunOf(p).Hp, 9);
        Assert.Equal("doors", RunOf(p).Phase);
    }

    [Fact]
    public void Shop_sells_heal_and_relics_for_void_stones()
    {
        var game = Game(); var p = new Player();
        game.EnterBiCanh(p);
        Mutate(p, r => { r.Phase = Shop; r.Offer = ["ho_than", "tu_bao"]; r.Hp = .3; r.Stones = ShopHealCost + ShopRelicCost - 1; });
        game.BiCanhShop(p, 2);   // linh dược
        Assert.Equal(.7, RunOf(p).Hp, 9);
        Assert.Throws<GameException>(() => game.BiCanhShop(p, 2));   // đã mua
        Assert.Throws<GameException>(() => game.BiCanhShop(p, 0));   // thiếu Hư Thạch
        Mutate(p, r => r.Stones = ShopRelicCost);
        game.BiCanhShop(p, 0);
        Assert.Contains("ho_than", RunOf(p).Relics);
        game.BiCanhShop(p, -1);
        Assert.Equal(("doors", 2), (RunOf(p).Phase, RunOf(p).Floor));
    }

    [Fact]
    public void Boss_floors_mirror_and_summon_then_full_clear()
    {
        var game = Game(); var p = Strong();
        game.EnterBiCanh(p);
        Mutate(p, r => { r.Floor = 5; r.Cleared = 4; });
        game.BiCanhDoor(p, 0);
        Assert.Equal("relic", RunOf(p).Phase);
        Assert.Equal(StonesBoss, RunOf(p).Stones);
        Mutate(p, r => { r.Phase = "doors"; r.Offer.Clear(); r.Floor = 15; r.Cleared = 14; });
        game.BiCanhDoor(p, 0);
        var run = RunOf(p);
        Assert.Equal(3, run.Fights.Count);   // 2 Hư Linh Vệ + Boss
        Assert.Equal(("over", "clear", 15), (run.Phase, run.End, run.Cleared));
        Assert.Equal(1, p.Count("ruong_hiem"));
        Assert.Equal(15, Load(p).Best);
    }
}
