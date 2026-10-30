using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace VanDao.Online;

/// <summary>Online schema settings shared by the backend, `dotnet ef` and the local tools.</summary>
public static class OnlineSchema
{
    public static void Npgsql(NpgsqlDbContextOptionsBuilder o) => o.MigrationsHistoryTable("__EFMigrationsHistory", "game");

    /// <summary>Only game_backend may touch schema game; Supabase anon/authenticated roles are locked out.</summary>
    public const string Security = """
        DO $$ BEGIN IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname='game_backend') THEN CREATE ROLE game_backend NOLOGIN; END IF; END $$;
        ALTER TABLE game."Players" ADD CONSTRAINT players_auth_owner_fk FOREIGN KEY ("OwnerId") REFERENCES auth.users(id) ON DELETE CASCADE;
        ALTER TABLE game."OnlineRequests" ADD CONSTRAINT requests_auth_owner_fk FOREIGN KEY ("OwnerId") REFERENCES auth.users(id) ON DELETE CASCADE;
        REVOKE ALL ON SCHEMA game FROM PUBLIC, anon, authenticated;
        REVOKE ALL ON ALL TABLES IN SCHEMA game FROM PUBLIC, anon, authenticated;
        REVOKE ALL ON ALL SEQUENCES IN SCHEMA game FROM PUBLIC, anon, authenticated;
        ALTER DEFAULT PRIVILEGES IN SCHEMA game REVOKE ALL ON TABLES FROM PUBLIC, anon, authenticated;
        GRANT USAGE ON SCHEMA game TO game_backend;
        """ + Grants;

    /// <summary>Re-run after every migration so tables added later get the same grants and RLS.</summary>
    public const string Grants = """
        GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA game TO game_backend;
        GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA game TO game_backend;
        DO $$ DECLARE t record; BEGIN FOR t IN SELECT tablename FROM pg_tables WHERE schemaname='game' LOOP
          EXECUTE format('ALTER TABLE game.%I ENABLE ROW LEVEL SECURITY',t.tablename);
          EXECUTE format('DROP POLICY IF EXISTS backend_only ON game.%I',t.tablename);
          EXECUTE format('CREATE POLICY backend_only ON game.%I TO game_backend USING (true) WITH CHECK (true)',t.tablename);
        END LOOP; END $$;
        """;
}
