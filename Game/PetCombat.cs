namespace VanDao.Game;

/// <summary>Trạng thái kỹ năng linh thú trong một trận; hiệu ứng cùng loại chỉ làm mới thời gian.</summary>
/// <param name="pet">Chỉ số riêng của linh thú (cộng vào Linh Uy); null = 0.</param>
/// <param name="quality">Phẩm riêng của con (Phàm → Thượng Cổ); −1 = phẩm gốc của loài.</param>
public sealed class PetCombat(PetDef definition, int form, CombatStats owner, int stars = 0, Random? random = null, bool allowMiracle = true, CombatStats? pet = null, int quality = -1)
{
    private readonly int _form = Math.Clamp(form, 0, 2);
    // Linh Uy: Công linh thú + Công chủ nhân × Đồng tâm · Linh Hộ: Máu linh thú + Máu chủ nhân × Đồng tâm
    private readonly double _power = (pet?.Atk ?? 0) + owner.Atk * PetData.SkillBond[Math.Clamp(form, 0, 2)];
    private readonly double _life = (pet?.Hp ?? 0) + owner.Hp * PetData.SkillBond[Math.Clamp(form, 0, 2)];
    private readonly double _coef = PetData.SkillCoef(definition.Skill, form, quality);
    private long _burnTick;
    private readonly double _starScale = PetData.StarScale(stars);
    private double TalentValue => definition.TalentValue * _starScale;
    private int _turn, _markUntil = -1, _downUntil = -1, _dodgeUntil = -1, _reflectUntil = -1;
    private int _burnLeft, _hotLeft;
    private int _ownerTurns;
    private readonly Random _rng = random ?? new Random(0);
    private int _paralyzedUntil = -1;                 // lượt cuối quái bị khống (tê liệt / choáng / đóng băng)
    private string _controlKind = "paralyze";
    private int _shatterTurn = -1;                     // lượt quái đóng băng nhận thêm sát thương (sau lượt bị đóng băng)
    private bool _miracleUsed;
    public bool MiracleUsed => _miracleUsed;
    /// <summary>Debuff linh thú vừa gây ở lần gọi Cast / TryMiracle gần nhất: "burn" · "paralyze" · "stun" · "freeze" · rỗng.
    /// Nhóm debuff (thiêu đốt, tê liệt, choáng, đóng băng) mỗi lúc chỉ có 1 trên quái — cái mới nhất đè cái cũ.</summary>
    public string NewDebuff { get; private set; } = "";
    /// <summary>Chủ nhân vừa gây debuff mới (thiêu đốt): xóa thiêu đốt / khống chế linh thú đang để lại trên quái.</summary>
    public void ClearDebuffs() { _burnLeft = 0; _paralyzedUntil = _shatterTurn = -1; }
    /// <summary>Quái đang bị khống (mọi loại) ở lượt hiện tại: mất lượt phản công.</summary>
    public bool Paralyzed => _turn <= _paralyzedUntil;
    /// <summary>Loại khống đang có hiệu lực: "paralyze" · "stun" · "freeze", rỗng nếu không bị khống.</summary>
    public string ControlKind => Paralyzed ? _controlKind : "";
    /// <summary>Lượt này quái còn đóng băng từ lượt trước: nhận thêm FreezeDamageBonus sát thương.</summary>
    public bool Shatter(int turn) => turn == _shatterTurn;
    public bool SkillReady => _ownerTurns >= definition.SkillEvery;
    public void RecordOwnerTurn() => _ownerTurns = Math.Min(definition.SkillEvery, _ownerTurns + 1);
    public long Shield { get; private set; }
    public bool Burning => _burnLeft > 0;
    private double Tier(double a, double b, double c) => (_form == 0 ? a : _form == 1 ? b : c) * _starScale;
    private string Name => definition.Forms[_form].SkillName;
    public double AttackDown => _turn <= _downUntil ? Tier(5, 8, 12) : 0;
    public double DodgeBonus => _turn <= _dodgeUntil ? Tier(5, 8, 12) : 0;
    public double GuardBonus => definition.Skill == "turtle" && Shield > 0 ? Tier(3, 5, 7) : 0;
    public long InitialShield()
    {
        Shield = definition.Talent == "shield" ? (long)(owner.Hp * TalentValue / 100) : 0;
        return Shield;
    }
    public void Absorb(long damage) => Shield = Math.Max(0, Shield - damage);
    public PetCastDto? TryMiracle(int turn, ref long hp, long monsterHp, long defense, double takeK, bool counterPending = true)
    {
        NewDebuff="";
        if(_miracleUsed || !allowMiracle || definition.Rarity!="ancient" || definition.Miracle is null)return null;
        _turn=turn;
        if(definition.Id=="that_thai_ho_diep" && hp<=0){
            hp=Math.Max(1,(long)(owner.Hp*.25));_miracleUsed=true;
            return new(turn,0,hp,0,definition.Miracle.Name,Kind:"butterfly_rebirth",IsMiracle:true,At:counterPending?.45:.90);
        }
        if(definition.Id=="thai_co_loi_long" && hp>0 && (long)hp*100 <= (long)owner.Hp*30 && monsterHp>0){
            var damage=Math.Min(monsterHp,Hit(250,defense,takeK,30));
            ClearDebuffs();_paralyzedUntil=turn+(counterPending?0:1);_controlKind="paralyze";_miracleUsed=true;NewDebuff="paralyze";
            return new(turn,damage,0,0,definition.Miracle.Name,new long[]{damage},"ancient_thunder_miracle",1,true,counterPending?.45:.90,"paralyze");
        }
        return null;
    }
    public long Healing(long requested, long hp)
    {
        if (hp <= 0) return 0;
        var bonus = definition.Talent == "healing_all" || definition.Talent == "healing" && hp < owner.Hp * .5 ? 1 + TalentValue / 100 : 1;
        return Math.Clamp(CombatNumber.Round(requested * bonus), 0, Math.Max(0, owner.Hp - hp));
    }
    public long AmplifyOwner(long damage, bool critical, bool targetBurning)
    {
        var scale = critical && _turn <= _markUntil ? 1 + Tier(5, 8, 12) / 100 : 1;
        if (definition.Talent == "burn_damage" && targetBurning) scale *= 1 + TalentValue / 100;
        return CombatNumber.Round(damage * scale);
    }
    private long Hit(double percent, long defense, double takeK, double pen = 0) =>
        Math.Max(1, CombatNumber.Round(Math.Max(1, owner.Atk * percent / 100 - Math.Floor(defense * (1 - pen / 100))) * takeK));
    /// <summary>Sát thương tuyệt kỹ: trừ thủ một lần cho cả chiêu (sàn 5%), rồi chia đều cho số đòn.</summary>
    private long[] SkillHits(double percent, long defense, double takeK, int count = 1, double pen = 0)
    {
        var raw = _power * percent / 100;
        var total = Math.Max(1, CombatNumber.Round(Math.Max(raw * .05, raw - Math.Floor(defense * (1 - pen / 100))) * takeK));
        return Enumerable.Range(0, count).Select(i => total / count + (i < total % count ? 1 : 0)).ToArray();
    }
    private long AddShield(double amount)
    {
        var old = Shield;
        Shield = Math.Min((long)(owner.Hp * .30), Shield + (long)amount);
        return Shield - old;
    }
    public PetCastDto? BeginTurn(int turn, ref long hp, long monsterHp, long defense, double takeK)
    {
        _turn = turn;
        if (_burnLeft > 0)
        {
            _burnLeft--;
            var damage = Math.Min(monsterHp, _burnTick);
            return new(turn, damage, 0, 0, Name + " · Thiêu đốt", Kind: "burn");
        }
        if (_hotLeft > 0)
        {
            _hotLeft--;
            var heal = Healing((long)(_life * _coef * .15 / 100), hp); hp += heal;
            return new(turn, 0, heal, 0, Name + " · Hồi phục", Kind: "regen");
        }
        return null;
    }
    public PetCastDto Cast(int turn, ref long hp, long monsterHp, long monsterMaxHp, long defense, double takeK)
    {
        _ownerTurns = 0; // Only a real cast consumes the accumulated turns; wave transitions do not.
        _turn = turn; NewDebuff = "";
        long damage = 0, heal = 0, guard = 0; var paralyze = 0; var hits = new List<long>();
        void Strike(double percent, int count = 1, double pen = 0)
        {
            foreach (var raw in SkillHits(percent, defense, takeK, count, pen))
            {
                var hit = Math.Min(Math.Max(0, monsterHp - damage), raw);
                hits.Add(hit); damage += hit;
            }
        }
        double Life(double share) => _life * _coef * share / 100;   // lượng hồi / khiên theo Linh Hộ
        switch (definition.Skill)
        {
            case "fox": Strike(_coef); _markUntil = turn + 2; break;
            case "frost": guard = AddShield(Life(1)); _downUntil = turn + 1; break;
            case "deer": heal = Healing((long)Life(.7), hp); _hotLeft = 2; break;            // 70% hồi ngay, 2 × 15% hồi dần
            case "turtle": guard = AddShield(Life(1)); break;
            case "eagle": Strike(_coef, 3); break;
            case "leech":
                Strike(_coef);
                heal = Healing(Math.Min((long)(damage * Tier(20, 25, 30) / 100), (long)(owner.Hp * Tier(4, 6, 8) / 100)), hp);
                break;
            case "crane": heal = Healing((long)Life(1), hp); _dodgeUntil = turn + 1; break;
            case "cat": Strike(_coef, 1, Tier(15, 25, 35)); break;
            case "phoenix":                                                                  // 60% đòn đầu, 2 × 20% thiêu đốt
                Strike(_coef * .6); ClearDebuffs(); _burnLeft = 2; NewDebuff = "burn";   // thiêu đốt đè khống chế cũ
                _burnTick = SkillHits(_coef * .2, defense, takeK)[0];
                break;
            case "xuanwu": guard = AddShield(Life(1)); _reflectUntil = turn + 1; break;
            case "dragon": Strike(_coef, 3, _form == 2 ? 20 : 0); break;
            case "tiger": Strike(_coef * (monsterHp < monsterMaxHp * .30 ? 1.25 : 1)); break;
            case "butterfly":                                                                // 45% hồi máu, 55% khiên
                heal = Healing((long)Life(.45), hp);
                guard = AddShield(Life(.55));
                break;
            case "ancient_thunder": Strike(_coef); break;
        }
        // Khống chế theo hệ (PetData.ControlOf): chỉ gieo khi chiêu có khống chế
        var (controlKind, controlChance) = PetData.ControlOf(definition.Skill);
        var controlled = "";
        if (controlChance > 0 && monsterHp - damage > 0 && _rng.NextDouble() < controlChance)
        {
            ClearDebuffs();                                                         // khống chế mới đè debuff cũ (kể cả thiêu đốt)
            _paralyzedUntil = turn; _controlKind = controlKind; paralyze = 1; controlled = controlKind; NewDebuff = controlKind;
            if (controlKind == "freeze") _shatterTurn = turn + 1;
        }
        hp += heal;
        return new(turn, damage, heal, guard, Name, hits.Count > 0 ? hits.ToArray() : null, definition.Skill, paralyze, ControlKind: controlled);
    }
    public PetCastDto? Reflect(int turn, long monsterHp, long defense, double takeK)
    {
        if (turn > _reflectUntil || monsterHp <= 0) return null;
        return new(turn, Math.Min(monsterHp, SkillHits(PetData.ReflectCoef(_form, quality), defense, takeK)[0]), 0, 0, Name + " · Phản đòn", Kind: "reflect",At:.9);
    }
    public void NextWave() { _burnLeft = 0; _markUntil = _downUntil = _paralyzedUntil = _shatterTurn = -1; }
}
