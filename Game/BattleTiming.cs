namespace VanDao.Game;

public static class BattleTiming
{
    public const double ExtraAttackSeconds = .34, PetCastSeconds = .8;
    // Mọi lượt tuyệt kỹ (đầy nộ): 2 giây hoạt ảnh + nhịp phản đòn; tính cả khi trượt.
    // cân bằng 11/2026: 3s → 2s (sheet 90 khung dựng 3s phát nhanh 1,5× phía client). Khớp ULTIMATE_CAST_SECONDS trong fire-fx.js.
    public const double UltimateTurnSeconds = 2 + .25;

    public static double CombatSeconds(BattleDto battle, double moveSpeed = 0)
    {
        var petTurns = battle.Pet?.Casts.Where(c => c.At < .85 && c.Turn >= 0 && c.Turn < battle.Hits)
            .Select(c => c.Turn).Distinct().Count() ?? 0;
        var ultimateTurns = battle.Casts.Where(c => c.Ultimate && c.Turn >= 0 && c.Turn < battle.Hits).Select(c => c.Turn).Distinct().Count();
        return battle.Hits * GameData.HitSeconds
            + ultimateTurns * (UltimateTurnSeconds - GameData.HitSeconds)
            + (battle.Extra?.Take(battle.Hits).Count(extra => extra) ?? 0) * ExtraAttackSeconds
            + petTurns * PetCastSeconds
            + Math.Max(0, (battle.Waves?.Count ?? 1) - 1) * GameData.WaveSeconds / (1 + Math.Max(0, moveSpeed) / 100)
            + GameData.EndSeconds;
    }
}
