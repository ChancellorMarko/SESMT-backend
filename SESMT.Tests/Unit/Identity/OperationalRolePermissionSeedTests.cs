using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SESMT.Application.Common.Interfaces;
using SESMT.Infrastructure.Persistence;
using SESMT.Infrastructure.Persistence.Seeding;

namespace SESMT.Tests.Unit.Identity;

/// <summary>
/// Agenda and designations are authorized by the SESMT_PT_* roles in the API; the seeded permissions must
/// mirror that, so the interface offers the operations to exactly the people the API will accept.
/// </summary>
public sealed class OperationalRolePermissionSeedTests
{
    [Theory]
    [InlineData("SESMT_PT_SOLICITANTE", new[] { "agenda:criar", "agenda:ler" })]
    [InlineData("SESMT_PT_OPERADOR", new[] { "agenda:ler" })]
    [InlineData("SESMT_PT_LIBERADOR", new[] { "agenda:ler" })]
    [InlineData("SESMT_PT_CONFIG", new[] { "agenda:criar", "agenda:editar", "agenda:ler", "designacao:editar", "designacao:ler" })]
    public async Task OperationalRolesReceiveTheAgendaGrantsTheApiAccepts(string role, string[] expected)
    {
        var grants = await SeededGrants();
        Assert.Equal(expected, grants[role].Where(IsOperational).ToArray());
    }

    [Theory]
    [InlineData("GESTOR")]
    [InlineData("LIDER")]
    [InlineData("AUDITOR")]
    [InlineData("TECNICO")]
    [InlineData("TRABALHADOR")]
    public async Task OtherRolesAreNotOfferedOperationsTheApiWouldRefuse(string role)
    {
        var grants = await SeededGrants();
        Assert.DoesNotContain(grants[role], IsOperational);
    }

    [Fact]
    public async Task AdministratorKeepsEveryPermission()
    {
        var grants = await SeededGrants();
        Assert.Contains("agenda:editar", grants["ADMIN"]); Assert.Contains("designacao:editar", grants["ADMIN"]);
    }

    private static bool IsOperational(string grant) => grant.StartsWith("agenda:", StringComparison.Ordinal) || grant.StartsWith("designacao:", StringComparison.Ordinal);

    private static async Task<Dictionary<string, string[]>> SeededGrants()
    {
        var database = $"seed-{Guid.NewGuid():N}";
        var services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<SesmtDbContext>(o => o.UseInMemoryDatabase(database))
            .AddSingleton<IPasswordHashService, UnusedHasher>()
            .Configure<SeedSettings>(s => s.BootstrapAdminEnabled = false)
            .BuildServiceProvider();
        await DatabaseSeeder.SeedAsync(services);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SesmtDbContext>();
        var rows = await (from rp in db.RolePermissions
                          join r in db.Roles on rp.RoleId equals r.Id
                          join p in db.Permissions on rp.PermissionId equals p.Id
                          where rp.DeletedAt == null
                          select new { r.Code, Grant = p.Resource + ":" + p.Action }).ToListAsync();
        var roles = await db.Roles.Select(r => r.Code).ToListAsync();
        return roles.ToDictionary(code => code, code => rows.Where(x => x.Code == code).Select(x => x.Grant).Order(StringComparer.Ordinal).ToArray());
    }

    private sealed class UnusedHasher : IPasswordHashService
    {
        public string HashPassword(string password) => throw new InvalidOperationException();
        public bool VerifyPassword(string passwordHash, string providedPassword) => throw new InvalidOperationException();
    }
}
