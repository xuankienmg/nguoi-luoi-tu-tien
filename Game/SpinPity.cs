namespace VanDao.Game;

/// <summary>Independent 100-spin cycles: the hundredth spin is guaranteed and resets progress.</summary>
public static class SpinPity
{
    public const int Threshold = 100;
    /// <param name="realm">Cảnh giới người chơi (vòng trang bị): từ GameData.SpinKimTienRealm có ô Kim Tiên.</param>
    public static int Roll(ref int progress, bool pet, Random? random = null, int realm = 0)
    {
        random ??= Random.Shared;
        progress = Math.Clamp(progress, 0, Threshold);
        var guaranteed = progress >= Threshold - 1;
        progress = guaranteed ? 0 : progress + 1;
        if (guaranteed && pet) return PetData.EggIds.Length - 1;
        var weights = pet ? PetData.PetSpinWeights : guaranteed ? GameData.SpinPityWeightsFor(realm) : GameData.SpinWeightsFor(realm);
        var pick = random.NextDouble() * weights.Sum();
        for (var i = 0; i < weights.Length; i++) if ((pick -= weights[i]) < 0) return i;
        return Array.FindLastIndex(weights, w => w > 0);
    }
}
