# Replace agility with dodge and accuracy

Approved rule: convert all legacy agility bonuses to dodge; accuracy remains separate. Agility no longer changes travel or combat duration. Keep legacy storage and item IDs compatible. Dedicated gem movement/attack-speed effects remain unchanged.

- [x] Add explicit combat dodge/accuracy fields, combine all passive and equipped skill sources once, remove old agility cap and timing scaling.
- [x] Update PvE/PvP, leaderboard statistics and initiative (attacker acts first since agility no longer exists).
- [x] Update character/item/skill/pet/set descriptions and sources; preserve movement skill categories.
- [x] Verify migration-compatible data, timing, stat separation and combat tests; review changes.

Movement clarification: dedicated movement speed reduces walk and wave-entry duration only. Dodge and Accuracy never scale animation time. Arena snapshot review fix: synchronize authoritative accuracy as well as dodge. Validation: 108 backend tests, 11 JavaScript tests and 280 phase scenarios passed. Character stat UI inspected in an isolated QA database; no real save used.
