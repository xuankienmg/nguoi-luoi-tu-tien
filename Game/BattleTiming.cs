namespace VanDao.Game;

public static class BattleTiming
{
    public const double ExtraAttackSeconds = .34, PetCastSeconds = .8;
    // 42 charge/flight frames + 18 explosion frames at 30 FPS, plus the counter.
    public const double XichDiemTurnSeconds = 2 + .25;

    public static double CombatSeconds(BattleDto battle, double moveSpeed = 0)
    {
        var petTurns = battle.Pet?.Casts.Where(c => c.At < .85 && c.Turn >= 0 && c.Turn < battle.Hits)
            .Select(c => c.Turn).Distinct().Count() ?? 0;
        return battle.Hits * GameData.HitSeconds
            + Enumerable.Range(0, battle.Hits).Count(turn => battle.Casts
                .Where(c => c.Turn == turn && (c.Dmg > 0 || c.Skill == "xich_diem_thien")).OrderByDescending(c => c.Dmg)
                .FirstOrDefault()?.Skill == "xich_diem_thien") * (XichDiemTurnSeconds - GameData.HitSeconds)
            + (battle.Extra?.Take(battle.Hits).Count(extra => extra) ?? 0) * ExtraAttackSeconds
            + petTurns * PetCastSeconds
            + Math.Max(0, (battle.Waves?.Count ?? 1) - 1) * GameData.WaveSeconds / (1 + Math.Max(0, moveSpeed) / 100)
            + GameData.EndSeconds;
    }
}
