-- Chỉ chạy phần MỚI: hạng Lôi đài (Đồng/Bạc/Vàng/…) — migration 20261112090000_OnlineArenaTier. Idempotent.
-- Yêu cầu: đã chạy 20261112_online_sect_dailyquest.sql (hoặc 20261026_online_authority.sql tới OnlineSectBoss).
-- Nội dung giống hệt khối cuối của 20261026_online_authority.sql.

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261112090000_OnlineArenaTier') THEN
    ALTER TABLE game."ArenaRanks" ADD COLUMN IF NOT EXISTS "Points" integer NOT NULL DEFAULT 0;
    ALTER TABLE game."ArenaRanks" ADD COLUMN IF NOT EXISTS "Protect" integer NOT NULL DEFAULT 0;
    ALTER TABLE game."ArenaRanks" ADD COLUMN IF NOT EXISTS "DailyClaimDay" text NOT NULL DEFAULT '';
    ALTER TABLE game."ArenaRanks" ADD COLUMN IF NOT EXISTS "WeeklyClaimWeek" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261112090000_OnlineArenaTier') THEN
    INSERT INTO game."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261112090000_OnlineArenaTier', '8.0.11');
    END IF;
END $EF$;
COMMIT;
