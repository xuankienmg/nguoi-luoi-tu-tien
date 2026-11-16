using VanDao.Data;

namespace VanDao.Game;

public partial class GameService
{
    // Highest completed stage, ordered by difficulty, chapter, then stage.
    // AreaMax is the next unlocked stage, not a completed stage.
    public static int LeaderboardProgress(Player p)
    {
        var best = 0;
        for (var d = 0; d < EndlessData.Diff; d++)
        for (var c = 0; c < GameData.OpenChapters; c++)
        {
            var cleared = IsCleared(p, d, c);
            var unlocked = d == 0 ? (c < p.ChapterMax ? GameData.AreasFor(d) : c == p.ChapterMax ? p.AreaMax : 1)
                : DiffProgMap(p).GetValueOrDefault($"{d}.{c}", 1);
            var completed = cleared ? GameData.AreasFor(d) : Math.Clamp(unlocked - 1, 0, GameData.AreasFor(d) - 1);
            if (completed > 0) best = Math.Max(best, d * 10000 + c * 100 + completed);
        }
        // Thiên Giới: 50000 + số tầng đã hạ (tối đa EndlessData.MaxFloor; ô "chương" của mã không dùng) → xếp trên mọi mức khác trong bảng Vượt ải
        var floors = EndlessData.Cleared(DiffProgMap(p).GetValueOrDefault($"{EndlessData.Diff}.0", 1));
        if (floors > 0) best = Math.Max(best, EndlessData.Diff * 10000 + floors);
        return best;
    }

    private static decimal RankedPetPower(Player p, Pet pet)
    {
        var s = PetStats(p, pet);
        return CombatRatings.Power(s.Atk, s.Def, s.Hp, new(s.Crit, s.CritResist, s.Accuracy, s.Dodge,
            s.Combo, s.ComboResist, s.Reflect, s.ReflectResist));
    }
    public static Pet? LeaderboardBestPet(Player p) => p.Pets
        .Where(x => PetData.ById.ContainsKey(x.DefId))
        .OrderByDescending(x => RankedPetPower(p, x)).ThenBy(x => x.Id).FirstOrDefault();
    public static decimal LeaderboardPetPower(Player p) => LeaderboardBestPet(p) is { } pet ? RankedPetPower(p, pet) : 0;
}
