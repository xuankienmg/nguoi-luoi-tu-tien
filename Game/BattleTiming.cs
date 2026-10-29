namespace VanDao.Game;

public static class BattleTiming
{
    public const double ExtraAttackSeconds = .34, PetCastSeconds = .8;

    public static double CombatSeconds(BattleDto battle, double moveSpeed = 0)
    {
        var petTurns = battle.Pet?.Casts.Where(c => c.At < .85 && c.Turn >= 0 && c.Turn < battle.Hits)
            .Select(c => c.Turn).Distinct().Count() ?? 0;
        return battle.Hits * GameData.HitSeconds
            + (battle.Extra?.Take(battle.Hits).Count(extra => extra) ?? 0) * ExtraAttackSeconds
            + petTurns * PetCastSeconds
            + Math.Max(0, (battle.Waves?.Count ?? 1) - 1) * GameData.WaveSeconds / (1 + Math.Max(0, moveSpeed) / 100)
            + GameData.EndSeconds;
    }
}
