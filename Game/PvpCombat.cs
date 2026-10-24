namespace VanDao.Game;

/// <summary>Kỹ năng trong ô của một bên PK (id + cấp).</summary>
public sealed record PvpSkill(string Id, int Lv);

/// <summary>
/// Ảnh chụp mọi thứ một tu sĩ mang vào Lôi đài (GameService.PvpFighterOf): chỉ số, đòn thường + tuyệt kỹ trong ô,
/// bị động công pháp, hiệu ứng trang bị theo cấp, thần thông bộ đồ, ảnh nhân vật.
/// Gửi lên Supabase (arena_ranks.loadout) để người khác đánh với mình. Linh thú chưa tham chiến (chỉ số cộng từ linh thú vẫn tính).
/// </summary>
public sealed record PvpFighter(
    string Name, int Realm, int Stage, string Hero, string HeroStand,
    int Atk, int Hp, int Def, double Crit, double Speed, double CritMult,
    string Basic, double BasicPct, double BasicCrit, List<PvpSkill> Skills, string Manual,
    double SkillPct, double Pen, bool PassiveBurn,
    double Shield, double Dodge, double AtkDown,
    double GuardPct, double FirstHitPct, double Lightning, double LightningPct,
    double Regen, double Reflect, double ExtraHit, double Rage, double WoodHeal, double WoodEmergency, int Divine, int V = 1)
{
    /// <summary>Đối thủ bản game cũ chưa gửi ảnh chụp: dựng từ chỉ số trên bảng xếp hạng, chỉ có đòn thường Hỏa Cầu.</summary>
    public static PvpFighter FromStats(string name, int realm, Dictionary<string, double>? st)
    {
        double S(string k) => st?.GetValueOrDefault(k) ?? 0;
        var r = GameData.Realms[Math.Clamp(realm, 0, GameData.Realms.Count - 1)];
        return new(name, realm, 0, r.Hero, r.HeroStand, (int)Math.Max(1, S("atk")), (int)Math.Max(1, S("hp")), (int)S("def"), S("crit"), S("speed"), .5,
            "hoa_cau", SkillData.ById.TryGetValue("hoa_cau", out var b) ? b.DmgBase : 100, 0, [], "hoa",
            0, 0, false, 0, CombatDodge.Chance(S("speed"), 0, 0), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    /// <summary>
    /// Đối thủ chưa gửi bộ kỹ năng (bản game chưa có Lôi đài): mang bộ kỹ năng, bị động, hiệu ứng trang bị và thần thông giống hệt người khiêu chiến,
    /// chỉ số gốc (công, máu, thủ, bạo kích, thân pháp) theo bảng xếp hạng của họ. Thắng thua khi đó chỉ do chỉ số; hồ sơ chỉ có Hỏa Cầu thì yếu hơn thực tế rất nhiều.
    /// </summary>
    public static PvpFighter MirrorOf(PvpFighter me, string name, int realm, Dictionary<string, double>? st)
    {
        if (st is null || st.GetValueOrDefault("atk") <= 0) return FromStats(name, realm, st);
        var b = FromStats(name, realm, st);
        // né = thân pháp + phần cộng thêm của trang bị / kỹ năng: giữ phần cộng thêm của mình, đổi thân pháp sang của đối thủ
        var extraDodge = Math.Max(0, me.Dodge - CombatDodge.Chance(me.Speed, 0, 0));
        return me with { Name = b.Name, Realm = b.Realm, Stage = 0, Hero = b.Hero, HeroStand = b.HeroStand,
            Atk = b.Atk, Hp = b.Hp, Def = b.Def, Crit = b.Crit, Speed = b.Speed, Dodge = CombatDodge.Chance(b.Speed, extraDodge, 0) };
    }
}

/// <summary>Một hành động của một bên trong trận PK (theo đúng thứ tự thời gian).</summary>
public sealed class PvpAction
{
    public bool ByA;
    public string Control = "";              // bị khống nên mất lượt: stun · paralyze · freeze
    public List<CastDto> Casts = [];          // tuyệt kỹ đã tung (Turn chưa gán)
    public int Basic; public bool BasicCrit;
    public bool Extra; public int ExtraDmg; public bool ExtraCrit;
    public int Bolt, FirstHit, Burn, Reflect, Heal;
    public bool Dodged, Nullified, Shatter, Boosted, Saved;
    public int Direct;                        // tổng sát thương trực tiếp lên đối thủ (trước khiên)
    public int HpA, HpD, GuardA, GuardD, RageA, RageD;
    public bool WeakenedA, WeakenedD, WindA, WindD;
}

public sealed record PvpResult(bool Win, bool Ko, PvpFighter A, PvpFighter D, List<PvpAction> Actions, int GuardA, int GuardD);

/// <summary>
/// Trận PK đối xứng: hai bên luân phiên ra đòn (bên thân pháp cao đi trước, bằng nhau: bên khiêu chiến).
/// Mỗi lượt của một bên làm đúng như tu sĩ đánh quái: thiêu đốt → thiên lôi → địa chấn → tuyệt kỹ khi đủ nộ / đòn thường → liên kích,
/// đối thủ được né, vô hiệu (thần thông), khiên, giảm sát thương, phản đòn; hiệu ứng phụ của tuyệt kỹ (khống, suy yếu, phong hộ, hồi, đốt) áp lên đối thủ.
/// Mọi số ngẫu nhiên lấy từ một Random(seed) theo thứ tự cố định: cùng seed + cùng hai ảnh chụp ra cùng một trận.
/// </summary>
public static class PvpCombat
{
    public const int MaxRounds = 50;
    /// <summary>Máu nhân lên trong Lôi đài (cả hai bên): một đòn thường ở cùng cảnh giới gây ~1/3 máu, không nhân thì trận hết trước khi đủ nộ tung tuyệt kỹ.</summary>
    public const int HpMult = 3;
    /// <summary>Sát khí: từ hiệp RageRound (0-based), sát thương trực tiếp hai bên +RagePerRound mỗi hiệp, để trận kết thúc bằng hạ gục
    /// thay vì kéo tới MaxRounds rồi so % máu (bên hồi máu / giảm sát thương nhiều sẽ thắng dù yếu hơn hẳn).</summary>
    public const int RageRound = 15;
    public const double RagePerRound = .15;

    private sealed class Side(PvpFighter f)
    {
        public readonly PvpFighter F = f;
        public int Hp = Math.Max(1, f.Hp), Guard = (int)Math.Round(Math.Max(1, f.Hp) * f.GuardPct / 100), Rage = (int)Math.Min(SkillData.RageMax, f.Rage), Acts;
        public string Control = "";
        public bool ShatterNext, WoodRecovered;
        public int WeakenLeft, WindLeft; public double WeakenValue, WindValue;
        public readonly CombatBurn Burn = new();
        public readonly DivineCombat Divine = new(f.Divine);
        public double Power => F.Atk * (1 + Math.Min(100, F.Crit) / 100 * F.CritMult);
        public List<(SkillDef s, int lv)> Ults = f.Skills
            .Where(k => SkillData.ById.TryGetValue(k.Id, out var s) && SkillData.Damage(s, k.Lv) > 0)
            .Select(k => (SkillData.ById[k.Id], k.Lv)).ToList();
    }

    public static PvpResult Run(PvpFighter a, PvpFighter d, int seed)
    {
        var rng = new Random(seed);
        a = a with { Hp = Math.Max(1, a.Hp) * HpMult }; d = d with { Hp = Math.Max(1, d.Hp) * HpMult };
        var A = new Side(a); var D = new Side(d);
        var (guardA, guardD) = (A.Guard, D.Guard);
        var first = d.Speed > a.Speed ? D : A; var second = first == A ? D : A;
        var actions = new List<PvpAction>();
        var ko = false;
        for (var r = 0; r < MaxRounds && !ko; r++)
        {
            A.Divine.BeginTurn(); D.Divine.BeginTurn();
            foreach (var (x, y) in new[] { (first, second), (second, first) })
            {
                var act = Act(x, y, rng, r);
                act.ByA = x == A;
                act.HpA = Math.Max(0, A.Hp); act.HpD = Math.Max(0, D.Hp); act.GuardA = A.Guard; act.GuardD = D.Guard;
                act.RageA = A.Rage; act.RageD = D.Rage;
                act.WeakenedA = A.WeakenLeft > 0; act.WeakenedD = D.WeakenLeft > 0; act.WindA = A.WindLeft > 0; act.WindD = D.WindLeft > 0;
                actions.Add(act);
                if (A.Hp <= 0 || D.Hp <= 0) { ko = true; break; }
            }
        }
        // hết hiệp: bên còn nhiều % máu hơn thắng, bằng nhau thì bên thủ giữ hạng
        var win = D.Hp <= 0 || A.Hp > 0 && (double)A.Hp / Math.Max(1, a.Hp) > (double)D.Hp / Math.Max(1, d.Hp);
        return new PvpResult(win, ko, a, d, actions, guardA, guardD);
    }

    /// <summary>
    /// Gốc tính thiêu đốt trong Lôi đài (CombatBurn đốt 5% gốc này mỗi lượt): máu gốc của đối thủ (chưa nhân HpMult),
    /// nhưng không quá 10 lần công người đốt, tức mỗi lượt tối đa 50% công. Đánh quái đốt 5% máu quái là đủ, còn PK mà đốt theo máu
    /// thì đối thủ càng trâu càng mất nhiều (bỏ qua cả phòng thủ): người yếu hơn nhiều vẫn thắng nhờ đốt.
    /// </summary>
    private static int BurnBase(Side burner, Side target) => (int)Math.Min(target.F.Hp / HpMult, burner.F.Atk * 10L);

    private static PvpAction Act(Side x, Side y, Random rng, int round)
    {
        var act = new PvpAction();
        var first = x.Acts == 0; x.Acts++;
        if (x.Control.Length > 0)                                             // bị choáng / tê liệt / đóng băng: mất lượt này
        {
            act.Control = x.Control; x.Control = "";
            if (x.WeakenLeft > 0) x.WeakenLeft--;
            return act;
        }
        var f = x.F;
        // thiêu đốt (bị động) đốt đối thủ từ lượt thứ 2 của mình
        var burnTrig = f.PassiveBurn && !first && rng.Next(100) < SkillData.BurnChance;
        act.Burn = y.Burn.Tick(BurnBase(x, y), burnTrig);
        // né / vô hiệu: tính một lần cho cả lượt ra đòn
        var dodge = Math.Min(CombatDodge.MaxChance, y.F.Dodge + (y.WindLeft > 0 ? y.WindValue : 0));
        act.Dodged = CombatDodge.Roll(rng, dodge);
        act.Nullified = !act.Dodged && y.Divine.Nullify(rng);
        if (act.Dodged || act.Nullified) y.Divine.Avoided();
        var avoided = act.Dodged || act.Nullified;
        var weaken = x.WeakenLeft > 0 ? x.WeakenValue : 0;
        int Hit(double raw, bool crit, bool boost = true)
        {
            var v = raw * (crit ? 1 + f.CritMult : 1);
            var def = y.F.Def * (1 - Math.Min(80, f.Pen) / 100);                   // phá giáp bỏ qua một phần thủ
            v = v * v / (v + Math.Max(0, def));                                    // công² / (công + thủ): cùng cấp thì cân, chênh lệch lớn thì bên mạnh áp đảo
            v *= (1 - Math.Min(60, y.F.Shield) / 100) * (1 - weaken / 100) * (1 - Math.Min(40, y.F.AtkDown) / 100);
            var dmg = Math.Max(1, (int)Math.Round(v));
            if (!boost) return dmg;
            var amp = x.Divine.Amplify(dmg); act.Boosted |= amp != dmg; return amp;
        }
        if (f.Lightning > 0 && rng.NextDouble() * 100 < f.Lightning) act.Bolt = Math.Max(1, (int)Math.Round(x.Power * f.LightningPct / 100));
        if (first && f.FirstHitPct > 0 && !avoided) act.FirstHit = Math.Max(1, (int)Math.Round(x.Power * f.FirstHitPct / 100));
        var fired = x.Rage >= SkillData.RageMax && x.Ults.Count > 0;
        var heal = 0;
        if (fired)
        {
            foreach (var (s, lv) in x.Ults)
            {
                var crit = rng.NextDouble() < Math.Min(100, f.Crit) / 100;
                var dmg = avoided ? 0 : Hit(f.Atk * SkillData.Damage(s, lv) * (1 + f.SkillPct / 100) / 100, crit);
                var sec = "";
                if (!avoided && s.SecondaryEffect is { } e && rng.NextDouble() * 100 < SkillSecondaryEffects.Chance(s, lv, f.Manual))
                {
                    sec = e.Kind;
                    switch (e.Kind)
                    {
                        case "stun": case "paralyze": case "freeze":
                            y.Control = e.Kind; if (e.Kind == "freeze") y.ShatterNext = true; break;
                        case "weaken": y.WeakenLeft = e.Duration; y.WeakenValue = e.Value; break;
                        case "wind": x.WindLeft = e.Duration; x.WindValue = e.Value; break;
                        case "heal": heal += (int)Math.Round(f.Hp * e.Value / 100); break;
                        case "burn": act.Burn += y.Burn.Ignite(BurnBase(x, y), act.Burn > 0); break;
                    }
                }
                act.Casts.Add(new CastDto(0, s.Id, s.Name, s.Anim, dmg, crit && dmg > 0, sec));
            }
            x.Rage = 0;
        }
        else
        {
            act.BasicCrit = rng.NextDouble() < Math.Min(100, f.Crit + f.BasicCrit) / 100;
            act.Basic = avoided ? 0 : Hit(f.Atk * f.BasicPct * (1 + f.SkillPct / 100) / 100, act.BasicCrit);
            if (avoided) act.BasicCrit = false;
            x.Rage = Math.Min(SkillData.RageMax, x.Rage + SkillData.RagePerHit);
        }
        var extraChance = f.Divine == 8 ? Math.Min(50, f.ExtraHit + 20) : f.ExtraHit;
        if (!avoided && extraChance > 0 && rng.NextDouble() * 100 < extraChance)
        {
            act.Extra = true; act.ExtraCrit = rng.NextDouble() < Math.Min(100, f.Crit + f.BasicCrit) / 100;
            act.ExtraDmg = Hit(f.Atk * f.BasicPct * (1 + f.SkillPct / 100) / 100, act.ExtraCrit, false);
        }
        var direct = act.FirstHit + act.Basic + act.ExtraDmg + act.Casts.Sum(c => c.Dmg);
        if (direct > 0 && y.ShatterNext) { direct = (int)Math.Round(direct * (1 + PetData.FreezeDamageBonus)); act.Shatter = true; y.ShatterNext = false; }
        direct += act.Bolt;
        if (round >= RageRound) direct = (int)Math.Min(int.MaxValue, Math.Round(direct * (1 + (round - RageRound + 1) * RagePerRound)));   // sát khí
        act.Direct = direct;
        var hit = direct;
        if (y.Guard > 0) { var ab = Math.Min(y.Guard, hit); y.Guard -= ab; hit -= ab; }
        y.Hp = y.Divine.Receive(y.Hp, hit) - act.Burn;
        act.Saved = y.Divine.Saved;
        if (!avoided && y.Hp > 0 && y.F.Reflect > 0 && direct > 0)                 // Địa mạch: đối thủ phản lại một phần
        {
            act.Reflect = Math.Max(1, (int)Math.Round(direct * y.F.Reflect / 100));
            x.Hp = x.Divine.Receive(x.Hp, act.Reflect);
        }
        if (x.Hp > 0)                                                               // hồi khí + Mộc: hồi khi tung chiêu, cứu nguy dưới 30% máu
        {
            var recover = (int)Math.Round(f.Hp * f.Regen / 100) + heal;
            if (fired && x.Ults.Any(u => u.s.Element == "moc")) recover += (int)Math.Round(f.Hp * f.WoodHeal / 100);
            if (!x.WoodRecovered && f.WoodEmergency > 0 && x.Hp < f.Hp * .3) { recover += (int)Math.Round(f.Hp * f.WoodEmergency / 100); x.WoodRecovered = true; }
            act.Heal = Math.Max(0, Math.Min(recover, f.Hp - x.Hp)); x.Hp += act.Heal;
        }
        if (x.WeakenLeft > 0) x.WeakenLeft--;
        if (y.WindLeft > 0) y.WindLeft--;
        return act;
    }

    /// <summary>
    /// Đổi trận thành BattleDto để màn hình chính diễn như trận đánh quái: mỗi lượt hiển thị = đòn của mình (A) rồi đòn phản của đối thủ (D).
    /// Đối thủ đi trước thì lượt đầu mình đứng yên (Idle). Trận dài thì lấy mẫu đều tối đa maxTurns lượt, luôn giữ lượt cuối.
    /// </summary>
    public static (BattleDto battle, PvpBattleDto pvp) ToBattle(PvpResult r, int maxTurns = 14)
    {
        var turns = new List<(PvpAction? a, PvpAction? d)>();
        PvpAction? pendingA = null; var started = false;
        foreach (var x in r.Actions)
        {
            if (x.ByA) { if (pendingA is not null) turns.Add((pendingA, null)); pendingA = x; started = true; }
            else { turns.Add((started ? pendingA : null, x)); pendingA = null; started = true; }
        }
        if (pendingA is not null) turns.Add((pendingA, null));
        var n = turns.Count;
        var h = Math.Min(maxTurns, n);
        var idx = Enumerable.Range(0, h).Select(i => n <= h ? i : (int)Math.Round((i + 1) * (double)n / h) - 1).Distinct().ToArray();
        var a = r.A; var d = r.D;
        int HpA((PvpAction? a, PvpAction? d) t) => (t.d ?? t.a)!.HpA;
        int HpD((PvpAction? a, PvpAction? d) t) => (t.d ?? t.a)!.HpD;
        var T = idx.Select(i => turns[i]).ToArray();
        var casts = T.SelectMany((t, i) => (t.a?.Casts ?? []).Select(c => c with { Turn = i })).ToList();
        var boltDmg = T.Select(t => t.a?.Bolt ?? 0).FirstOrDefault(v => v > 0);
        var counter = T.Select(t => t.d is null || t.d.Control.Length > 0 ? 0 : t.d.Direct).ToArray();
        var battle = new BattleDto(d.Name, d.Hero, Math.Max(1, d.Hp), d.Atk, d.Def,
            HeroDmg: T.Select(t => t.a?.Basic ?? 0).DefaultIfEmpty(0).Max(), MonsterDmg: counter.DefaultIfEmpty(0).Max(), Hits: T.Length, Win: r.Win,
            MonHpLeft: T.Select(HpD).ToArray(), HeroHpLeft: T.Select(HpA).ToArray(),
            MonSkip: T.Select(t => t.d is null || t.d.Control.Length > 0).ToArray(), Casts: casts, HeroHp: Math.Max(1, a.Hp),
            Basic: a.Basic, Rage: T.Select(t => (t.d ?? t.a)!.RageA).ToArray(),
            Burn: T.Select(t => t.a?.Burn ?? 0).FirstOrDefault(v => v > 0), BurnPct: a.PassiveBurn || T.Any(t => t.a?.Burn > 0) ? CombatBurn.Percent : 0,
            Burned: T.Select(t => (t.a?.Burn ?? 0) > 0).ToArray(),
            Bolt: T.Select(t => (t.a?.Bolt ?? 0) > 0).ToArray(), BoltDmg: boltDmg,
            Extra: T.Select(t => t.a?.Extra == true).ToArray(),
            Heal: T.Select(t => (t.a?.Heal ?? 0)).ToArray(),
            Reflect: T.Select(t => t.d?.Reflect ?? 0).ToArray(),
            Guard: r.GuardA, GuardLeft: T.Select(t => (t.d ?? t.a)!.GuardA).ToArray(),
            FirstHit: T.Length > 0 ? T[0].a?.FirstHit ?? 0 : 0,
            BasicDamage: T.Select(t => t.a?.Basic ?? 0).ToArray(), BasicCritical: T.Select(t => t.a?.BasicCrit == true).ToArray(),
            ExtraDamage: T.Select(t => t.a?.ExtraDmg ?? 0).ToArray(), ExtraCritical: T.Select(t => t.a?.ExtraCrit == true).ToArray(),
            Dodged: T.Select(t => t.d is not null && t.d.Dodged).ToArray(), DodgeChance: a.Dodge,
            GuardBefore: T.Select(t => t.a?.GuardA ?? r.GuardA).ToArray(),
            BurnDamage: T.Select(t => t.a?.Burn ?? 0).ToArray(),
            Divine: new DivineBattleDto(a.Divine, a.Divine > 0 ? DivineAbility.Definitions.FirstOrDefault(x => x.Tier == a.Divine).Name ?? "" : "", 0,
                T.Select(t => new DivineTurnDto(t.d?.Nullified == true, t.d?.Saved == true, t.a?.Boosted == true, false)).ToArray()),
            MonControl: T.Select(t => t.d?.Control ?? "").ToArray(),
            Shatter: T.Select(t => t.a?.Shatter == true).ToArray(),
            Weakened: T.Select(t => (t.d ?? t.a)!.WeakenedD).ToArray(), WindGuard: T.Select(t => (t.d ?? t.a)!.WindA).ToArray(),
            CounterDamage: counter);
        // HeroHpBefore: máu của mình ngay trước đòn phản của đối thủ = máu sau lượt của mình
        battle = battle with { HeroHpBefore = T.Select((t, i) => t.a?.HpA ?? (i == 0 ? Math.Max(1, a.Hp) : HpA(T[i - 1]))).ToArray() };
        var pvp = new PvpBattleDto(d.Name, d.Realm, d.HeroStand, a.Name,
            Idle: T.Select(t => t.a is null || t.a.Control.Length > 0).ToArray(),
            IdleKind: T.Select(t => t.a?.Control ?? "").ToArray(),
            FoeCasts: T.Select(t => string.Join(" · ", t.d?.Casts.Select(c => c.Name) ?? [])).ToArray(),
            FoeCrit: T.Select(t => t.d is not null && (t.d.BasicCrit || t.d.Casts.Any(c => c.Critical))).ToArray(),
            FoeDodged: T.Select(t => t.a is not null && t.a.Control.Length == 0 && (t.a.Dodged || t.a.Nullified)).ToArray(),
            FoeHeal: T.Select(t => t.d?.Heal ?? 0).ToArray(),
            FoeBurn: T.Select(t => t.d?.Burn ?? 0).ToArray(),
            Ko: r.Ko, Turns: n);
        return (battle, pvp);
    }
}

/// <summary>Phần riêng của trận PK cho màn hình chính: đối thủ (ảnh đứng, tên chiêu), lượt mình bị khống / đứng chờ, đòn bị né.</summary>
public record PvpBattleDto(string Foe, int FoeRealm, string FoeStand, string Me, bool[] Idle, string[] IdleKind, string[] FoeCasts, bool[] FoeCrit,
    bool[] FoeDodged, int[] FoeHeal, int[] FoeBurn, bool Ko, int Turns);
