# Vấn Đạo (Người Lười Tu Tiên)

@AGENTS.md

Game idle tu tiên: backend ASP.NET Core 8 (C#) giữ toàn bộ logic/chỉ số, frontend `wwwroot/` (JS thuần) chỉ hiển thị. Desktop (WebView2) + Launcher + server online (Supabase/Postgres).

## Quy tắc tiết kiệm token

- Đọc đúng file theo bản đồ dưới đây. Dùng Grep tìm tên hằng/hàm, rồi Read với `offset/limit`. Không đọc nguyên `GameService.cs` (~2.500 dòng) hoặc `GameData.cs` (~1.600 dòng).
- Không tìm trong `bin/`, `obj/`, `build-check/`, `publish/`, `wwwroot/img/`, `site/img/`, `tools/*/bin/`.
- Sửa chỉ số: chỉ đổi giá trị + chú thích ngắn (`// cân bằng MM/YYYY: ...`), không refactor, không giải thích dài.
- Chỉ chạy test liên quan đến phần đã sửa (xem mục Test).

## Bản đồ chỉ số (C#, `Game/`)

| Chủ đề | Nơi sửa |
|---|---|
| Cảnh giới, tầng, qi cần, tỉ lệ đột phá, ô túi | `GameData.Realms`, `QiGrowth`, `StatGrowth`, `Break*` const, `StageBreakChance` — `GameData.cs` |
| Phạt đột phá thất bại / cooldown | `BreakFailLoss`, `BreakMajorLoss`, `BreakFailCooldown`, `Debuff*` — `GameData.cs` |
| Vật phẩm, đan, linh thảo, nguyên liệu Boss | `GameData.Items` (+ `SellLoot.cs`, `HerbExpansion.cs`, `FinalRealmData.cs`) |
| Trang bị: chỉ số gốc, phẩm, hệ số | `GearBases`, `GearLevels`, `QualityMult`, `GearAtkHpMult`, `HapLinh` — `GameData.cs`; ngân sách: `EquipmentBudget.cs`, `EquipmentEffects.cs` |
| Luyện khí (ghép 9 món) | `FuseCount`, `FuseChance` — `GameData.cs`; logic `GameService.Fuse` |
| Giá bán / Tạp Hóa | `RarityPrice`, `PillSellPrice`, `Shop` — `GameData.cs`; `GameService.Shop.cs` |
| Vòng quay | `SpinTicketPrice`, `SpinWeights` — `GameData.cs`; `SpinEvent.cs`, `SpinPity.cs`, `GameService.SpinEvent.cs/SpinDaily.cs` |
| Quái, chương, độ khó | `Chapters`, `ChapterMult`, `ChapterMonHp/Atk`, `MonsterHpMult`, `Elite*`, `Boss*`, `Diff*`, `Nightmare*` — `GameData.cs`; `HellMapData.cs`, `WanderingBoss.cs`, `MonsterRageCombat.cs`, `HellMonsterSkills.cs` |
| Rương, tỉ lệ rơi | `Chests`, `ChestChance`, `DropQuality`, `DiffChest*` — `GameData.cs`; `ChestRewards.cs`, `PetLoot.cs` |
| Luyện đan, đan phương | `HerbDrops`, `Brews`, `BreakPillMax` — `GameData.cs`; cấp luyện đan `Alchemy.cs`; đan hỗ trợ `SupportPills.cs` |
| Công pháp / kỹ năng | `SkillData.cs`, `ElementSkillData.cs`, `CultivationManuals.cs`, `SkillEffectCombat.cs`, `SkillSecondaryEffects.cs` |
| Tinh thạch (gem) | `GemData.cs`, `GemProgression.cs`, `GemRatingBudget.cs`, `GemCombat.cs` |
| Chỉ số chiến đấu (bạo, né, xuyên…) | `CombatRatings.cs`, `CombatRatingRules.cs`, `CombatDodge.cs`, `CombatBurn.cs`, `ComboCombat.cs`, `ElementCounter.cs`, `MonsterCombatRatings.cs` |
| Tốc độ trận, thời gian chuyến | `WalkSeconds`, `HitSeconds`, `ExploreQiSeconds`, `MaxOfflineSeconds` — `GameData.cs`; `BattleTiming.cs` |
| Linh thú | `PetData.cs`, `PetCombat.cs`, `PetTraining.cs`, `GameService.Pet*.cs` |
| Kỳ duyên | `KyDuyen.Data.cs`, `KyDuyen.cs`, `GameService.KyDuyen.cs` |
| Đấu trường / PvP | `Arena.cs`, `ServerArena.cs`, `PvpCombat.cs`, `GameService.Arena.cs` |
| Chợ, VIP, code quà | `Market.cs`, `ServerMarket.cs`, `GameService.Market.cs`, `GameService.Vip.cs`, `DonateCodes.cs` |
| Bảng xếp hạng, chiến lực | `GameService.Power`, `Leaderboard.cs`, `ServerLeaderboard.cs` |
| Trang phục (skin) | `GameData.Skins`; sprite: `wwwroot/js/inventory-sprites.js` |

Công thức tổng hợp trong `GameService.cs`: `Stats`, `GearEffects`, `SkillBonus`, `BattleAt`, `Tick`, `Advance`, `Chance`, `ExploreQi`.

## Thêm chức năng mới

1. Dữ liệu/hằng: file `*Data.cs` riêng trong `Game/` (không nhồi thêm vào `GameData.cs` nếu là hệ thống mới).
2. Hành động: `partial class GameService` trong file mới `Game/GameService.<TênHệ>.cs`.
3. DTO gửi client: `Game/Dtos.cs`; trạng thái người chơi: `GameService.BuildStateAsync`.
4. API: `Program.cs` (`api.MapGet/MapPost`, kiểu `/players/{id}/...`).
5. Lưu DB: `Data/Entities.cs` + migration trong `Online/Migrations/`.
6. Giao diện: `wwwroot/js/<hệ>.js` + `wwwroot/css/<hệ>.css`, nạp trong `wwwroot/index.html`. Chữ hiển thị mới phải thêm bản tiếng Anh vào `wwwroot/js/i18n-en.js`.
7. Ghi chú phát hành cho người chơi: `docs/releases/upcoming.md`.

## Test

- C#: `dotnet test test/GemSystemTests --filter "FullyQualifiedName~<TênTest>" --nologo -v q -clp:ErrorsOnly --logger "console;verbosity=minimal"` (chỉ lọc test liên quan; output gọn).
- JS: `node --test --test-reporter=dot test/<tên>.test.cjs` (chỉ in chi tiết khi lỗi).
- Kiểm tra cân bằng bằng script riêng trong `tools/*_checks/` khi có.
