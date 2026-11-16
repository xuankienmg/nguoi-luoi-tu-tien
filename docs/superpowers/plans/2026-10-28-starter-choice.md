# Starter selection implementation plan

New characters choose one available cultivation manual and one species-specific egg. Unlock the chosen tree and activate its basic attack; skill levels, point costs and realm requirements remain unchanged. Persist completion per character. Existing saves skip selection and keep their rewards. Reset characters select again.

Use a server-generated option list and validate it before awarding anything. Existing Hỏa Hồ eggs stay compatible. Offer one existing non-divine species per element, with egg quality matching species rarity. No premium or ancient pets in starter choices.

- [x] Add persisted completion state and backward-compatible schema default; test new creation, reload, reset, rejection and duplicate submission.
- [x] Add guaranteed species eggs and test hatching each choice.
- [x] Add choice endpoint and state DTO; pause offline progression until selection.
- [x] Add responsive, keyboard-accessible choice dialog with manual and pet cards, explicit confirmation and visible errors; reopen after refresh until completion.
- [x] Run backend and UI checks, inspect rendered dialog at wide and narrow sizes, and review the final changes.

Validation: 66 backend tests passed; 5 existing JavaScript tests passed. Browser checks passed at 1100×780, 390×700 and 620×350, covering options, disabled confirmation, Escape, submission, reload and duplicate rejection. Screenshots inspected in build-check/starter-ui/. Review found the separate admin reset path also needed StarterChosen=false; fixed and covered by regression test. Schema upgrade is tested against a pre-column database. No real player save was used in browser QA.
