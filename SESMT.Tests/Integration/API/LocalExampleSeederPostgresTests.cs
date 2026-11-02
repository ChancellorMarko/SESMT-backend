using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SESMT.API.Services;
using SESMT.Application.Common.Interfaces;
using SESMT.Domain.Modules.WorkPermits.Constants;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class LocalExampleSeederPostgresTests
{
    [Theory]
    [InlineData("Production", "Host=127.0.0.1;Database=ampara_fullstack_preview_20261102")]
    [InlineData("Development", "Host=example.com;Database=ampara_fullstack_preview_20261102")]
    [InlineData("Development", "Host=127.0.0.1;Database=ampara_integration")]
    [InlineData("Development", "Host=127.0.0.1;Database=postgres")]
    public void OrdinaryOrProductionDatabasesAreNeverSeedTargets(string environment, string connection)
        => Assert.Throws<InvalidOperationException>(() => LocalExampleSeeder.RequireLocalTarget(environment, connection));

    [PostgresFact]
    public async Task CreatesOnlyAnIsolatedExampleCompanyAndRerunPreservesEditsAndPasswords()
    {
        var database = new PostgresDatabaseFixture(); await database.InitializeAsync();
        try
        {
            await using var factory = new PostgresApiFactory(database); using var client = factory.CreateClient();
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SESMT.Infrastructure.Persistence.SesmtDbContext>();
            var hash = scope.ServiceProvider.GetRequiredService<IPasswordHashService>(); var cpf = scope.ServiceProvider.GetRequiredService<ICpfProtectionService>();
            var beforeWorkers = await db.Workers.CountAsync(w => w.CompanyId == database.CompanyA);
            var result = await LocalExampleSeeder.SeedAsync(db, hash, cpf, "seed-admin@example.invalid", "ExampleTestOnly-2026!", DateTimeOffset.UtcNow);
            Assert.False(result.AlreadyPresent); Assert.Equal(8, result.Users); Assert.Equal(8, result.Workers); Assert.Equal(2, result.Units);
            Assert.True(result.CredentialsMatch);
            Assert.Equal(8, result.Documents); Assert.Equal(6, result.Appointments); Assert.Equal(3, result.Permits);
            Assert.All(await db.WorkPermits.Where(p => p.CompanyId == result.CompanyId).ToListAsync(), p => Assert.Equal(WorkPermitStatus.UnderReview, p.Status));
            Assert.All(await db.WorkPermitParticipants.Where(p => db.WorkPermits.Any(w => w.Id == p.WorkPermitId && w.CompanyId == result.CompanyId)).ToListAsync(), p => Assert.Null(p.AcknowledgedAt));
            var person = await db.Users.SingleAsync(u => u.Email.Value == "tecnico@exemplo.ampara.local"); var originalHash = person.PasswordHash;
            person.Update("Exemplo Nome Editado Pela Equipe", person.Email.Value); await db.SaveChangesAsync();
            var repeated = await LocalExampleSeeder.SeedAsync(db, hash, cpf, "seed-admin@example.invalid", "AnotherTestOnly-2026!", DateTimeOffset.UtcNow.AddDays(2));
            Assert.True(repeated.AlreadyPresent); Assert.Equal(result.Workers, repeated.Workers); Assert.Equal(result.Permits, repeated.Permits);
            Assert.False(repeated.CredentialsMatch);
            await db.Entry(person).ReloadAsync(); Assert.Equal("Exemplo Nome Editado Pela Equipe", person.Name); Assert.Equal(originalHash, person.PasswordHash);
            Assert.Equal(beforeWorkers, await db.Workers.CountAsync(w => w.CompanyId == database.CompanyA));
        }
        finally { await database.DisposeAsync(); }
    }
}
