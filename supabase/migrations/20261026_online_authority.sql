DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'game') THEN
        CREATE SCHEMA game;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS game."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030042413_OnlineBaseline') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'game') THEN
            CREATE SCHEMA game;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030042413_OnlineBaseline') THEN
    CREATE TABLE game."OnlineRequests" (
        "OwnerId" uuid NOT NULL,
        "RequestId" uuid NOT NULL,
        "Fingerprint" character varying(64) NOT NULL,
        "Response" text NOT NULL,
        "Status" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_OnlineRequests" PRIMARY KEY ("OwnerId", "RequestId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030042413_OnlineBaseline') THEN
    CREATE TABLE game."Players" (
        "Id" uuid NOT NULL,
        "OwnerId" uuid NOT NULL,
        "LastChannelAt" timestamp with time zone NOT NULL,
        "Name" character varying(64) NOT NULL,
        "StarterChosen" boolean NOT NULL,
        "Realm" integer NOT NULL,
        "Stage" integer NOT NULL,
        "Qi" double precision NOT NULL,
        "Bonus" double precision NOT NULL,
        "PillBonus" double precision NOT NULL,
        "Skin" text NOT NULL,
        "Gender" text NOT NULL,
        "SkinsUnlocked" text NOT NULL,
        "AutoExplore" boolean NOT NULL,
        "ExploreReadyAt" timestamp with time zone NOT NULL,
        "LastTickAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "Area" integer NOT NULL,
        "AreaMax" integer NOT NULL,
        "Chapter" integer NOT NULL,
        "ChapterMax" integer NOT NULL,
        "MapVer" integer NOT NULL,
        "Diff" integer NOT NULL,
        "DiffProg" text NOT NULL,
        "Cleared" text NOT NULL,
        "MapRealm" integer NOT NULL,
        "BreakCooldownUntil" timestamp with time zone NOT NULL,
        "DebuffUntil" timestamp with time zone NOT NULL,
        "Skills" text NOT NULL,
        "LearnedManuals" text NOT NULL,
        "ManualVersion" integer NOT NULL,
        "ManualProgress" text NOT NULL,
        "ActiveManual" text NOT NULL,
        "BossKills" integer NOT NULL,
        "ElixirPoints" integer NOT NULL,
        "SkillSlots" text NOT NULL,
        "Admin" text NOT NULL,
        "AdminPoints" integer NOT NULL,
        "Redeemed" text NOT NULL,
        "SpinHistory" text NOT NULL,
        "SpinDay" text NOT NULL,
        "SpinDailyUsed" integer NOT NULL,
        "PetSpinDay" text NOT NULL,
        "PetSpinDailyUsed" integer NOT NULL,
        "SpinPityUsed" integer NOT NULL,
        "PetSpinPityUsed" integer NOT NULL,
        "EventKey" text NOT NULL,
        "EventPoints" integer NOT NULL,
        "EventClaimed" text NOT NULL,
        "LbSecret" text NOT NULL,
        "Tainted" boolean NOT NULL,
        "Flagged" boolean NOT NULL,
        "AlchemyExp" integer NOT NULL,
        "RefineRecipe" integer NOT NULL,
        "RefineStartAt" timestamp with time zone NOT NULL,
        "RefineEndAt" timestamp with time zone NOT NULL,
        "RefineLeft" integer NOT NULL,
        "RefineCatalyst" boolean NOT NULL,
        "LastRefine" text NOT NULL,
        "LastRefineAt" timestamp with time zone NOT NULL,
        "BodyHp" bigint NOT NULL,
        "BodyDef" bigint NOT NULL,
        "Hatch" text NOT NULL,
        "GemVault" text NOT NULL,
        "GemSlots" text NOT NULL,
        CONSTRAINT "PK_Players" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030042413_OnlineBaseline') THEN
    CREATE TABLE game."Equipment" (
        "PlayerId" uuid NOT NULL,
        "Slot" character varying(20) NOT NULL,
        "ItemId" character varying(40) NOT NULL,
        CONSTRAINT "PK_Equipment" PRIMARY KEY ("PlayerId", "Slot"),
        CONSTRAINT "FK_Equipment_Players_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES game."Players" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030042413_OnlineBaseline') THEN
    CREATE TABLE game."Inventory" (
        "PlayerId" uuid NOT NULL,
        "ItemId" character varying(40) NOT NULL,
        "Count" integer NOT NULL,
        CONSTRAINT "PK_Inventory" PRIMARY KEY ("PlayerId", "ItemId"),
        CONSTRAINT "FK_Inventory_Players_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES game."Players" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030042413_OnlineBaseline') THEN
    CREATE TABLE game."Logs" (
        "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
        "PlayerId" uuid NOT NULL,
        "At" timestamp with time zone NOT NULL,
        "Text" character varying(500) NOT NULL,
        "Kind" character varying(10) NOT NULL,
        CONSTRAINT "PK_Logs" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Logs_Players_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES game."Players" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030042413_OnlineBaseline') THEN
    CREATE TABLE game."Pets" (
        "Id" uuid NOT NULL,
        "PlayerId" uuid NOT NULL,
        "DefId" text NOT NULL,
        "Level" integer NOT NULL,
        "Exp" integer NOT NULL,
        "Form" integer NOT NULL,
        "AppearanceForm" integer NOT NULL,
        "Quality" integer NOT NULL,
        "Stars" integer NOT NULL,
        "StarPts" integer NOT NULL,
        "AptAtk" integer NOT NULL,
        "AptDef" integer NOT NULL,
        "AptHp" integer NOT NULL,
        "Slot" integer NOT NULL,
        "FoodSpent" integer NOT NULL,
        "Awaken" integer NOT NULL,
        "GearCollar" text NOT NULL,
        "GearArmor" text NOT NULL,
        "GearCharm" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Pets" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Pets_Players_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES game."Players" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030042413_OnlineBaseline') THEN
    CREATE INDEX "IX_Logs_PlayerId_Id" ON game."Logs" ("PlayerId", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030042413_OnlineBaseline') THEN
    CREATE INDEX "IX_OnlineRequests_OwnerId_CreatedAt" ON game."OnlineRequests" ("OwnerId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030042413_OnlineBaseline') THEN
    CREATE INDEX "IX_Pets_PlayerId" ON game."Pets" ("PlayerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030042413_OnlineBaseline') THEN
    CREATE UNIQUE INDEX "IX_Players_OwnerId" ON game."Players" ("OwnerId") WHERE "OwnerId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030042413_OnlineBaseline') THEN
    DO $$ BEGIN IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname='game_backend') THEN CREATE ROLE game_backend NOLOGIN; END IF; END $$;
    ALTER TABLE game."Players" ADD CONSTRAINT players_auth_owner_fk FOREIGN KEY ("OwnerId") REFERENCES auth.users(id) ON DELETE CASCADE;
    ALTER TABLE game."OnlineRequests" ADD CONSTRAINT requests_auth_owner_fk FOREIGN KEY ("OwnerId") REFERENCES auth.users(id) ON DELETE CASCADE;
    REVOKE ALL ON SCHEMA game FROM PUBLIC, anon, authenticated;
    REVOKE ALL ON ALL TABLES IN SCHEMA game FROM PUBLIC, anon, authenticated;
    REVOKE ALL ON ALL SEQUENCES IN SCHEMA game FROM PUBLIC, anon, authenticated;
    ALTER DEFAULT PRIVILEGES IN SCHEMA game REVOKE ALL ON TABLES FROM PUBLIC, anon, authenticated;
    GRANT USAGE ON SCHEMA game TO game_backend;GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA game TO game_backend;
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA game TO game_backend;
    DO $$ DECLARE t record; BEGIN FOR t IN SELECT tablename FROM pg_tables WHERE schemaname='game' LOOP
      EXECUTE format('ALTER TABLE game.%I ENABLE ROW LEVEL SECURITY',t.tablename);
      EXECUTE format('DROP POLICY IF EXISTS backend_only ON game.%I',t.tablename);
      EXECUTE format('CREATE POLICY backend_only ON game.%I TO game_backend USING (true) WITH CHECK (true)',t.tablename);
    END LOOP; END $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030042413_OnlineBaseline') THEN
    INSERT INTO game."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030042413_OnlineBaseline', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030101521_OnlineLeaderboard') THEN
    CREATE TABLE game."LeaderboardEntries" (
        "PlayerId" uuid NOT NULL,
        "Name" character varying(64) NOT NULL,
        "Realm" integer NOT NULL,
        "Stage" integer NOT NULL,
        "Level" integer NOT NULL,
        "Qi" double precision NOT NULL,
        "RealmName" character varying(64) NOT NULL,
        "StageName" character varying(64) NOT NULL,
        "Power" bigint NOT NULL,
        "Progress" bigint NOT NULL,
        "PetPower" bigint NOT NULL,
        "Equipment" text NOT NULL,
        "Stats" text NOT NULL,
        "Flagged" boolean NOT NULL,
        "Key" text NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_LeaderboardEntries" PRIMARY KEY ("PlayerId"),
        CONSTRAINT "FK_LeaderboardEntries_Players_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES game."Players" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030101521_OnlineLeaderboard') THEN
    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA game TO game_backend;
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA game TO game_backend;
    DO $$ DECLARE t record; BEGIN FOR t IN SELECT tablename FROM pg_tables WHERE schemaname='game' LOOP
      EXECUTE format('ALTER TABLE game.%I ENABLE ROW LEVEL SECURITY',t.tablename);
      EXECUTE format('DROP POLICY IF EXISTS backend_only ON game.%I',t.tablename);
      EXECUTE format('CREATE POLICY backend_only ON game.%I TO game_backend USING (true) WITH CHECK (true)',t.tablename);
    END LOOP; END $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030101521_OnlineLeaderboard') THEN
    INSERT INTO game."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030101521_OnlineLeaderboard', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030103203_OnlineMarket') THEN
    CREATE TABLE game."MarketListings" (
        "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
        "SellerId" uuid NOT NULL,
        "SellerName" character varying(64) NOT NULL,
        "ItemId" character varying(40) NOT NULL,
        "Qty" integer NOT NULL,
        "Price" bigint NOT NULL,
        "Status" character varying(10) NOT NULL,
        "BuyerId" uuid,
        "BuyerName" character varying(64),
        "Payout" bigint NOT NULL,
        "SellerClaimed" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "ClosedAt" timestamp with time zone,
        CONSTRAINT "PK_MarketListings" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_MarketListings_Players_SellerId" FOREIGN KEY ("SellerId") REFERENCES game."Players" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030103203_OnlineMarket') THEN
    CREATE INDEX "IX_MarketListings_BuyerId_ClosedAt" ON game."MarketListings" ("BuyerId", "ClosedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030103203_OnlineMarket') THEN
    CREATE INDEX "IX_MarketListings_SellerId_Status" ON game."MarketListings" ("SellerId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030103203_OnlineMarket') THEN
    CREATE INDEX "IX_MarketListings_Status_CreatedAt" ON game."MarketListings" ("Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030103203_OnlineMarket') THEN
    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA game TO game_backend;
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA game TO game_backend;
    DO $$ DECLARE t record; BEGIN FOR t IN SELECT tablename FROM pg_tables WHERE schemaname='game' LOOP
      EXECUTE format('ALTER TABLE game.%I ENABLE ROW LEVEL SECURITY',t.tablename);
      EXECUTE format('DROP POLICY IF EXISTS backend_only ON game.%I',t.tablename);
      EXECUTE format('CREATE POLICY backend_only ON game.%I TO game_backend USING (true) WITH CHECK (true)',t.tablename);
    END LOOP; END $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030103203_OnlineMarket') THEN
    INSERT INTO game."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030103203_OnlineMarket', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030103938_OnlineArena') THEN
    ALTER TABLE game."LeaderboardEntries" ADD "Loadout" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030103938_OnlineArena') THEN
    CREATE TABLE game."ArenaFights" (
        "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
        "AttackerId" uuid NOT NULL,
        "DefenderId" uuid NOT NULL,
        "AttackerName" character varying(64) NOT NULL,
        "DefenderName" character varying(64) NOT NULL,
        "Win" boolean NOT NULL,
        "AttackerRank" bigint NOT NULL,
        "DefenderRank" bigint NOT NULL,
        "Swapped" boolean NOT NULL,
        "Seed" bigint NOT NULL,
        "Status" character varying(10) NOT NULL,
        "Practice" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ArenaFights" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030103938_OnlineArena') THEN
    CREATE TABLE game."ArenaRanks" (
        "PlayerId" uuid NOT NULL,
        "Pos" bigint NOT NULL,
        "Wins" integer NOT NULL,
        "Losses" integer NOT NULL,
        "JoinedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ArenaRanks" PRIMARY KEY ("PlayerId"),
        CONSTRAINT "FK_ArenaRanks_Players_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES game."Players" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030103938_OnlineArena') THEN
    CREATE INDEX "IX_ArenaFights_AttackerId_CreatedAt" ON game."ArenaFights" ("AttackerId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030103938_OnlineArena') THEN
    CREATE INDEX "IX_ArenaFights_DefenderId_CreatedAt" ON game."ArenaFights" ("DefenderId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030103938_OnlineArena') THEN
    CREATE UNIQUE INDEX "IX_ArenaRanks_Pos" ON game."ArenaRanks" ("Pos");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030103938_OnlineArena') THEN
    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA game TO game_backend;
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA game TO game_backend;
    DO $$ DECLARE t record; BEGIN FOR t IN SELECT tablename FROM pg_tables WHERE schemaname='game' LOOP
      EXECUTE format('ALTER TABLE game.%I ENABLE ROW LEVEL SECURITY',t.tablename);
      EXECUTE format('DROP POLICY IF EXISTS backend_only ON game.%I',t.tablename);
      EXECUTE format('CREATE POLICY backend_only ON game.%I TO game_backend USING (true) WITH CHECK (true)',t.tablename);
    END LOOP; END $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game."__EFMigrationsHistory" WHERE "MigrationId" = '20261030103938_OnlineArena') THEN
    INSERT INTO game."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030103938_OnlineArena', '8.0.11');
    END IF;
END $EF$;
COMMIT;

