using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using VanDao.Data;

namespace VanDao.Online;

/// <summary>Used by `dotnet ef` and the schema test; online migrations always target PostgreSQL.</summary>
public sealed class OnlineDesignTimeFactory : IDesignTimeDbContextFactory<GameDbContext>
{
    public GameDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("VANDAO_MIGRATIONS_DB") ?? "Host=127.0.0.1;Port=55432;Database=postgres", OnlineSchema.Npgsql)
            .Options);
}
