namespace SESMT.Tests.Integration;

/// <summary>Integration tests are opt-in; a missing database is reported as skipped.</summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public const string ConnectionVariable = "SESMT_TEST_POSTGRES_ADMIN";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
            Skip = $"Configure {ConnectionVariable} for an isolated PostgreSQL integration run.";
    }
}

/// <summary>Data-driven variant of <see cref="PostgresFactAttribute"/>; skipped the same way without a database.</summary>
public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgresFactAttribute.ConnectionVariable)))
            Skip = $"Configure {PostgresFactAttribute.ConnectionVariable} for an isolated PostgreSQL integration run.";
    }
}
