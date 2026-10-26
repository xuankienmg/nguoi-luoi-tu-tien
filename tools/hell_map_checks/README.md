# Địa Ngục 1 checks

Run from the repository root:

```powershell
dotnet run --project tools/hell_map_checks -- D:/projects/van-dao/VanDao
dotnet run --project tools/hell_map_checks -- D:/projects/van-dao/VanDao --balance
node tools/hell_map_checks/client.cjs
```

Uses an isolated in-memory SQLite context and synthetic players; no save files or network services are touched. If the game is running from `bin/Debug`, build the app with `-o bin/hell-check` to avoid the locked DLL.

Coverage: Hell locked until Nightmare boss 10, per-difficulty stage counts (stage 16 normal, boss 31), key requirement, chapter-by-chapter unlock, reload of saves at stages 16/30/31, old difficulties keep boss 16, leaderboard progress, the four monster skills (armor capacity and expiry, single-hit chains, burn after three hits without stacking, announced and interruptible eruption), skill events tied to real replay turns, deterministic online/offline replay, one chest per boss win, chest identity and normalized odds, per-tier skill limits, stats below `int.MaxValue`, all four image variants for every monster, every one of the twelve new `img/hell` monsters used in a roster with correct variant sizes, six chapters using the new boss art, and themed skills for the new species.

## Late Nightmare → early Hell (2026-10-26)

```powershell
dotnet run --project tools/hell_map_checks -- D:/projects/van-dao/VanDao --curve      # strongest wave per stage, ÁM 5 → ĐN 3
dotnet run --project tools/hell_map_checks -- D:/projects/van-dao/VanDao --frontier   # first stage each hero cannot reliably clear
dotnet run --project tools/hell_map_checks -- D:/projects/van-dao/VanDao --debug      # turn-by-turn battles
```

At this tier the skill crit is clamped to `int.MaxValue`, which is also the boss HP ceiling, so the turn-3 cast kills anything; fights last 2–4 turns and a hero wins when it survives until the cast. Monster HP barely matters, attack decides. Above a hero's two-hit threshold wins fall to dodge luck (~60%) rather than zero, so "clears" means 21 of 24 battles (three player ids × eight times). The real fix is 64-bit combat numbers; until then only attack is tuned.

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

## Balance snapshot (2026-10-26, before the late-Nightmare retune)

`--balance`, wood manual, full active and passive skills at max level, gear set of the realm at three qualities. Format `chapter-stage:W/L/turns`, chapters 1, 5, 10.

| Fixture | Ch.1 | Ch.5 | Ch.10 |
|---|---|---|---|
| Độ Kiếp q4 | wins 1-1 only | loses | loses |
| Độ Kiếp q7 | wins up to 1-30, loses boss | wins 5-1 | wins 10-1 |
| Độ Kiếp q9 | clears incl. boss | clears incl. boss | wins 10-1, loses from 10-10 |
| Chân Tiên q4–q9 | clears | clears | clears |

Opening chapters are a reachable step for invested Độ Kiếp players; the late chapters require Chân Tiên. Chân Tiên HP already reaches `int.MaxValue` at q4, so the final chapters do not yet separate optimized Chân Tiên Hậu kỳ builds from ordinary ones; this needs tuning once Chân Tiên stats are rebalanced.
