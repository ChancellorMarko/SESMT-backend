using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.Documents.Interfaces;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Infrastructure.Persistence.Repositories.Documents;

public sealed class DocumentVersionWorkflow(SesmtDbContext db) : IDocumentVersionWorkflow
{
    public async Task ReviewAsync(Document document, DocumentVersion version, Guid actorId, bool accepted, string? reason, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (accepted)
        {
            // Hold the type row until commit: a concurrent change to its signature rule
            // must not slip between checking the requirement and publishing the version.
            var type = await db.DocumentTypes.FromSqlInterpolated($"SELECT * FROM docs.documento_tipo WHERE id = {document.DocumentTypeId} FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(ct);
            if (type is null || !type.Active || type.DeletedAt.HasValue || type.RequiresSignature)
                throw new DocumentOperationException(422, "O tipo documental não permite publicação sem comprovar seus requisitos de assinatura.");
            var current = await db.DocumentVersions.Where(v => v.DocumentId == document.Id && v.IsCurrent && v.DeletedAt == null).ToListAsync(ct);
            foreach (var previous in current) previous.Supersede();
            // Clear the old filtered unique-index entry before publishing a replacement.
            // Both saves, including audit rows, belong to the same transaction.
            await db.SaveChangesAsync(ct);
            version.Approve(actorId); version.Publish();
        }
        else version.Reject(actorId, reason!);
        document.RecordReview(accepted, actorId);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
