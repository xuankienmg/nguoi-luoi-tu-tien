# Herb progression implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Distribute herbs by chapter at all difficulties and simplify the approved replenishing pill recipes.
**Architecture:** Keep HerbDrops as the source of truth, select by the current chapter, and retain PetLoot probability/quantity logic. Update final recipes through Brews and HerbExpansion.ApplyRecipe.
**Tech Stack:** C#/.NET 8, xUnit, existing wiki exporter.
**Spec:** ../specs/2026-11-02-herb-progression-design.md (approved in chat).

## Global constraints
Preserve unrelated working changes. Keep pill effects, stone costs, boss and chest rewards unchanged. Reuse the current checkout to preserve its active changes. Do not commit unrelated work.

## Review focus
All four difficulties, Hell stage 30, material unlock thresholds, final merged recipes, and chapter 10 transition sources must be covered.

## Tasks
1. Add regression tests in AlchemyMaterialSourceTests for chapter-specific drops across every stage/difficulty, exact final replenishing recipes, supplementary source windows, and gathering every recipe before its breakthrough milestone. Run filtered dotnet test and confirm failure.
2. Update GameData.cs and HerbExpansion.cs with approved chapter windows, all-chapter Tẩy Tủy Thảo, chapter 10 Kiến Mộc, and two replenishing recipes. Run filtered tests.
3. Update herb item descriptions, wiki explanatory text and exporter comments; adjust herb_checks assertions that require the superseded rules. Run herb_checks and regenerate wiki via wiki_export.
4. Run the complete xUnit suite, inspect scoped diffs and generated wiki output. Record results here.

## Execution ledger
- Task 1: complete. Added regression tests and observed failures with old data (8 requirement failures; a test's gear lookup was corrected to TryGetValue).
- Task 2: complete. Chapter-specific sources and approved recipes implemented; 13 alchemy tests passed, full suite initially 318/318.
- Task 3: complete. Updated item descriptions, wiki notes, herb_checks and README. herb_checks: 1203 checks passed. Wiki export initially succeeded (1254 items, 14 pets, 10 chapters).
- Task 4: reviewing. Added an exhaustive recipe progression milestone check. Latest full rebuild ran 328 tests: 327 passed, 1 unrelated MonsterRageTests failure while concurrent workspace changes were arriving.
- Ruling: reuse current checkout and leave unrelated edits intact; no branch, commit, publish, or merge requested.
- Ruling: VSTest localhost communication hangs in sandbox; escalated test run works. Use bin/herb-validation output to avoid running game file locks.
- Ruling: concurrent GemData.BossBonusDrops API now requires chapter. Adapt wiki exporter to export chapter-specific boss gem tables, preserving the concurrent API and its meaning.
- Independent read-only review found no actionable herb progression bugs. README stale descriptions corrected. StageGroups redistribution still determines effective odds as before; chest reward tables are untouched by this task.
- Final verification: 14/14 AlchemyMaterialSourceTests pass; herb_checks 1203/1203; wiki_export succeeds and generated JSON validates all 40 chapter/difficulty sources and both recipes; scoped git diff --check passes. Full suite latest: 327/328 with unrelated MonsterRageTests.KillingWavesBeforeCounterNeverChargesOrTransfersRage failing. No edits to monster rage logic.
