using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Infrastructure.Persistence.Repositories.WorkPermits;
using SESMT.Infrastructure.Persistence.Seeding;

namespace SESMT.Tests.Unit.Identity;

public sealed class BackendSafetyRegressionTests
{
    [Fact]
    public void ProductionBootstrapRejectsDevelopmentDefaults()
        => Assert.False(new SeedSettings().HasSafeProductionBootstrap());

    [Fact]
    public void ProductionCanExplicitlyDisableInitialAccountBootstrap()
        => Assert.True(new SeedSettings { BootstrapAdminEnabled = false }.HasSafeProductionBootstrap());

    [Theory]
    [InlineData("invalid", "TestOnly-NotARealCredential-123!")]
    [InlineData("admin@example.invalid", "short")]
    [InlineData("", "TestOnly-NotARealCredential-123!")]
    public void ProductionBootstrapRejectsInvalidSettings(string email, string password)
        => Assert.False(new SeedSettings { AdminEmail = email, AdminPassword = password }.HasSafeProductionBootstrap());

    [Fact]
    public void ProductionBootstrapAcceptsExplicitNonDefaultSettings()
        => Assert.True(new SeedSettings { AdminEmail = "admin@example.invalid", AdminPassword = "TestOnly-NotARealCredential-123!" }.HasSafeProductionBootstrap());

    [Fact]
    public async Task DuplicateDesignationUsesSetsAndDoesNotReuseRevokedHistory()
    {
        await using var f = await NativeResourceAuthorizationTests.Fixture.Create("EMPRESA");
        var services = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var locations = new[] { f.Units[0].Id, f.Units[1].Id };
        var starts = DateTimeOffset.UtcNow; var ends = starts.AddHours(1);
        var previous = new OperationalDesignation(f.CompanyA.Id, f.Workers[0].Id, services, locations, "atender", starts, ends, "Teste", f.Actor.Id);
        f.Db.Add(previous); await f.Db.SaveChangesAsync();
        var repository = new OperationalDesignationRepository(f.Db);
        var reordered = new OperationalDesignation(f.CompanyA.Id, f.Workers[0].Id, services.Reverse().ToArray(), locations.Reverse().ToArray(), "atender", starts, ends, "Teste", f.Actor.Id);
        Assert.Equal(previous.Id, (await repository.FindDuplicateAsync(reordered))!.Id);
        previous.Revoke(f.Actor.Id, previous.Version); await f.Db.SaveChangesAsync();
        Assert.Null(await repository.FindDuplicateAsync(reordered));
    }

    [Fact]
    public async Task AppointmentRosterUsesWorkerUserLinkAndRejectsMissingOrForeignWorkers()
    {
        await using var f = await NativeResourceAuthorizationTests.Fixture.Create("EMPRESA");
        var appointment = new WorkPermitAppointment(f.CompanyA.Id, "Teste", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), f.Actor.Id, f.Units[0].Id);
        appointment.ConfigureContext(Guid.NewGuid(), ["ALTURA"], [f.Workers[0].Id], f.Actor.Id, appointment.Version);
        var repository = new WorkPermitRepository(f.Db);
        Assert.Equal(f.Actor.Id, Assert.Single((await repository.GetAppointmentParticipantUserIdsAsync(appointment))!));
        Assert.NotEqual(f.Actor.Id, f.Workers[0].Id);
        appointment.ConfigureContext(Guid.NewGuid(), ["ALTURA"], [f.Workers[1].Id], f.Actor.Id, appointment.Version);
        Assert.Null(await repository.GetAppointmentParticipantUserIdsAsync(appointment));
        appointment.ConfigureContext(Guid.NewGuid(), ["ALTURA"], [f.Workers[3].Id], f.Actor.Id, appointment.Version);
        Assert.Null(await repository.GetAppointmentParticipantUserIdsAsync(appointment));
    }
}
