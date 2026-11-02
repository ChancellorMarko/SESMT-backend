using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Infrastructure.Persistence.Queries;
using SESMT.Infrastructure.Persistence.Repositories;
using SESMT.Infrastructure.Persistence.Repositories.Documents;
using SESMT.Infrastructure.Persistence.Repositories.WorkPermits;
using Fixture = SESMT.Tests.Unit.Identity.NativeResourceAuthorizationTests.Fixture;

namespace SESMT.Tests.Unit.Identity;

public sealed class GlobalAdministrationScopeTests
{
    private static async Task Grant(Fixture f, string resource)
    {
        var permission = new Permission(resource, "ler", "Teste"); f.Db.AddRange(permission, new RolePermission(f.Role.Id, permission.Id)); await f.Db.SaveChangesAsync();
    }
    [Fact]
    public async Task GlobalScopeNeedsItsOwnActiveActionAndDoesNotPromoteCustomOrCompanyAssignments()
    {
        foreach (var (code, global) in new[] { ("CUSTOM", true), ("ADMIN", false), ("ADMIN", true) })
        {
            await using var f = await Fixture.Create("EMPRESA", code, global); await Grant(f, "pt");
            var ids = await GlobalAdministrationScope.CompanyIdsAsync(f.Db, f.Actor.Id, "pt", "ler", default);
            Assert.Equal(code == "ADMIN" && global ? 2 : 0, ids.Length);
            Assert.Empty(await GlobalAdministrationScope.CompanyIdsAsync(f.Db, f.Actor.Id, "pt", "liberar", default));
            f.Role.MarkAsDeleted(); await f.Db.SaveChangesAsync();
            Assert.Empty(await GlobalAdministrationScope.CompanyIdsAsync(f.Db, f.Actor.Id, "pt", "ler", default));
        }
    }
    [Fact]
    public async Task AdministrativeDashboardReadsOtherRequestsAndOnlyActiveCompanyScope()
    {
        await using var f = await Fixture.Create("EMPRESA", "ADMIN", true); await Grant(f, "pt");
        var first = new WorkPermitAppointment(f.CompanyA.Id, "Exemplo visível", DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(2), Guid.NewGuid(), f.Units[0].Id);
        var hidden = new WorkPermitAppointment(f.CompanyB.Id, "Empresa inativa", DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(2), Guid.NewGuid(), f.Units[2].Id);
        f.Db.AddRange(first, hidden); f.CompanyB.Deactivate(); await f.Db.SaveChangesAsync();
        var result = await new DashboardRepository(f.Db).GetDataAsync(f.Actor.Id, default);
        Assert.Equal(first.Id, Assert.Single(result.Appointments).Id); Assert.Contains(f.Units[1].Id, result.AllowedLocalIds); Assert.DoesNotContain(f.Units[2].Id, result.AllowedLocalIds);
        Assert.Equal(f.CompanyA.Id, Assert.Single(await new WorkPermitRepository(f.Db).GetAccessibleCompanyIdsAsync(f.Actor.Id)));
        Assert.False(await new WorkPermitRepository(f.Db).HasRoleInCompanyAsync(f.Actor.Id, f.CompanyA.Id, "SESMT_PT_OPERADOR"));
    }
    [Fact]
    public async Task GlobalDocumentReadKeepsMedicalRecordsProtected()
    {
        await using var f = await Fixture.Create("EMPRESA", "ADMIN", true); await Grant(f, "documento");
        var normal = new DocumentType("ADMIN", "Administrativo", null, "administrativo", SecrecyLevel.Internal, false, false, null, null, "{}", 1);
        var medical = new DocumentType("HEALTH", "Saúde", null, "saude", SecrecyLevel.Medical, false, false, null, null, "{}", 1); f.Db.AddRange(normal, medical); await f.Db.SaveChangesAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Document Doc(Guid type, string secrecy) => new(type, f.CompanyA.Id, f.Units[0].Id, null, Guid.NewGuid().ToString("N"), "Exemplo", null, secrecy, today, null, null, f.Actor.Id);
        var policy = new DocumentAccessPolicy(f.Db);
        Assert.True(await policy.CanAccessAsync(f.Actor.Id, Doc(normal.Id, SecrecyLevel.Internal), "ler", default));
        Assert.False(await policy.CanAccessAsync(f.Actor.Id, Doc(medical.Id, SecrecyLevel.Medical), "ler", default));
    }
}
