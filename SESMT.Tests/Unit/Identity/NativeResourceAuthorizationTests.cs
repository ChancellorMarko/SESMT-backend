using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SESMT.API.Controllers;
using SESMT.API.Services;
using SESMT.Application;
using SESMT.Application.Common.Interfaces;
using SESMT.Application.Exceptions;
using SESMT.Application.Modules.Organization.Interfaces;
using SESMT.Application.Modules.Organization.Services;
using SESMT.Application.Modules.Workforce.DTOs;
using SESMT.Application.Modules.Workforce.Interfaces;
using SESMT.Application.Modules.Workforce.Services;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.Organization.Enums;
using SESMT.Domain.Modules.Workforce.Entities;
using SESMT.Infrastructure.Persistence;
using SESMT.Infrastructure.Persistence.Repositories.Audit;
using SESMT.Infrastructure.Persistence.Repositories.IdentityAccess;
using SESMT.Infrastructure.Persistence.Repositories.Workforce;
using OrganizationUnit = SESMT.Domain.Modules.Organization.Entities.Unit;

namespace SESMT.Tests.Unit.Identity;

// Executes the actual EF hierarchy/grant reader and native services, without an application DB.
// Relational translation and transactions also have separate PostgreSQL scenarios.
public sealed class NativeResourceAuthorizationTests
{
    [Theory]
    [InlineData("UNIDADE", 2)]
    [InlineData("SETOR", 1)]
    [InlineData("EMPRESA", 3)]
    public async Task WorkerScopeIsAppliedBeforePaging(string scope, int expected)
    {
        await using var f = await Fixture.Create(scope);
        var service = new AuthorizedWorkerService(null!, f.Access());
        var first = await service.ListAsync(f.CompanyA.Id, pageSize: 1);
        Assert.Equal(f.Workers[0].Id, Assert.Single(first.Value!).Id);
        var all = await service.ListAsync(f.CompanyA.Id, pageSize: 200);
        Assert.Equal(expected, all.Value!.Count());
        Assert.DoesNotContain(all.Value!, w => w.Id == f.Workers[3].Id);
    }

    [Fact]
    public async Task ForeignOrNeighbourWorkerIsDeniedBeforeCpfAndSensitiveAudit()
    {
        await using var f = await Fixture.Create();
        var cpf = new CpfSpy();
        var core = new WorkerService(new WorkerRepository(f.Db), new WorkerHistoryRepository(f.Db), cpf,
            new UnitOfWork(f.Db), new AuditLogRepository(f.Db), f.Current);
        var service = new AuthorizedWorkerService(core, f.Access());
        foreach (var hidden in f.Workers.Skip(2))
            await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(hidden.Id));
        Assert.Equal(0, cpf.Reads);
        Assert.Equal(0, await f.Db.AuditLogs.CountAsync());
        var permitted = await service.GetByIdAsync(f.Workers[0].Id);
        Assert.True(permitted.IsSuccess);
        Assert.Equal(1, cpf.Reads);
        Assert.Equal(1, await f.Db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task ForeignWritesAndHistoryCannotReachLegacyServices()
    {
        await using var f = await Fixture.Create();
        var workers = new AuthorizedWorkerService(null!, f.Access());
        var history = new AuthorizedWorkerHistoryService(null!, f.Access());
        var id = f.Workers[3].Id;
        await Assert.ThrowsAsync<NotFoundException>(() => workers.UpdateAsync(id, new WorkerUpdateRequest()));
        await Assert.ThrowsAsync<NotFoundException>(() => workers.DeactivateAsync(id));
        await Assert.ThrowsAsync<NotFoundException>(() => history.ListByWorkerAsync(id));
        await Assert.ThrowsAsync<NotFoundException>(() => history.AddManualAsync(id, new WorkerHistoryCreateRequest()));
        Assert.Null(f.Workers[3].DeletedAt);
    }

    [Fact]
    public async Task WorkerCannotMoveToAnUnauthorisedUnitOrMismatchedSector()
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<ResourceForbiddenException>(() => f.Access().ValidateWorkerContextAsync(
            "editar", f.CompanyA.Id, f.Units[1].Id, f.Sectors[2].Id, null, default));
        await Assert.ThrowsAsync<ResourceForbiddenException>(() => f.Access().ValidateWorkerContextAsync(
            "editar", f.CompanyA.Id, f.Units[0].Id, f.Sectors[2].Id, null, default));
    }

