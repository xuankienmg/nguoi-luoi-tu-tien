# Địa Ngục 1 checks

Run from the repository root:

```powershell
dotnet run --project tools/hell_map_checks -- D:/projects/van-dao/VanDao
dotnet run --project tools/hell_map_checks -- D:/projects/van-dao/VanDao --balance
node tools/hell_map_checks/client.cjs
```

Uses an isolated in-memory SQLite context and synthetic players; no save files or network services are touched. If the game is running from `bin/Debug`, build the app with `-o bin/hell-check` to avoid the locked DLL.

Coverage: Hell locked until Nightmare boss 10, per-difficulty stage counts (stage 16 normal, boss 31), key requirement, chapter-by-chapter unlock, reload of saves at stages 16/30/31, old difficulties keep boss 16, leaderboard progress, the four monster skills (armor capacity and expiry, single-hit chains, burn after three hits without stacking, announced and interruptible eruption), skill events tied to real replay turns, deterministic online/offline replay, one chest per boss win, chest identity and normalized odds, per-tier skill limits, stats below `CombatNumber.Max` (2^53 − 1), the chapter 4 and 10 Chân Tiên gates, all four image variants for every monster, every one of the twelve new `img/hell` monsters used in a roster with correct variant sizes, six chapters using the new boss art, and themed skills for the new species.

## Late Nightmare → early Hell (2026-10-26)

```powershell
dotnet run --project tools/hell_map_checks -- D:/projects/van-dao/VanDao --curve      # strongest wave per stage, ÁM 5 → ĐN 10
dotnet run --project tools/hell_map_checks -- D:/projects/van-dao/VanDao --frontier   # first stage each hero cannot reliably clear
dotnet run --project tools/hell_map_checks -- D:/projects/van-dao/VanDao --tune       # largest HellMapData.StatTune each Chân Tiên row clears, ĐN 4–10
dotnet run --project tools/hell_map_checks -- D:/projects/van-dao/VanDao --debug      # turn-by-turn battles; --debug 9,0,5,3,4,31 = realm,stage,quality,diff,chapter,area
```

Before 64-bit combat (next section) the skill crit was clamped to `int.MaxValue`, which was also the boss HP ceiling, so the turn-3 cast kills anything; fights last 2–4 turns and a hero wins when it survives until the cast. Monster HP barely matters, attack decides. Above a hero's two-hit threshold wins fall to dodge luck (~60%) rather than zero, so "clears" means 21 of 24 battles (three player ids × eight times). This section tuned attack only for that reason.

`GameData.NightmareAtkTune` sets Nightmare boss attack evenly at 600M → 700M → 800M → 900M → 1000M for chapters 6–10 (previously 49M → 208M → 658M → 934M → 1326M); normal stages scale with it. Hell continues from there: its boss is always 5% above its stage-30 elite (1055M → 1108M → 1163M for chapters 1–3; previously weaker than that elite and than Nightmare boss 10), and stage 1 opens below Nightmare boss 10. The checks lock this shape: no boss easier than the one before, no late-Nightmare chapter above ×1.5.

Frontier (first stage not cleared reliably), full skills, realm gear at quality q:

| Hero | Before | After |
|---|---|---|
| Đại Thừa Hậu kỳ q5–q9 | ÁM boss 6–7 | inside ÁM 6 (stage 10–16) |
| Độ Kiếp q5 | ÁM boss 9 | ÁM boss 6–7 |
| Độ Kiếp q7 | ÁM boss 10 / ĐN 1 | ÁM boss 8 (Sơ kỳ) · ĐN boss 1 (Hậu kỳ) |
| Độ Kiếp q8 | ÁM boss 10 | ÁM boss 9 · ĐN boss 1 |
| Độ Kiếp q9 | ĐN 3–7 | past ĐN 3 |
| Chân Tiên | clears everything | clears everything (needs 64-bit combat) |

Neighbouring rows still swap now and then (dodge luck follows the player id).

## Nightmare HP floor (2026-10-28)

`NightmareAtkTune` and the late combat factor raise attack only, so Nightmare 6 monsters had a fifth of their attack as HP (6-1: hp 12M, atk 62M; elite 6-15: 103M / 352M). `GameData.NightmareHpFloor` now keeps Nightmare HP at no less than 1.2 × attack, and boss HP at no less than its attack.

The extra HP alone pushed Đại Thừa Hậu kỳ back to 6-3 … 6-10 and Độ Kiếp q5 to boss 6, so chapter 6 attack comes down to compensate: `NightmareAtkTune[5]` 12.2 → 10 (boss) and `NightmareMobAtkTune[5]` = 0.5 for normal and elite waves (net 5). A single chapter tune near 6 would also restore the frontier, but would drop boss 6 to ~300M and break the ×1.5 boss-growth check into chapter 7. Chapters 7–10 keep their attack; only their normal and elite HP rises.

| Stage | Before hp / atk | After hp / atk |
|---|---|---|
| ÁM 6-1 | 12M / 62M | 31M / 26M |
| ÁM 6-15 elite | 103M / 352M | 173M / 144M |
| ÁM 6-16 boss | 507M / 597M | 507M / 489M |
| ÁM 7-1 | 17M / 73M | 88M / 73M |
| ÁM 10-1 | 48M / 105M | 126M / 105M |
| ÁM 10-16 boss | 2062M / 1000M | unchanged |

