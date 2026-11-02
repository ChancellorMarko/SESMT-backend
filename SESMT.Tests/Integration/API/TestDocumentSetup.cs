using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Tests.Integration.API;

internal static class TestDocumentSetup
{
    // Each mutating test owns a document; collection fixtures are shared across tests.
    public static async Task<Guid> CreateAsync(PostgresDatabaseFixture database)
    {
        await using var db = database.CreateContext();
        var source = await db.Documents.AsNoTracking().SingleAsync(d => d.Id == database.DocumentA);
        var document = new Document(source.DocumentTypeId, source.CompanyId, source.UnitId, source.DepartmentId,
            "QA-FILE-" + Guid.NewGuid().ToString("N"), "Evidência fictícia isolada", null, source.SecrecyLevel,
            DateOnly.FromDateTime(DateTime.UtcNow), null, null, database.ActorA, [], "{}");
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        return document.Id;
    }
}
