using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SESMT.Infrastructure.Persistence;

/// <summary>
/// Usada apenas pelo `dotnet ef` em tempo de design.
/// A connection string aqui aponta para o Postgres do docker-compose (porta 5433).
/// Em runtime, a API injeta a connection string via DI.
/// </summary>
public class SesmtDbContextFactory : IDesignTimeDbContextFactory<SesmtDbContext>
{
    public SesmtDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SesmtDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5433;Database=sesmt;Username=sesmt;Password=sesmt123",
            npg => npg.MigrationsAssembly(typeof(SesmtDbContext).Assembly.FullName));

        return new SesmtDbContext(optionsBuilder.Options);
    }
}