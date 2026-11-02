using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SESMT.API.Contracts.Users;
using SESMT.API.Controllers;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using Fixture = SESMT.Tests.Unit.Identity.NativeResourceAuthorizationTests.Fixture;

namespace SESMT.Tests.Unit.Identity;

public sealed class UserManagementContextTests
{
    private static UsersController Controller(Fixture f)
    {
        var controller = new UsersController(f.Db, null!) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        controller.HttpContext.User = new(new ClaimsIdentity([new Claim("sub", f.Actor.Id.ToString())], "Tests"));
        return controller;
    }
    private static async Task Grant(Fixture f, params string[] actions)
    {
        foreach (var action in actions)
        {
            var permission = new Permission("usuario", action, "Permissão fictícia");
            f.Db.AddRange(permission, new RolePermission(f.Role.Id, permission.Id));
        }
        await f.Db.SaveChangesAsync();
    }
    private static async Task<User> Target(Fixture f, params Guid[] companies)
    {
        var user = new User("Conta de teste", $"{Guid.NewGuid():N}@example.invalid", "not-a-login-hash"); f.Db.Add(user);
        foreach (var company in companies)
        {
            var scope = new UserScope(user.Id, "EMPRESA"); scope.Update(user.Id, "EMPRESA", company); f.Db.Add(scope);
            f.Db.Add(new UserRole(user.Id, f.Role.Id, company));
        }
        await f.Db.SaveChangesAsync(); return user;
    }

    [Fact]
    public async Task CompanyCatalogContainsOnlyDelegableActiveHierarchyAndDynamicRoles()
    {
        await using var f = await Fixture.Create("EMPRESA", "ADMIN"); await Grant(f, "criar");
        var custom = new Role("custom_case", "Perfil personalizado", "Sem catálogo fixo no frontend"); f.Db.Add(custom);
        f.Sectors[0].Deactivate(); await f.Db.SaveChangesAsync();
        var result = Assert.IsType<UserManagementContext>(Assert.IsType<OkObjectResult>(await Controller(f).ManagementContext("criar", null, default)).Value);
        Assert.Null(result.Target); Assert.Null(result.AccessVersion);
        Assert.All(result.Scopes, scope => Assert.Equal(f.CompanyA.Id, scope.CompanyId));
        Assert.DoesNotContain(result.Scopes, scope => scope.Id == f.Sectors[0].Id);
        Assert.Contains(result.Roles, role => role.Code == custom.Code);
        Assert.Contains(result.Scopes, scope => scope.Id == f.Units[0].Id);
    }

    [Fact]
    public async Task AGlobalCustomAssignmentDoesNotBecomeAdminBecauseAPermissionExistsInTheCatalog()
    {
        await using var f = await Fixture.Create("EMPRESA", "CUSTOM", global: true); await Grant(f, "criar", "ler");
        f.Db.Add(new Permission("perfil", "ler", "Catálogo não é concessão")); await f.Db.SaveChangesAsync();
        Assert.Equal(403, Assert.IsType<ObjectResult>(await Controller(f).ManagementContext("criar", null, default)).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await Controller(f).List(default)).StatusCode);
    }

    [Fact]
    public async Task GlobalAdminNeedsTheRequestedEffectiveActionAndAnActiveAccount()
    {
        await using var f = await Fixture.Create("EMPRESA", "ADMIN", global: true);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await Controller(f).ManagementContext("criar", null, default)).StatusCode);
        await Grant(f, "criar");
        Assert.IsType<OkObjectResult>(await Controller(f).ManagementContext("criar", null, default));
        f.Actor.Deactivate(f.Actor.Id); await f.Db.SaveChangesAsync();
        Assert.Equal(403, Assert.IsType<ObjectResult>(await Controller(f).ManagementContext("criar", null, default)).StatusCode);
    }

    [Fact]
    public async Task ACompanyAdminCannotReadManagementDetailsForAMultiCompanyTarget()
    {
        await using var f = await Fixture.Create("EMPRESA", "ADMIN"); await Grant(f, "editar");
        var target = await Target(f, f.CompanyA.Id, f.CompanyB.Id);
        Assert.Equal(404, Assert.IsType<ObjectResult>(await Controller(f).ManagementContext("editar", target.Id, default)).StatusCode);
    }

    [Fact]
    public async Task AccessIsReplacedAtomicallyWithAuditAndAStaleVersionCannotOverwriteIt()
    {
        await using var f = await Fixture.Create("EMPRESA", "ADMIN"); await Grant(f, "editar");
        var target = await Target(f, f.CompanyA.Id);
        var custom = new Role("custom_case", "Perfil personalizado", "Dinâmico e ativo"); f.Db.Add(custom); await f.Db.SaveChangesAsync();
        var controller = Controller(f);
        var context = Assert.IsType<UserManagementContext>(Assert.IsType<OkObjectResult>(await controller.ManagementContext("editar", target.Id, default)).Value);
        var command = new UserAccessRequest([custom.Code], [f.Units[0].Id], context.AccessVersion!, true);
        var saved = Assert.IsType<UserAccessResponse>(Assert.IsType<OkObjectResult>(await controller.SetAccess(target.Id, command, default)).Value);
        Assert.Equal([custom.Code], saved.User.Roles); Assert.Equal([f.Units[0].Id], saved.User.ScopeIds); Assert.NotEqual(context.AccessVersion, saved.AccessVersion);
        Assert.Equal(1, await f.Db.AuditLogs.CountAsync(log => log.ResourceId == target.Id));
        Assert.Equal(409, Assert.IsType<ObjectResult>(await controller.SetAccess(target.Id, command, default)).StatusCode);
        Assert.Equal(1, await f.Db.UserRoles.CountAsync(r => r.UserId == target.Id && r.DeletedAt == null));
        Assert.Equal(1, await f.Db.AuditLogs.CountAsync(log => log.ResourceId == target.Id));
    }

    [Fact]
    public async Task ForeignScopeFailsWithoutChangingEitherRolesOrScopes()
    {
        await using var f = await Fixture.Create("EMPRESA", "ADMIN"); await Grant(f, "editar"); var target = await Target(f, f.CompanyA.Id);
        var controller = Controller(f); var context = Assert.IsType<UserManagementContext>(Assert.IsType<OkObjectResult>(await controller.ManagementContext("editar", target.Id, default)).Value);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await controller.SetAccess(target.Id, new([f.Role.Code], [f.CompanyB.Id], context.AccessVersion!, true), default)).StatusCode);
        Assert.Equal(f.CompanyA.Id, (await f.Db.UserScopes.SingleAsync(s => s.UserId == target.Id && s.DeletedAt == null)).ReferenceId);
        Assert.Empty(await f.Db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task SelfAndGlobalAssignmentsAreProtectedFromTheAccessForm()
    {
        await using var f = await Fixture.Create("EMPRESA", "ADMIN", global: true); await Grant(f, "editar");
        Assert.Equal(409, Assert.IsType<ObjectResult>(await Controller(f).ManagementContext("editar", f.Actor.Id, default)).StatusCode);
        var target = await Target(f, f.CompanyA.Id); f.Db.Add(new UserRole(target.Id, f.Role.Id)); await f.Db.SaveChangesAsync();
        Assert.Equal(409, Assert.IsType<ObjectResult>(await Controller(f).SetAccess(target.Id, new([f.Role.Code], [f.CompanyA.Id], new('a', 64), true), default)).StatusCode);
    }
}
