using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Infrastructure.Persistence.Repositories.Documents;
using SESMT.Infrastructure.Persistence.Repositories.WorkPermits;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class DocumentSignaturePostgresTests(PostgresDatabaseFixture database)
{
    private async Task<(Guid DocumentId, Guid VersionId)> EvidenceAsync(bool required)
    {
        await using var db = database.CreateContext();
        var type = new DocumentType("SIG-" + Guid.NewGuid().ToString("N"), "Evidência fictícia", null, "QA", SecrecyLevel.Internal, required, true, null, null, "{}", 1);
        var document = new Document(type.Id, database.CompanyA, database.UnitA, null, "SIG-" + Guid.NewGuid().ToString("N"), "Teste de assinatura", null,
            SecrecyLevel.Internal, DateOnly.FromDateTime(DateTime.UtcNow), null, null, database.ActorA);
        var file = new StoredFile("local", "QA", "test-object", "qa.pdf", "application/pdf", 10, new string('a', 64), false, "CLEAN");
        var version = new DocumentVersion(document.Id, 1, file.Id, file.FileHash, database.ActorA, null);
        version.SubmitForReview(); document.RegisterVersion(1, database.ActorA);
        db.AddRange(type, document, file, version);
        await db.SaveChangesAsync(); return (document.Id, version.Id);
    }

    [PostgresFact]
    public async Task WorkflowRejectsRequiredSignatureEvenWhenCalledWithoutTheService()
    {
        var ids = await EvidenceAsync(true);
        await using var db = database.CreateContext();
        var document = await db.Documents.SingleAsync(d => d.Id == ids.DocumentId);
        var version = await db.DocumentVersions.SingleAsync(v => v.Id == ids.VersionId);
        var failure = await Assert.ThrowsAsync<DocumentOperationException>(() => new DocumentVersionWorkflow(db).ReviewAsync(document, version, database.ActorA, true, null, default));
        Assert.Equal(422, failure.Status);
        await using var verify = database.CreateContext();
        Assert.False((await verify.DocumentVersions.SingleAsync(v => v.Id == ids.VersionId)).IsCurrent);
        Assert.Equal(DocumentStatus.InReview, (await verify.Documents.SingleAsync(d => d.Id == ids.DocumentId)).Status);
    }

    [PostgresFact]
    public async Task ReadinessRejectsPreviouslyPublishedEvidenceWhenTypeRequiresSignature()
    {
        var ids = await EvidenceAsync(true);
        await using var db = database.CreateContext();
        var document = await db.Documents.SingleAsync(d => d.Id == ids.DocumentId);
        var version = await db.DocumentVersions.SingleAsync(v => v.Id == ids.VersionId);
        // Model an old published record created before signature enforcement.
        version.Approve(database.ActorA); version.Publish(); document.RecordReview(true, database.ActorA);
        var appointment = new WorkPermitAppointment(database.CompanyA, "Agenda isolada de QA", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(2), database.ActorA);
        var permit = new WorkPermit(database.CompanyA, appointment.Id, "MANUTENCAO", "PT isolada de teste", database.ActorA);
        permit.AddParticipant(new WorkPermitParticipant(permit.Id, database.ActorA, "Participante", database.ActorA), database.ActorA);
        db.AddRange(appointment, permit, new DocumentLink(document.Id, LinkedEntityType.Worker, database.WorkerA),
            new DocumentLink(document.Id, LinkedEntityType.WorkPermit, permit.Id),
            new WorkPermitDocumentRequirement(database.CompanyA, "MANUTENCAO", document.DocumentTypeId, "QA", database.ActorA));
        await db.SaveChangesAsync();
        var result = await new WorkPermitRepository(db).EvaluateReadinessAsync(permit);
        Assert.False(result.Ready); Assert.Contains(result.Issues, issue => issue.Code == "ASSINATURA_PENDENTE" && issue.DocumentTypeId == document.DocumentTypeId && issue.ParticipantUserId == database.ActorA);
    }
}
