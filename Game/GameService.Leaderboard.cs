using VanDao.Data;

namespace VanDao.Game;

public partial class GameService
{
    // Highest completed stage, ordered by difficulty, chapter, then stage.
    // AreaMax is the next unlocked stage, not a completed stage.
    public static int LeaderboardProgress(Player p)
    {
        var best = 0;
        for (var d = 0; d < GameData.DiffNames.Length; d++)
        for (var c = 0; c < GameData.OpenChapters; c++)
        {
            var cleared = IsCleared(p, d, c);
            var unlocked = d == 0 ? (c < p.ChapterMax ? GameData.AreasFor(d) : c == p.ChapterMax ? p.AreaMax : 1)
                : DiffProgMap(p).GetValueOrDefault($"{d}.{c}", 1);
            var completed = cleared ? GameData.AreasFor(d) : Math.Clamp(unlocked - 1, 0, GameData.AreasFor(d) - 1);
            if (completed > 0) best = Math.Max(best, d * 10000 + c * 100 + completed);
        }
        return best;
    }

    private static long RankedPetPower(Player p, Pet pet)
    {
        var s = PetStats(p, pet);
        return CombatRatings.Power(s.Atk, s.Def, s.Hp, new(s.Crit, s.CritResist, s.Accuracy, s.Dodge,
            s.Combo, s.ComboResist, s.Reflect, s.ReflectResist));
    }
    public static Pet? LeaderboardBestPet(Player p) => p.Pets
        .Where(x => PetData.ById.ContainsKey(x.DefId))
        .OrderByDescending(x => RankedPetPower(p, x)).ThenBy(x => x.Id).FirstOrDefault();
    public static long LeaderboardPetPower(Player p) => LeaderboardBestPet(p) is { } pet ? RankedPetPower(p, pet) : 0;
}
