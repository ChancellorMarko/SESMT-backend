using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class TeamMigrationPostgresTests
{
    [PostgresFact]
    public async Task UpgradeBackfillsExistingTeamsWithoutChangingTheirIdentityOrRoster()
    {
        // Own a separate disposable database so downgrading never affects another test's data.
        var isolated = new PostgresDatabaseFixture();
        try
        {
            await isolated.InitializeAsync();
            await using var db = isolated.CreateContext();
            var original = await db.Teams.AsNoTracking().SingleAsync(t => t.Id == isolated.TeamA);
            var migrations = db.Database.GetMigrations().ToArray(); var index = Array.FindIndex(migrations, m => m.EndsWith("AddTeamOptimisticConcurrency")); Assert.True(index > 0);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(migrations[index - 1]);
            await migrator.MigrateAsync(migrations[index]);
            await using var verify = isolated.CreateContext(); var current = await verify.Teams.SingleAsync(t => t.Id == isolated.TeamA);
            Assert.Equal(1, current.Version); Assert.Equal(original.OrganizationId, current.OrganizationId); Assert.Equal(original.CompanyId, current.CompanyId);
            Assert.Equal(original.LocalId, current.LocalId); Assert.Equal(original.Name, current.Name); Assert.Equal(original.ChiefId, current.ChiefId); Assert.Equal(original.ParticipantIds, current.ParticipantIds);
        }
        finally { await isolated.DisposeAsync(); }
    }
}
