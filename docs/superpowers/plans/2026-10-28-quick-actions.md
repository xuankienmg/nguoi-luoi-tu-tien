# Quick actions implementation plan

**Goal:** Add quick fusion for gear and gems and quick sale by item group, as approved in chat.

**Architecture:** Keep the existing nine-item fusion rules. Snapshot eligible gear before fusion and defer all outputs until consumption is complete. Reuse the gem quick-fusion endpoint. Keep gear rarity sale options and append non-gear item groups with explicit confirmation.

**Tech stack:** C#, ASP.NET, vanilla JavaScript.

- [x] Add and run regression tests for gear fusion: no cascading, maximum rarity preserved, insufficient inputs rejected.
- [x] Add gear quick-fusion service and route, then connect both fusion buttons.
- [x] Extend quick sale pools and labels by item type, retaining live select updates and double-click confirmation.
- [x] Run .NET tests, JavaScript behavior checks and diff checks.
