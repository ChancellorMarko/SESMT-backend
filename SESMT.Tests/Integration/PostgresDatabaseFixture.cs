using Microsoft.EntityFrameworkCore;
using Npgsql;
using SESMT.Domain.Modules.IdentityAccess.Constants;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.Organization.Enums;
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Domain.Modules.Workforce.Entities;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Application.Modules.WorkPermits.Constants;
using SESMT.Infrastructure.Persistence;

namespace SESMT.Tests.Integration;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresCollection : ICollectionFixture<PostgresDatabaseFixture>
{
    public const string Name = "Isolated PostgreSQL";
}

/// <summary>Creates and drops only a uniquely named database; never reuses the application's database.</summary>
public sealed class PostgresDatabaseFixture : IAsyncLifetime
{
    private string? _adminConnection;
    private string? _databaseName;
    private bool _created;
    public string ConnectionString { get; private set; } = "";
    public Guid ActorA { get; private set; }
    public Guid ActorB { get; private set; }
    public Guid CompanyA { get; private set; }
    public Guid CompanyB { get; private set; }
    public Guid PermitA { get; private set; }
    public Guid PermitB { get; private set; }
    public Guid UnitA { get; private set; }
    public Guid UnitB { get; private set; }
    public Guid WorkerA { get; private set; }
    public Guid WorkerB { get; private set; }
    public Guid TeamA { get; private set; }
    public Guid TeamB { get; private set; }
    public Guid DocumentA { get; private set; }
    public Guid DocumentB { get; private set; }

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(PostgresFactAttribute.ConnectionVariable);
        if (string.IsNullOrWhiteSpace(configured)) return;
        var admin = new NpgsqlConnectionStringBuilder(configured);
        if (admin.Database != "postgres")
            throw new InvalidOperationException("The integration admin connection must target the postgres maintenance database.");
        if (admin.Host is not ("localhost" or "127.0.0.1" or "::1"))
            throw new InvalidOperationException("Integration tests require an explicit loopback PostgreSQL connection.");
        admin.IncludeErrorDetail = false;
        _adminConnection = admin.ConnectionString;
        _databaseName = $"sesmt_it_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
        await create.ExecuteNonQueryAsync();
        _created = true;
        var isolated = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = _databaseName };
        ConnectionString = isolated.ConnectionString;
        try
        {
            await using var db = CreateContext();
            await db.Database.MigrateAsync();
            await ValidateMigratedSchemaAsync(db);
            await SeedScenarioAsync(db);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public SesmtDbContext CreateContext() => new(new DbContextOptionsBuilder<SesmtDbContext>()
        .UseNpgsql(ConnectionString).Options);

    private async Task ValidateMigratedSchemaAsync(SesmtDbContext db)
    {
        var tables = db.Model.GetRelationalModel().Tables.ToArray();
        var schemas = tables.Select(table => table.Schema ?? "public").Distinct().ToArray();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var query = new NpgsqlCommand("SELECT table_schema, table_name, column_name FROM information_schema.columns WHERE table_schema = ANY(@schemas)", connection);
        query.Parameters.AddWithValue("schemas", schemas);
        var actual = new HashSet<string>(StringComparer.Ordinal);
        await using (var reader = await query.ExecuteReaderAsync())
            while (await reader.ReadAsync()) actual.Add($"{reader.GetString(0)}.{reader.GetString(1)}.{reader.GetString(2)}");
        var missing = tables.SelectMany(table => table.Columns.Select(column => $"{table.Schema ?? "public"}.{table.Name}.{column.Name}"))
            .Where(column => !actual.Contains(column)).Order().ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException($"Migrated PostgreSQL schema is missing mapped columns: {string.Join(", ", missing)}.");
    }

    private async Task SeedScenarioAsync(SesmtDbContext db)
    {
        var companies = new[]
        {
            new Company("Prestadora fictícia A", "Teste A", "11222333000181", null, null, CompanyType.PRESTADORA),
            new Company("Prestadora fictícia B", "Teste B", "11444777000161", null, null, CompanyType.PRESTADORA),
        };
        var actors = new[]
        {
            new User("Operador fictício A", "operator-a@example.invalid", "not-a-login-hash"),
            new User("Operador fictício B", "operator-b@example.invalid", "not-a-login-hash"),
        };
        var requester = new Role(WorkPermitRoleCodes.Requester, "Solicitante de teste", "Somente para os testes isolados");
        var configurator = new Role(WorkPermitRoleCodes.Configurator, "Configurador de teste", "Somente para os testes isolados");
        var documentReader = new Role("QA_DOCUMENT_EDITOR", "Documentos de teste", "Somente para os testes isolados");
        db.AddRange(companies); db.AddRange(actors); db.AddRange(requester, configurator, documentReader);
        foreach (var action in new[] { "ler", "criar", "editar" })
        {
            var permission = new Permission("documento", action, "Permissão de teste isolado");
            db.Add(permission); db.Add(new RolePermission(documentReader.Id, permission.Id));
        }
        for (var index = 0; index < actors.Length; index++)
        {
            var scope = new UserScope(actors[index].Id, ScopeType.EMPRESA);
            scope.Update(actors[index].Id, ScopeType.EMPRESA, companies[index].Id);
            db.Add(scope); db.Add(new UserRole(actors[index].Id, requester.Id, companies[index].Id));
            db.Add(new UserRole(actors[index].Id, documentReader.Id, companies[index].Id));
        }
        db.Add(new UserRole(actors[0].Id, configurator.Id, companies[0].Id));
        var units = companies.Select((company, index) => new SESMT.Domain.Modules.Organization.Entities.Unit(company.Id, $"QA-{index}", $"Unidade fictícia {index}",
            "MATRIZ", "01000000", "Rua fictícia", "1", null, "Bairro de teste", "Cidade de teste", "SP")).ToArray();
        db.AddRange(units);
        var workers = companies.Select((company, index) => new Worker(company.Id, $"QA-{index}", [1, 2, 3],
            new string(index == 0 ? 'A' : 'B', 64), index == 0 ? "Participante fictício A" : "Participante fictício B", new DateOnly(1990, 1, 1),
            new DateOnly(2020, 1, 1), null, null, units[index].Id, null, actors[index].Id)).ToArray();
        db.AddRange(workers);
        var teams = companies.Select((company, index) => new Team(company.Id, company.Id, units[index].Id,
            $"Equipe fictícia {index}", null, workers[index].Id, [])).ToArray();
        db.AddRange(teams);
        db.Add(new OperationalDesignation(companies[0].Id, workers[0].Id,
            WorkPermitServiceCatalogEntry.All.Select(service => service.Id), [units[0].Id], "atender",
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7), "Fictícia para teste isolado", actors[0].Id));
        var documentType = new DocumentType("QA-EVIDENCE", "Evidência fictícia", null, "administrativo", SecrecyLevel.Internal,
            false, true, null, null, "{}", 1);
        db.Add(documentType);
        var documents = companies.Select((company, index) => new Document(documentType.Id, company.Id, units[index].Id, null,
            $"QA-DOC-{index}", $"Documento fictício {index}", null, SecrecyLevel.Internal, DateOnly.FromDateTime(DateTime.UtcNow),
            null, null, actors[index].Id)).ToArray();
        db.AddRange(documents);
        var appointments = companies.Select((company, index) => new WorkPermitAppointment(company.Id,
            $"Atendimento fictício {index}", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(2), actors[index].Id)).ToArray();
        db.AddRange(appointments);
        var permits = companies.Select((company, index) => new WorkPermit(company.Id, appointments[index].Id,
            "MANUTENCAO", $"PT fictícia {index}", actors[index].Id)).ToArray();
        db.AddRange(permits);
        await db.SaveChangesAsync();
        CompanyA = companies[0].Id; CompanyB = companies[1].Id;
        ActorA = actors[0].Id; ActorB = actors[1].Id;
        PermitA = permits[0].Id; PermitB = permits[1].Id;
        UnitA = units[0].Id; UnitB = units[1].Id;
        WorkerA = workers[0].Id; WorkerB = workers[1].Id;
        TeamA = teams[0].Id; TeamB = teams[1].Id;
        DocumentA = documents[0].Id; DocumentB = documents[1].Id;
    }

    public async Task DisposeAsync()
    {
        if (!_created || _databaseName is null || _adminConnection is null) return;
        var database = _databaseName;
        if (!System.Text.RegularExpressions.Regex.IsMatch(database, "^sesmt_it_[a-f0-9]{32}$"))
            throw new InvalidOperationException("Refusing to drop a database outside the allocated integration namespace.");
        await using (var isolatedPool = new NpgsqlConnection(ConnectionString)) NpgsqlConnection.ClearPool(isolatedPool);
        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
        _databaseName = null;
        _created = false;
    }
}
