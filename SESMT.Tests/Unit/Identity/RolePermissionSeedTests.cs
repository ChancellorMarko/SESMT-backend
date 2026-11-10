using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SESMT.Application.Common.Interfaces;
using SESMT.Infrastructure.Persistence;
using SESMT.Infrastructure.Persistence.Seeding;

namespace SESMT.Tests.Unit.Identity;

/// <summary>Pins the seeded grants of the operational roles so a merge cannot silently drop a module.</summary>
public sealed class RolePermissionSeedTests
{
    [Fact]
    public async Task WorkerReadsOwnModulesAndNeverWrites()
    {
        var grants = await SeededGrants("TRABALHADOR");
        Assert.Equal(
            new[] { "aso:acompanhar", "documento:ler", "epi:ler", "trabalhador:ler", "treinamento:ler" },
            grants);
    }

    [Fact]
    public async Task TechnicianManagesEquipmentAndTrainingsWithoutDeleting()
    {
        var grants = await SeededGrants("TECNICO");
        Assert.Contains("epi:ler", grants); Assert.Contains("epi:criar", grants); Assert.Contains("epi:editar", grants);
        Assert.Contains("treinamento:ler", grants); Assert.Contains("treinamento:criar", grants); Assert.Contains("treinamento:editar", grants);
        Assert.DoesNotContain(grants, g => g.EndsWith(":excluir", StringComparison.Ordinal));
        Assert.DoesNotContain(grants, g => g.StartsWith("aso:", StringComparison.Ordinal));
    }

    private static async Task<string[]> SeededGrants(string roleCode)
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
        var role = await db.Roles.SingleAsync(r => r.Code == roleCode);
        return await db.RolePermissions
            .Where(rp => rp.RoleId == role.Id && rp.DeletedAt == null)
            .Join(db.Permissions, rp => rp.PermissionId, p => p.Id, (_, p) => p.Resource + ":" + p.Action)
            .OrderBy(g => g)
            .ToArrayAsync();
    }

    private sealed class UnusedHasher : IPasswordHashService
    {
        public string HashPassword(string password) => throw new InvalidOperationException();
        public bool VerifyPassword(string passwordHash, string providedPassword) => throw new InvalidOperationException();
    }
}
