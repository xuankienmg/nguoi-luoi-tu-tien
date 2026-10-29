namespace VanDao.Game;

public static class ComboCombat
{
    public static double Chance(double own,double opponent) =>
        CombatRatingRules.Opposed(own, opponent);
    public static bool Roll(Random random,double own,double opponent)
    {
        return CombatRatingRules.Roll(random, own, opponent);
    }
}
