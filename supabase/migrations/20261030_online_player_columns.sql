-- Columns added on master after 20261026_online_authority.sql was generated.
BEGIN;
ALTER TABLE game."Players" ADD COLUMN IF NOT EXISTS "StarterChosen" boolean NOT NULL DEFAULT true;
ALTER TABLE game."Players" ADD COLUMN IF NOT EXISTS "ManualProgress" text NOT NULL DEFAULT '';
ALTER TABLE game."Players" ADD COLUMN IF NOT EXISTS "GemVault" text NOT NULL DEFAULT '';
ALTER TABLE game."Players" ADD COLUMN IF NOT EXISTS "GemSlots" text NOT NULL DEFAULT '';
COMMIT;
