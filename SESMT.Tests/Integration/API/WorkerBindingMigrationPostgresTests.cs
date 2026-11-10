using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace SESMT.Tests.Integration.API;
[Collection(PostgresCollection.Name)]
public sealed class WorkerBindingMigrationPostgresTests
{
    [PostgresFact]
    public async Task UpgradePreservesIdentityAndBackfillsTheRevision()
    {
        var isolated = new PostgresDatabaseFixture();
        try
        {
            await isolated.InitializeAsync(); await using var db = isolated.CreateContext(); var original = await db.Workers.AsNoTracking().SingleAsync(w => w.Id == isolated.WorkerA);
            var (before, target) = BindingMigration(db); var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(before); await migrator.MigrateAsync(target);
            await using var verify = isolated.CreateContext(); var current = await verify.Workers.SingleAsync(w => w.Id == original.Id);
            Assert.Equal(1, current.Revision); Assert.Equal(original.UserId, current.UserId); Assert.Equal(original.CpfEncryption, current.CpfEncryption); Assert.Equal(original.CpfHash, current.CpfHash);
            Assert.Equal(original.Name.Value, current.Name.Value); Assert.Equal(original.UnitId, current.UnitId); Assert.Equal(original.DateOfBirth, current.DateOfBirth);
        }
        finally { await isolated.DisposeAsync(); }
    }
    [PostgresFact]
    public async Task HistoricalDuplicatesBlockTheMigrationWithoutChoosingOrDeletingAnIdentity()
    {
        var isolated = new PostgresDatabaseFixture();
        try
        {
            await isolated.InitializeAsync(); await using var db = isolated.CreateContext(); var (before, target) = BindingMigration(db); var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(before);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE core.trabalhador SET usuario_id = {isolated.ActorA} WHERE id = {isolated.WorkerB}");
            var failure = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(target)); Assert.Contains("Revise os vinculos", failure.MessageText);
            Assert.Equal(2, await db.Database.SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM core.trabalhador WHERE usuario_id = {isolated.ActorA}").SingleAsync());
        }
        finally { await isolated.DisposeAsync(); }
    }

    // Located by name: later migrations are rolled back first, so the test keeps working as new ones are added.
    private static (string Before, string Target) BindingMigration(DbContext db)
    {
        var migrations = db.Database.GetMigrations().ToArray();
        var index = Array.FindIndex(migrations, m => m.EndsWith("_AddWorkerAccountBindingConcurrency", StringComparison.Ordinal));
        Assert.True(index > 0, "AddWorkerAccountBindingConcurrency migration not found.");
        return (migrations[index - 1], migrations[index]);
    }
}
