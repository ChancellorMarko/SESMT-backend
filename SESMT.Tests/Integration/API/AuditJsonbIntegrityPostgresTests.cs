using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Audit;
using SESMT.Domain.Modules.Audit.Entities;

namespace SESMT.Tests.Integration.API;

/// <summary>Audit hashes must survive jsonb storage, and legacy events must not be reported as tampered.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AuditJsonbIntegrityPostgresTests(PostgresDatabaseFixture database)
{
    private static readonly string[] Samples =
    [
        """{"b": 1, "a": 2}""",
        """{"aa":1,"b":2,"c":{"zz":1,"y":2}}""",
        """{"a":1,"a":2}""",
        """{"nome":"Ação é ç","emoji":"😀","ação":"chave com acento","e":1,"é":2}""",
        """{"s":"linha\nnova\ttab \"aspas\" \\ barra / \u0001 \u001f \u00e9 \u2028"}""",
        """[1,1.0,1.50,-0,-0.0,0.0,1e2,1.5E+1,1e-2,-1.25e-3,0.10e1,123456789012345678901234567890.12,1E+0,2e0]""",
        """{"x":{"z":[],"y":{}},"w":null,"v":true,"u":false}""",
        "\"texto solto\"",
        "42",
        "  { \"espaço\" :  [ 1 , 2 ] }  ",
        JsonSerializer.Serialize(new { Nome = "José da Conceição", Valor = 1.50m, Data = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero), Lista = new[] { "ã", "ü" } }),
    ];
    public static TheoryData<string> Corpus() { var data = new TheoryData<string>(); foreach (var sample in Samples) data.Add(sample); return data; }

    [PostgresTheory]
    [MemberData(nameof(Corpus))]
    public async Task CanonicalTextMatchesPostgresJsonbOutput(string json)
    {
        await using var db = database.CreateContext();
        var expected = await db.Database.SqlQuery<string>($"SELECT CAST(CAST({json} AS jsonb) AS text) AS \"Value\"").SingleAsync();
        Assert.Equal(expected, JsonbText.Canonicalize(json));
        Assert.Equal(expected, JsonbText.Canonicalize(expected));
    }

    [PostgresFact]
    public async Task NewEventsStayVerifiableAfterStorageAndTamperingIsStillDetected()
    {
        var resource = Guid.NewGuid();
        await using (var db = database.CreateContext())
        {
            foreach (var json in Samples)
                db.AuditLogs.Add(new AuditLog("JSONB_CHECK", "teste", resource, database.ActorA, "qa@example.invalid", "QA", json, json, json, null, null, null));
            await db.SaveChangesAsync();
        }
        await using (var db = database.CreateContext())
        {
            var stored = await db.AuditLogs.AsNoTracking().Where(a => a.ResourceId == resource).ToArrayAsync();
            Assert.Equal(Samples.Length, stored.Length);
            Assert.All(stored, log => { Assert.Equal(3, log.HashVersion); Assert.Equal(AuditIntegrity.Valid, log.VerifyIntegrity()); });
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE audit.log_auditoria SET dados_novos = '{{\"alterado\": true}}'::jsonb WHERE id = {stored[0].Id}");
        }
        await using (var db = database.CreateContext())
            Assert.Equal(AuditIntegrity.Invalid, (await db.AuditLogs.AsNoTracking().OrderBy(a => a.Id).FirstAsync(a => a.ResourceId == resource)).VerifyIntegrity());
    }

    [PostgresFact]
    public async Task LegacyEventHashedOverRawJsonIsUnverifiableNotTampered()
    {
        // Rebuild a v2 event the way it was written before: hash over the raw JSON text, which jsonb then normalizes.
        var log = new AuditLog("LEGACY_CHECK", "teste", Guid.NewGuid(), database.ActorA, null, null, null, null, null, null, null, null);
        Set(log, nameof(AuditLog.HashVersion), 2);
        Set(log, nameof(AuditLog.NewData), """{"b":1,"a":"\u00e9"}""");
        Set(log, nameof(AuditLog.CurrentHash), typeof(AuditLog).GetMethod("ComputeHash", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(log, null));
        Assert.Equal(AuditIntegrity.Valid, log.VerifyIntegrity());
        await using (var db = database.CreateContext()) { db.AuditLogs.Add(log); await db.SaveChangesAsync(); }
        await using (var db = database.CreateContext())
        {
            var stored = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EventId == log.EventId);
            Assert.Equal("""{"a": "é", "b": 1}""", stored.NewData);
            Assert.Equal(AuditIntegrity.LegacyUnverifiable, stored.VerifyIntegrity());
        }
    }

    private static void Set(AuditLog log, string property, object? value) =>
        typeof(AuditLog).GetProperty(property)!.SetValue(log, value);
}
