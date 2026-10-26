# Địa Ngục 1 checks

Run from the repository root:

```powershell
dotnet run --project tools/hell_map_checks -- D:/projects/van-dao/VanDao
dotnet run --project tools/hell_map_checks -- D:/projects/van-dao/VanDao --balance
node tools/hell_map_checks/client.cjs
```

Uses an isolated in-memory SQLite context and synthetic players; no save files or network services are touched. If the game is running from `bin/Debug`, build the app with `-o bin/hell-check` to avoid the locked DLL.

Coverage: Hell locked until Nightmare boss 10, per-difficulty stage counts (stage 16 normal, boss 31), key requirement, chapter-by-chapter unlock, reload of saves at stages 16/30/31, old difficulties keep boss 16, leaderboard progress, the four monster skills (armor capacity and expiry, single-hit chains, burn after three hits without stacking, announced and interruptible eruption), skill events tied to real replay turns, deterministic online/offline replay, one chest per boss win, chest identity and normalized odds, per-tier skill limits, stats below `int.MaxValue`, and all four image variants for every monster.

## Balance snapshot (2026-10-26)

`--balance`, wood manual, full active and passive skills at max level, gear set of the realm at three qualities. Format `chapter-stage:W/L/turns`, chapters 1, 5, 10.

| Fixture | Ch.1 | Ch.5 | Ch.10 |
|---|---|---|---|
| Độ Kiếp q4 | wins 1-1 only | loses | loses |
| Độ Kiếp q7 | wins up to 1-30, loses boss | wins 5-1 | wins 10-1 |
| Độ Kiếp q9 | clears incl. boss | clears incl. boss | wins 10-1, loses from 10-10 |
| Chân Tiên q4–q9 | clears | clears | clears |

Opening chapters are a reachable step for invested Độ Kiếp players; the late chapters require Chân Tiên. Chân Tiên HP already reaches `int.MaxValue` at q4, so the final chapters do not yet separate optimized Chân Tiên Hậu kỳ builds from ordinary ones; this needs tuning once Chân Tiên stats are rebalanced.
