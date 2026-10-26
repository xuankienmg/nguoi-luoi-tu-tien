namespace VanDao.Game;

/// <summary>Independent repeating cycles: 100 ordinary spins, then one guaranteed spin.</summary>
public static class SpinPity
{
    public const int Threshold = 100;
    public static int Roll(ref int progress, bool pet, Random? random = null)
    {
        random ??= Random.Shared;
        progress = Math.Clamp(progress, 0, Threshold);
        var guaranteed = progress == Threshold;
        progress = guaranteed ? 0 : progress + 1;
        if (guaranteed)
        {
            if (pet) return PetData.EggIds.Length - 1;
            var roll = random.NextDouble();
            return GameData.SpinWeights.Length - 3 + (roll < .4 ? 0 : roll < .7 ? 1 : 2);
        }
        var weights = pet ? PetData.PetSpinWeights : GameData.SpinWeights;
        var pick = random.NextDouble() * weights.Sum();
        for (var i = 0; i < weights.Length; i++) if ((pick -= weights[i]) < 0) return i;
        return weights.Length - 1;
    }
}
