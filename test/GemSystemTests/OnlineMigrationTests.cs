#pragma warning disable EF1001 // model differ is internal, but it is what `dotnet ef migrations add` uses
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using VanDao.Data;
using VanDao.Online;

namespace GemSystemTests;

public class OnlineMigrationTests
{
    [Fact]
    public void OnlineMigrationsMatchTheEntityModel()
    {
        // No connection is opened: comparing the migration snapshot with the model is offline.
        using var db = new OnlineDesignTimeFactory().CreateDbContext([]);
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot?.Model;
        Assert.True(snapshot is not null, "Chưa có migration online. Chạy dotnet ef migrations add (xem tools/online_local/README.md).");
        if (snapshot is IMutableModel mutable) snapshot = mutable.FinalizeModel();
        snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot, designTime: true, validationLogger: null);
        var current = db.GetService<IDesignTimeModel>().Model;
        Assert.False(db.GetService<IMigrationsModelDiffer>().HasDifferences(snapshot.GetRelationalModel(), current.GetRelationalModel()),
            "Data/Entities.cs đã đổi mà chưa có migration online. Chạy dotnet ef migrations add <Tên> (xem tools/online_local/README.md).");
    }
}
