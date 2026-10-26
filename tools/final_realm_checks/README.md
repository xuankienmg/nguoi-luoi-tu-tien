# Final realm checks

Run from the repository root (the explicit root path also works from another directory):

```powershell
dotnet run --project tools/final_realm_checks -- D:/projects/van-dao/VanDao
node tools/final_realm_checks/client.cjs
node tools/final_realm_checks/meditation.cjs
node tools/final_realm_checks/realm-fx.cjs
powershell -NoProfile -ExecutionPolicy Bypass -File tools/final_realm_checks/transparency.ps1
```

Uses an isolated in-memory SQLite context and synthetic players; no save files, real accounts or network services are touched. Tests the actual GameService operations, data and local production assets.

Chest regression coverage: one actual boss win at each difficulty yields one Boss Chest; all ordinary chest distributions at all realms remain normalized and exclude Huyền Tiên; the event chest uses 40% Tiên, 30% Đạo and 30% Huyền Tiên and real batch openings produce only those grades. Pity pays out on spin 100 and repeats independently for gear/pets; completed event milestones reset only after all rewards are claimed, preserving excess points and allowing another reward cycle.

Coverage: both major breakthroughs, zero-chance failure and guaranteed success, boss core retention, material validation, all four minor-stage advances, the final realm cap, pill recipes and realm requirements, thirty-percent pill bonus cap, twenty gear families and all quality variants, seven elemental skill branches, real PvE and PvP with full gear and max-level passives, client icon mapping and PNG integrity.

The synthetic zero-chance failure uses a negative Bonus before the service clamps chance to zero. Success uses the existing `break` admin flag. Neither case depends on random luck. Real tests use the real service without mock production behavior.
