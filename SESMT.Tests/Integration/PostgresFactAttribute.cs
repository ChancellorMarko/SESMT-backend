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
