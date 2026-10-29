# Combat rating regression simulation

Run from the repository root:

```powershell
dotnet run --project tools/combat_rating_checks -p:BaseOutputPath=D:/projects/van-dao/VanDao/build-check/combat-ratings/ -- D:/projects/van-dao/VanDao
```

Runs 512 deterministic battles: four difficulties, chapters 1/10, elite stage 10 and the boss, 32 seeds each. Every scenario uses the same realm-9 stage-2 character with full supreme realm-9 equipment and max fire skills, without gems or pets. The real `GameService.BattleAt` produces each result. The tool checks nonnegative HP and aligned reaction arrays; it does not change game balance or saved characters.

Results are written to `output/combat-ratings-balance.json`; console output lists wins and mean turns. Combo/counter metrics count turns with positive damage, not shielded or missed activations. This is a regression fixture, not a claim that the loadout should clear every difficulty. In particular, losing to the final Hell boss without gems/pets is a balance observation, not a failing correctness check.
