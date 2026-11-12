namespace VanDao.Game;

/// <summary>Per-battle secondary state. Enemy effects replace each other; hero buffs are independent.</summary>
public sealed class SkillEffectCombat(string? activeManual = null, IReadOnlyDictionary<string, double>? passiveBonus = null)
{
    private string _control = "";
    private int _controlTurn = -1, _shatterTurn = -1;
    private int _weakenFrom = -1, _weakenUntil = -1, _windFrom = -1, _windUntil = -1;
    private double _weakenValue, _windValue;
    public string ControlKind(int turn) => turn == _controlTurn ? _control : "";
    public bool Shatter(int turn) => turn >= 0 && turn == _shatterTurn;
    public double AttackDown(int turn) => _weakenFrom >= 0 && turn >= _weakenFrom && turn <= _weakenUntil ? _weakenValue : 0;
    public double DodgeBonus(int turn) => _windFrom >= 0 && turn >= _windFrom && turn <= _windUntil ? _windValue : 0;
    /// <summary>Phong hộ cộng cùng lượng chính xác như né tránh (11/2026).</summary>
    public double AccuracyBonus(int turn) => DodgeBonus(turn);
    public void ClearEnemy() { _control = ""; _controlTurn = _shatterTurn = _weakenFrom = _weakenUntil = -1; }

    public SkillSecondaryDef? TryApply(SkillDef skill, int level, int turn, Random random, bool targetAlive) =>
        Apply(skill.SecondaryEffect, SkillSecondaryEffects.Chance(skill, level, activeManual, passiveBonus), turn, random, targetAlive);

    /// <summary>Đòn thường chính: hiệu ứng bất lợi cùng hệ, tỉ lệ <see cref="SkillSecondaryEffects.BasicChance"/>.</summary>
    public SkillSecondaryDef? TryApplyBasic(SkillDef basic, IReadOnlyDictionary<string, int> levels, int turn, Random random, bool targetAlive) =>
        Apply(SkillSecondaryEffects.ForBasic(basic), activeManual is null ? 0 : SkillSecondaryEffects.BasicChance(basic, levels, activeManual, passiveBonus), turn, random, targetAlive);

    private SkillSecondaryDef? Apply(SkillSecondaryDef? effect, double chance, int turn, Random random, bool targetAlive)
    {
        if (effect is null || chance <= 0 || turn < 0 || effect.Target == "monster" && !targetAlive
            || random.NextDouble() >= chance / 100.0) return null;
        if (effect.Target == "monster") ClearEnemy();
        switch (effect.Kind)
        {
            case "stun": case "paralyze": case "freeze":
                _control = effect.Kind; _controlTurn = turn;
                if (effect.Kind == "freeze") _shatterTurn = turn + 1;
                break;
            case "weaken": _weakenFrom = turn; _weakenUntil = turn + effect.Duration - 1; _weakenValue = effect.Value; break;
            case "wind": _windFrom = turn; _windUntil = turn + effect.Duration - 1; _windValue = effect.Value; break;
        }
        return effect;
    }
}
