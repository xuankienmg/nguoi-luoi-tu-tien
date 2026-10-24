using System.Reflection;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;
using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
var game = new GameService(db);
var method = typeof(GameService).GetMethod("ExploreSeconds", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)!;
var checks=0;
foreach(var realm in new[]{0,1,2,4,6}) foreach(var area in new[]{1,10,16}) {
    var p=new Player{Realm=realm,Area=area};
    var battle=GameService.Battle(p);
    var speed=GameService.Stats(p).Speed;
    var raw=GameData.WalkSeconds + battle.Hits*GameData.HitSeconds + (Math.Max(1,battle.Waves?.Count??1)-1)*GameData.WaveSeconds + GameData.EndSeconds;
    var expected=Math.Max(GameData.MinExploreSeconds, raw*(1-Math.Clamp(speed,0,60)/100));
    var actual=(double)method.Invoke(method.IsStatic?null:game,new object[]{p})!;
    if(Math.Abs(actual-expected)>1e-8)throw new Exception($"Timing mismatch realm={realm} area={area}: {actual} vs {expected}");
    if(speed>0&&raw>GameData.MinExploreSeconds&&actual>=raw)throw new Exception("Agility did not shorten combat trip");
    checks++;
}
Console.WriteLine($"PASS: server travel/combat timing and minimum duration, {checks} cases.");
