# Final realms implementation plan

> Execute inline using superpowers:executing-plans. User authorized autonomous implementation and design choices.

**Goal:** Finish the existing Độ Kiếp and Chân Tiên implementation without changing progression balance.

**Architecture:** Retain the data-driven realm, gear and skill definitions. Supply dedicated production icon assets at their existing paths and exercise real GameService operations using a standalone checks project, following tools/herb_checks.

**Tech stack:** .NET 8, JavaScript, PNG assets.

## Design decisions

- Dedicated illustrated icons: violet/silver tribulation gear and ivory/jade/gold primordial gear; skill icons distinguish all seven elements and active/passive roles.
- Preserve all current costs, probabilities and realm requirements unless a test demonstrates an integration defect.
- Keep existing hero sprites and VFX families; no new game systems or database migrations.
- Work in the existing feature checkout, claude/do-kiep-chan-tien. Leave changes reviewable without publishing.

## Tasks

- [x] Add tools/final_realm_checks with real service tests for progression, missing-material rejection, highest realm, brewing, break pills, equipment, skills, stats and image availability. Run before implementation to demonstrate missing assets or behavioral defects.
- [x] Generate gear/skill art, extract atlas cells to the 48 registered PNG paths, inspect atlas and sample icons. Keep source prompts and deterministic extraction utility.
- [x] Fix integration defects supported by failed checks, with regression checks first.
- [x] Run final realm checks, existing herb/market/agility checks, JavaScript checks and main build; obtain a fresh code review and resolve actionable findings.

## Review focus

- A failed breakthrough must preserve boss cores and pill bonuses.
- Chân Tiên Hậu kỳ must reject further advancement without consuming items.
- Gear variants must retain correct realm, item level and real image paths.
- Every element must learn and equip the new active skills while passives affect stats.
- High realm battles must not produce negative HP, damage or recovery from integer overflow.

## Execution ledger

- Initial inspection: 20 gear and 28 skill PNGs missing; build succeeds with five existing warnings.
- User chose autonomous execution; no further design approval required.
- Red: 50/463 checks failed: 48 missing icons and two missing brew realm restrictions. Initial harness mistakes (base gear registration and relative root) were corrected before interpreting failures.
- Green: 515 server checks including full equipment and max-passive PvE/PvP; 62 client asset/mapping/VFX checks.
- Built-in imagegen produced gear/skill atlases. Inspected sources and sample extracted icons. Sources/prompts and deterministic cell extraction are preserved under tools/final_realm_art.
- Ruling: add MinRealm 7/8 to new Phá Chướng Đan because Brew uses item MinRealm and these are realm-specific recipes. Other costs and progression balance are unchanged.
- Regression results: herbs 336, market 403, server agility 15, client agility 280, number formatting 15; wiki navigation/recipes/480 areas/304 image paths pass.
- Ruling: wiki check must expect vi-VN formatting now gear count exceeds 1,000. Only test expectation changed; user-visible formatting was already correct.
- Independent read-only reviewer found no actionable defects; independently reran 62 client checks and verified wiki gear copies match runtime assets.
- Default build output locked by an already-running game. Verification build uses build-check/final-realms rather than stopping that process.
- Concurrent changes appeared in existing Hỏa icon assets and wwwroot/js/skills.js after review; these were not made by this task and are preserved.
- Final verification build: `dotnet build --no-restore -o build-check/final-realms -v quiet` succeeds, 0 errors, 1 WindowsBase warning. Final client rerun: 62 pass; git diff --check clean. Changes left in feature checkout for review; no commit, push, merge or release performed. Live desktop animation/layout was not exercised.
