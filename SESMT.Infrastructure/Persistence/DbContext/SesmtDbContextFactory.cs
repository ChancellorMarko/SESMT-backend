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
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Sesmt");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Configure a variável de ambiente ConnectionStrings__Sesmt.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<SesmtDbContext>();

        optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsAssembly(
                typeof(SesmtDbContext).Assembly.FullName));

        return new SesmtDbContext(optionsBuilder.Options);
    }
}