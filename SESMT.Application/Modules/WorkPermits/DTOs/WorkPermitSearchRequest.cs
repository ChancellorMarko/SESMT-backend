using SESMT.Domain.Modules.WorkPermits.Constants;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record WorkPermitSearchRequest(Guid? CompanyId = null, string? Search = null, string? Q = null,
    string? ServiceCode = null, Guid? ParticipantUserId = null, Guid? LocationId = null,
    DateTimeOffset? From = null, DateTimeOffset? To = null, string? Status = null, int Page = 1, int PageSize = 20)
{
    public WorkPermitSearchRequest Validated()
    {
        if (Page < 1 || PageSize is < 1 or > 100 || Page > int.MaxValue / PageSize
            || CompanyId == Guid.Empty || ParticipantUserId == Guid.Empty || LocationId == Guid.Empty
            || From.HasValue && To.HasValue && From >= To)
            throw new ArgumentException("Informe filtros, intervalo e paginação válidos (pageSize de 1 a 100).");
        if (Search is not null && Q is not null && Search.Trim() != Q.Trim())
            throw new ArgumentException("Use search ou q com o mesmo critério.");
        var text = (Search ?? Q ?? "").Trim();
        var code = ServiceCode?.Trim().ToUpperInvariant();
        var status = Status?.Trim().ToUpperInvariant();
        if (text.Length > 200 || text.Any(char.IsControl) || code?.Length > 80 || code?.Contains(',') == true
            || code?.Any(char.IsControl) == true)
            throw new ArgumentException("Os critérios da pesquisa excedem os limites permitidos.");
        if (!string.IsNullOrEmpty(status) && !new[] { WorkPermitStatus.UnderReview, WorkPermitStatus.Released,
            WorkPermitStatus.InProgress, WorkPermitStatus.Suspended, WorkPermitStatus.Completed, WorkPermitStatus.Cancelled }.Contains(status))
            throw new ArgumentException("Situação de PT inválida.");
        return this with { Search = text, Q = null, ServiceCode = code, Status = status, From = From?.ToUniversalTime(), To = To?.ToUniversalTime() };
    }
}

public sealed record WorkPermitSearchResult(IReadOnlyList<WorkPermit> Items, int Total, int Page, int PageSize);
public sealed record WorkPermitPage(IReadOnlyList<WorkPermitResponse> Items, int Total, int Page, int PageSize);