    [Fact]
    public async Task AGrantInOneCompanyDoesNotAuthoriseAnotherCompanyScope()
    {
        await using var f = await Fixture.Create("EMPRESA");
        var extra = new UserScope(f.Actor.Id, "EMPRESA"); extra.Update(f.Actor.Id, "EMPRESA", f.CompanyB.Id);
        f.Db.Add(extra); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ResourceForbiddenException>(() => new AuthorizedWorkerService(null!, f.Access()).ListAsync(f.CompanyB.Id));
    }

    [Fact]
    public async Task OwnOnlyCannotReadColleaguesOrWriteEvenWithAnExtraActionGrant()
    {
        await using var f = await Fixture.Create("EMPRESA", roleCode: "TRABALHADOR");
        var access = f.Access();
        var rows = await (await access.WorkersAsync("ler", default)).ToListAsync();
        Assert.Equal(f.Workers[0].Id, Assert.Single(rows).Id);
        await Assert.ThrowsAsync<NotFoundException>(() => access.WorkerAsync(f.Workers[1].Id, "ler", default));
        await Assert.ThrowsAsync<ResourceForbiddenException>(() => access.WorkersAsync("editar", default));
    }

    [Theory]
    [InlineData("empresa:editar")]
    [InlineData("empresa:excluir")]
    [InlineData("unidade:criar")]
    [InlineData("cargo:editar")]
    public async Task PartialScopeDoesNotPermitCompanyWideMutations(string permission)
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<ResourceForbiddenException>(() => f.Access().RequireAsync(permission, f.CompanyA.Id, true, default));
    }

    [Fact]
    public async Task OrganisationQueriesFilterBeforePagingAndSectorDoesNotExpandToUnit()
    {
        await using var f = await Fixture.Create("SETOR");
        var units = new AuthorizedUnitService(null!, f.Access(), f.Db);
        Assert.Equal(f.Units[0].Id, Assert.Single((await units.ListAsync()).Value!).Id);
        var sectors = new AuthorizedDepartmentService(null!, f.Access(), f.Db);
        Assert.Equal(f.Sectors[0].Id, Assert.Single((await sectors.ListByUnitAsync(f.Units[0].Id)).Value!).Id);
        await Assert.ThrowsAsync<ResourceForbiddenException>(() => sectors.GetByIdAsync(f.Sectors[1].Id));
        await Assert.ThrowsAsync<ResourceForbiddenException>(() => units.DeactivateAsync(f.Units[0].Id));
        await Assert.ThrowsAsync<ResourceForbiddenException>(() => new AuthorizedCompanyService(null!, f.Access(), f.Db).GetByIdAsync(f.CompanyB.Id));
        await Assert.ThrowsAsync<ResourceForbiddenException>(() => new AuthorizedPositionService(null!, f.Access(), f.Db).GetByIdAsync(f.ForeignPosition.Id));
    }

    [Fact]
    public async Task TeamScopeRejectsForeignUnitsAndOrganisationIdIsNotALocation()
    {
        await using var f = await Fixture.Create();
        var service = new AuthorizedTeamService(null!, f.Access());
        Assert.Single((await service.ListByLocationAsync(f.Units[0].Id)).Value!);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(f.ForeignTeam.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ListByLocationAsync(f.CompanyA.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeactivateAsync(f.ForeignTeam.Id));
    }

    [Fact]
    public async Task InternalCompanyTeamsRejectMissingForeignAndInactiveParticipants()
    {
        await using var f = await Fixture.Create();
        Assert.Equal(CompanyType.INTERNA, f.CompanyA.TipoEmpresa);
        foreach (var id in new[] { Guid.NewGuid(), f.Workers[3].Id, f.Workers[2].Id })
            await Assert.ThrowsAsync<ArgumentException>(() => f.Access().ValidateTeamAsync("criar", f.CompanyA.Id,
                f.Units[0].Id, f.Workers[0].Id, [id], default));
        f.Workers[1].Deactivate(); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => f.Access().ValidateTeamAsync("editar", f.CompanyA.Id,
            f.Units[0].Id, f.Workers[0].Id, [f.Workers[1].Id], default));
    }

    [Fact]
    public async Task InactiveParentsAndRevokedRolesFailClosed()
    {
        await using var f = await Fixture.Create();
        f.Units[0].Deactivate(); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ResourceForbiddenException>(() => f.Access().WorkerAsync(f.Workers[0].Id, "ler", default));
        f.Role.MarkAsDeleted(); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ResourceForbiddenException>(() => f.Access().WorkersAsync("ler", default));
    }

    [Fact]
    public async Task AccountCannotBeLinkedAcrossCompaniesOrToTwoWorkers()
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<ArgumentException>(() => f.Access().ValidateAccountAsync(f.Actor.Id, f.CompanyB.Id, null, default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Access().ValidateAccountAsync(f.Actor.Id, f.CompanyA.Id, null, default));
        await f.Access().ValidateAccountAsync(f.Actor.Id, f.CompanyA.Id, f.Workers[0].Id, default);
    }

    [Fact]
    public async Task MeDropsDeletedRolePermissionsAndDoesNotPromoteCompanyAdmin()
    {
        await using var f = await Fixture.Create(roleCode: "ADMIN");
        var before = await f.Identity();
        // An ancestor company ID enables dependent selectors, without granting full-company access.
        Assert.Equal(f.CompanyA.Id, Assert.Single(before.GetProperty("scope").GetProperty("companyIds").EnumerateArray()).GetGuid());
        Assert.Single(before.GetProperty("scope").GetProperty("unitIds").EnumerateArray());
        Assert.NotEmpty(before.GetProperty("permissions").EnumerateArray());
        f.Role.MarkAsDeleted(); await f.Db.SaveChangesAsync();
        var after = await f.Identity();
        Assert.Empty(after.GetProperty("roles").EnumerateArray());
        Assert.Empty(after.GetProperty("permissions").EnumerateArray());
    }

    [Fact]
    public async Task ExplicitGlobalAdminGetsOnlyActiveHierarchyAndCanBootstrapACompany()
    {
        await using var f = await Fixture.Create(roleCode: "ADMIN", global: true);
        f.CompanyB.Deactivate(); await f.Db.SaveChangesAsync();
        var identity = await f.Identity();
        Assert.Equal(f.CompanyA.Id, Assert.Single(identity.GetProperty("scope").GetProperty("companyIds").EnumerateArray()).GetGuid());
        Assert.DoesNotContain(identity.GetProperty("scope").GetProperty("unitIds").EnumerateArray(), u => u.GetGuid() == f.Units[2].Id);
        await f.Access().RequireGlobalCompanyCreationAsync(default);
    }

    [Theory]
    [InlineData("EMPRESA", 2, 3)]
    [InlineData("UNIDADE", 1, 2)]
    [InlineData("SETOR", 1, 1)]
    public async Task IdentityScopeMatchesAuthorizedHierarchyForDependentSelectors(string scope, int unitCount, int sectorCount)
    {
        await using var f = await Fixture.Create(scope);
        var identity = await f.Identity(); var actual = identity.GetProperty("scope");
        Assert.Equal(f.CompanyA.Id, Assert.Single(actual.GetProperty("companyIds").EnumerateArray()).GetGuid());
        Assert.Equal(unitCount, actual.GetProperty("unitIds").GetArrayLength());
        Assert.Equal(sectorCount, actual.GetProperty("sectorIds").GetArrayLength());
        Assert.DoesNotContain(actual.GetProperty("unitIds").EnumerateArray(), unit => unit.GetGuid() == f.Units[2].Id);
        Assert.False(actual.GetProperty("ownOnly").GetBoolean());
    }

    [Fact]
    public async Task WorkerIdentityKeepsOnlyItsOwnLocationEvenWithACompanyScope()
    {
        await using var f = await Fixture.Create("EMPRESA", "TRABALHADOR");
        var identity = await f.Identity(); var actual = identity.GetProperty("scope");
        Assert.True(actual.GetProperty("ownOnly").GetBoolean());
        Assert.Equal(f.Units[0].Id, Assert.Single(actual.GetProperty("unitIds").EnumerateArray()).GetGuid());
        Assert.Equal(f.Sectors[0].Id, Assert.Single(actual.GetProperty("sectorIds").EnumerateArray()).GetGuid());
    }

    [Fact]
    public async Task CompanyAdminCannotCreateAGlobalCompany()
    {
        await using var f = await Fixture.Create("EMPRESA", "ADMIN");
        await Assert.ThrowsAsync<ResourceForbiddenException>(() => f.Access().RequireGlobalCompanyCreationAsync(default));
    }

    [Fact]
    public void RegistrationReplacesAllNativeControllerInterfaces()
    {
        var services = new ServiceCollection().AddApplication().AddNativeResourceAuthorization();
        foreach (var type in new[] { typeof(IWorkerService), typeof(IWorkerHistoryService), typeof(ICompanyService), typeof(IUnitService), typeof(IDepartmentService), typeof(IPositionService), typeof(ITeamService) })
            Assert.StartsWith("Authorized", services.Last(d => d.ServiceType == type).ImplementationType!.Name);
    }

    [Theory]
    [InlineData("roles")]
    [InlineData("scopes")]
    [InlineData("deactivate")]
    public async Task CompanyAdminCannotReplaceOrDeactivateAMultiCompanyAccount(string operation)
    {
        await using var f = await Fixture.Create("EMPRESA", "ADMIN");
        var target = new User("Pessoa Compartilhada", "shared@example.invalid", "not-a-login-hash");
        f.Db.Add(target);
        foreach (var company in new[] { f.CompanyA.Id, f.CompanyB.Id })
        {
            var scope = new UserScope(target.Id, "EMPRESA"); scope.Update(target.Id, "EMPRESA", company); f.Db.Add(scope);
        }
        foreach (var action in new[] { "editar", "excluir" })
        {
            var permission = new Permission("usuario", action, "Permissão de teste");
            f.Db.Add(permission); f.Db.Add(new RolePermission(f.Role.Id, permission.Id));
        }
        await f.Db.SaveChangesAsync();
        var controller = new UsersController(f.Db, null!) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        controller.HttpContext.User = new(new ClaimsIdentity([new Claim("sub", f.Actor.Id.ToString())], "Tests"));
        var result = operation switch
        {
            "roles" => await controller.SetRoles(target.Id, new(["ADMIN"]), default),
            "scopes" => await controller.SetScopes(target.Id, new([f.CompanyA.Id]), default),
            _ => await controller.Deactivate(target.Id, default)
        };
        Assert.Equal(404, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.True(target.Active);
        Assert.Equal(2, await f.Db.UserScopes.CountAsync(s => s.UserId == target.Id && s.DeletedAt == null));
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        public SesmtDbContext Db { get; } = new(new DbContextOptionsBuilder<SesmtDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public User Actor { get; } = new("Pessoa fictícia", "actor@example.invalid", "not-a-login-hash");
        public Company CompanyA { get; } = new("Empresa A", "A", "11222333000181", null, null, CompanyType.INTERNA);
        public Company CompanyB { get; } = new("Empresa B", "B", "11444777000161", null, null, CompanyType.INTERNA);
        public Role Role { get; private set; } = null!;
        public OrganizationUnit[] Units { get; private set; } = [];
        public Department[] Sectors { get; private set; } = [];
        public Worker[] Workers { get; private set; } = [];
        public Position ForeignPosition { get; private set; } = null!;
        public Team ForeignTeam { get; private set; } = null!;
        public CurrentActor Current => new(Actor.Id);
        public NativeResourceAccess Access() => new(Db, Current, new BrowserWorkspaceAccessReader(new UserAuthenticationRepository(Db), Db));

        public static async Task<Fixture> Create(string scope = "UNIDADE", string roleCode = "GESTOR_LOCAL", bool global = false)
        {
            var f = new Fixture();
            f.Role = new(roleCode, "Perfil de teste", "Perfil fictício");
            f.Units = Enumerable.Range(0, 3).Select(i => new OrganizationUnit(i == 2 ? f.CompanyB.Id : f.CompanyA.Id,
                "U" + i, "Unidade " + i, "MATRIZ", "01000000", "Rua fictícia", "1", null, "Bairro", "Cidade", "SP")).ToArray();
            f.Sectors = new[] { new Department(f.Units[0].Id, "S0", null, null), new Department(f.Units[0].Id, "S1", null, null),
                new Department(f.Units[1].Id, "S2", null, null), new Department(f.Units[2].Id, "S3", null, null) };
            f.Workers = Enumerable.Range(0, 4).Select(i => new Worker(i == 3 ? f.CompanyB.Id : f.CompanyA.Id,
                "M" + i, [1], new string('a', 64), new[] { "Ana Araujo", "Beatriz Barros", "Carla Costa", "Dora Dias" }[i], new(1990, 1, 1), new(2020, 1, 1), null, null,
                f.Sectors[i].UnitId, f.Sectors[i].Id, i == 0 ? f.Actor.Id : null)).ToArray();
            f.ForeignPosition = new(f.CompanyB.Id, "Cargo B", null, null);
            f.ForeignTeam = new(f.CompanyB.Id, f.CompanyB.Id, f.Units[2].Id, "Equipe B", null, f.Workers[3].Id);
            f.Db.AddRange(f.Actor, f.CompanyA, f.CompanyB, f.Role, f.ForeignPosition, f.ForeignTeam);
            f.Db.AddRange(f.Units); f.Db.AddRange(f.Sectors); f.Db.AddRange(f.Workers);
            f.Db.Add(new Team(f.CompanyA.Id, f.CompanyA.Id, f.Units[0].Id, "Equipe A", null, f.Workers[0].Id));
            f.Db.Add(new UserRole(f.Actor.Id, f.Role.Id, global ? null : f.CompanyA.Id));
            if (!global)
            {
                var link = new UserScope(f.Actor.Id, scope);
                link.Update(f.Actor.Id, scope, scope == "EMPRESA" ? f.CompanyA.Id : scope == "SETOR" ? f.Sectors[0].Id : f.Units[0].Id);
                f.Db.Add(link);
            }
            foreach (var resource in new[] { "empresa", "unidade", "setor", "cargo", "equipe", "trabalhador" })
                foreach (var action in new[] { "ler", "criar", "editar", "excluir" })
                {
                    var permission = new Permission(resource, action, "Permissão fictícia");
                    f.Db.Add(permission); f.Db.Add(new RolePermission(f.Role.Id, permission.Id));
                }
            await f.Db.SaveChangesAsync(); return f;
        }

        public async Task<JsonElement> Identity()
        {
            var controller = new AuthController(null!, null!, null!, null!, new UserAuthenticationRepository(Db), Db)
            {
                ControllerContext = new() { HttpContext = new DefaultHttpContext() }
            };
            controller.HttpContext.User = new(new ClaimsIdentity([new Claim("sub", Actor.Id.ToString())], "Tests"));
            return JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await controller.Me(default)).Value);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    internal sealed record CurrentActor(Guid Actor) : ICurrentUserService
    {
        public Guid? UserId => Actor;
        public string? Email => "actor@example.invalid";
        public string? Name => "Pessoa fictícia";
        public string? IpAddress => null;
        public string? UserAgent => null;
        public string? TraceId => "native-authorization-test";
        public bool IsAuthenticated => true;
    }

    private sealed class CpfSpy : ICpfProtectionService
    {
        public int Reads { get; private set; }
        public (byte[] Encrypted, string Hash) Protect(string rawCpf) => throw new NotSupportedException();
        public string Hash(string rawCpf) => throw new NotSupportedException();
        public string Unprotect(byte[] encrypted) { Reads++; return "52998224725"; }
    }
}
