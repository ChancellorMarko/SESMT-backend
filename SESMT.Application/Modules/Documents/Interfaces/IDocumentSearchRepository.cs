using System.Linq.Expressions;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Application.Modules.Documents.Interfaces;

public interface IDocumentSearchRepository
{
    Task<DocumentPageResponse> SearchAsync(DocumentSearchRequest request, Expression<Func<Document, bool>> authorized, CancellationToken ct);
}
