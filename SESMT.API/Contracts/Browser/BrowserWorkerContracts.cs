namespace SESMT.API.Contracts.Browser;

public sealed record BrowserWorkerResponse(
    Guid Id, string Registration, string FullName, Guid CompanyId, Guid? UnitId,
    Guid? SectorId, Guid? JobId, DateOnly AdmissionDate, string Status)
{
    public bool SourceReadOnly => true;
    public string? EmploymentType => null;
    public long? Revision => null;
}

public sealed record BrowserWorkerAssignment(Guid UnitId, Guid SectorId, Guid JobId);

// Native history stores assignment periods, not operational admission/transfer commands.
public sealed record BrowserWorkerMovementResponse(
    Guid Id, Guid WorkerId, string Type, DateOnly EffectiveDate,
    BrowserWorkerAssignment? PreviousAssignment, BrowserWorkerAssignment NewAssignment,
    string? Reason, DateTime CreatedAt, Guid? CreatedBy);

public sealed record BrowserWorkerPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