Boss attack runs 489M → 702M → 800M → 900M → 1000M (chapter 7 / 6 = 1.44). `--frontier` matches the one before this change for Đại Thừa Hậu kỳ (q5 / q7 / q8 / q9 at 6-10 / 6-13 / boss / boss, was 6-10 / 6-14 / boss / boss) and for every Độ Kiếp row in Nightmare (q5 boss 7, q7 boss 8, q8 boss 9).

## 64-bit combat and Chân Tiên chapters 4–10 (2026-10-26)

All combat quantities are `long` now: `CombatStats`, `MonsterDef`, every HP/damage/heal/shield field of `BattleDto`, `WaveDto`, `CastDto`, `PetCastDto`, `AreaDto`/`WaveInfoDto`, `BreakPreviewDto`, `MonsterSkillEventDto.Value`, PvP (`PvpFighter`, `PvpAction`, `PvpBattleDto`), and the helpers in `PetCombat`, `DivineCombat`, `CombatBurn`, `HellMonsterCombat`. `CombatNumber` saturates at 2^53 − 1 instead of `int.MaxValue`, so the JS client still receives exact integers (its formatter already prints T). Rescaling late stats was rejected: every realm's displayed numbers, saves and leaderboard history would shift for a problem that only exists above 2.1B.

- Regression: a scratch dump of `BattleAt`, `PreviewMap`, `AreaWaves` and PvP for 80 heroes (every realm, Sơ/Hậu kỳ, q4/q9, with and without pet) × Thường/Khó/Ác Mộng × all chapters × 16 stages × 2 trip times (76,800 battles) is byte-identical before and after wherever no value had reached `int.MaxValue`. The 7,758 lines that had (all Chân Tiên heroes, whose HP was pinned there, and Độ Kiếp skill crits in late Ác Mộng) change only in the unclamped numbers, with no win/loss flips.
- Saves: `Player.BodyHp`/`BodyDef` became `long`; SQLite `INTEGER` is already 64-bit, so old `vandao.db` files load unchanged. Leaderboard power was already `long` and its stats are `double`. Arena: `PvpFighter` loadouts now carry HP above 2^31 for Chân Tiên; a not-yet-updated client fails to parse such a loadout (caught) and falls back to stats, where `(int)` of an HP above 2^31 breaks. It only affects old builds fighting a Chân Tiên, and the launcher auto-updates.

Uncapped, Chân Tiên Sơ kỳ q5 (atk 221M, hp 3.7B) one-shot every Hell 4 wave. `HellMapData.StatTune` multiplies HP, attack and defense of chapters 4–10 by 2.30 · 2.62 · 2.99 · 3.40 · 3.89 · 4.40 · 4.50 (chapters 1–3 stay 1), fitted with `--tune`, which bisects the largest multiplier each row still clears per chapter. Boss HP now runs 2.6B (ch.4) → 8.1B (ch.10), attack 2.8B → 7.4B. The default run checks the gates: Độ Kiếp q9 loses boss 4 and Chân Tiên Sơ kỳ q5 wins it; Chân Tiên Sơ kỳ q9 loses boss 10 and Hậu kỳ q9 wins it; Chân Tiên Hậu kỳ q5 loses boss 7.

`--frontier` (first stage not cleared reliably):

| Quality | Sơ kỳ | Trung kỳ | Hậu kỳ |
|---|---|---|---|
| Độ Kiếp q9 (Sơ / Hậu) | ĐN 4-5 | — | ĐN 4-10 |
| Chân Tiên q5 | ĐN boss 5 | ĐN boss 5 | ĐN boss 6 |
| Chân Tiên q6 | ĐN boss 7 | ĐN boss 7 | ĐN boss 7 |
| Chân Tiên q7 | ĐN boss 8 | ĐN boss 6 | ĐN boss 8 |
| Chân Tiên q8 | ĐN boss 7 | ĐN boss 9 | ĐN boss 7 |
| Chân Tiên q9 | ĐN boss 10 | clears | clears |

Findings: gear quality dominates (q5 → q9 is about ×2.5 in clearable stats) while Sơ → Hậu kỳ adds only 5–10%, so "ch.10 needs Hậu kỳ q9" lands as "needs q9 past Sơ kỳ"; Trung kỳ q9 also clears. q8 sets are no stronger than q7 for this wood build (two of three q8 rows stop earlier than q7); that is a gear-data question, not tuned here. Each chapter's boss is its wall: every row clears stage 30 of the chapter it stops in.

## Balance snapshot (2026-10-26, before the late-Nightmare retune)

`--balance`, wood manual, full active and passive skills at max level, gear set of the realm at three qualities. Format `chapter-stage:W/L/turns`, chapters 1, 5, 10.

| Fixture | Ch.1 | Ch.5 | Ch.10 |
|---|---|---|---|
| Độ Kiếp q4 | wins 1-1 only | loses | loses |
| Độ Kiếp q7 | wins up to 1-30, loses boss | wins 5-1 | wins 10-1 |
| Độ Kiếp q9 | clears incl. boss | clears incl. boss | wins 10-1, loses from 10-10 |
| Chân Tiên q4–q9 | clears | clears | clears |

Opening chapters are a reachable step for invested Độ Kiếp players; the late chapters require Chân Tiên. Chân Tiên HP already reaches `int.MaxValue` at q4, so the final chapters do not yet separate optimized Chân Tiên Hậu kỳ builds from ordinary ones; this needs tuning once Chân Tiên stats are rebalanced.
