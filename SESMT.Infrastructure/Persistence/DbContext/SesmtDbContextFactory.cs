using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SESMT.Infrastructure.Persistence;

/// <summary>
/// Usada apenas pelo `dotnet ef` em tempo de design.
/// Em runtime, a API injeta a connection string via DI (Program.cs).
/// </summary>
public class SesmtDbContextFactory : IDesignTimeDbContextFactory<SesmtDbContext>
{
    public SesmtDbContext CreateDbContext(string[] args)
    {
        // Prioriza env var (CI, dev com .env, produção de migração).
        // Se ausente, cai no placeholder do docker-compose local — migrations add
        // não abre conexão, então o valor é irrelevante para o comando.
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Sesmt")
            ?? "Host=localhost;Port=5433;Database=sesmt;Username=sesmt;Password=sesmt123";

        var optionsBuilder = new DbContextOptionsBuilder<SesmtDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsAssembly(
                typeof(SesmtDbContext).Assembly.FullName));

        return new SesmtDbContext(optionsBuilder.Options);
    }
}
