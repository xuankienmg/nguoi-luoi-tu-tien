using System.Globalization;
using VanDao.Game;
using static VanDao.Game.ForgeData;
using Xunit.Abstractions;
namespace GemSystemTests;
public class ForgeRelicBalanceTests(ITestOutputHelper output)
{
    [Fact] public void CompareAllFourRelicChoicesAtProgressionMilestones()
    {
        var ids=ForgeRelicData.All.Select(r=>r.Id).ToArray();
        var sets=new List<string[]>{Array.Empty<string>()};
        sets.AddRange(ids.Select(id=>new[]{id}));
        for(int a=0;a<ids.Length;a++)for(int b=a+1;b<ids.Length;b++)
        for(int c=b+1;c<ids.Length;c++)for(int d=c+1;d<ids.Length;d++)sets.Add([ids[a],ids[b],ids[c],ids[d]]);
        var report=new List<string>{"level,relics,expected_level_change,stones_per_strike,iron_per_strike,unshielded_break_chance"};
        foreach(var l in new[]{0,30,100,149})
        {
            double best=double.NegativeInfinity;string choice="";
            foreach(var set in sets)
            {
                var m=ForgeRelicData.ModsOf(set);
                // Fixed-level sampling isolates the local risk/cost and includes real charge resets on break.
                const int samples=1000;
                var rng=new Random(20261124+l);var state=new SlotState{L=l};double drift=0,stones=0,iron=0;
                for(var n=0;n<samples;n++)
                {
                    var o=OddsAt(l,state.F>0,m);var cost=StoneCost(GameData.ItemById["linh_thach"],l,m);
                    var outcome=Roll(o,rng.NextDouble());
                    stones+=cost*(outcome==Outcome.Hold?1-m.HoldRefund:1);
                    iron+=m.IronFreeChance>0&&rng.NextDouble()<m.IronFreeChance?0:IronCost(l,m);
                    state=Apply(state,outcome,m,rng);drift+=state.L-l;state.L=l;
                }
                drift/=samples;stones/=samples;iron/=samples;
                Assert.True(double.IsFinite(drift));Assert.InRange(stones,0,10000);Assert.True(iron>=.75);
                if(drift>best){best=drift;choice=string.Join('|',set);}
                report.Add(string.Join(',',l,string.Join('|',set),drift.ToString("F6",CultureInfo.InvariantCulture),stones.ToString("F2",CultureInfo.InvariantCulture),iron.ToString("F2",CultureInfo.InvariantCulture),OddsAt(l,false,m).Break.ToString("F6",CultureInfo.InvariantCulture)));
            }
            output.WriteLine($"+{l}: highest estimated level drift {best:F4}, {choice}");
        }
        var path=Environment.GetEnvironmentVariable("CODEX_FORGE_BALANCE_REPORT");
        if(!string.IsNullOrEmpty(path))File.WriteAllLines(path,report);
        output.WriteLine("1000 seeded strikes per fixed-level loadout include real triple gains, charge cycles, break resets, iron savings and refunds. Sampling estimates are not a time-to-cap forecast.");
    }
}
