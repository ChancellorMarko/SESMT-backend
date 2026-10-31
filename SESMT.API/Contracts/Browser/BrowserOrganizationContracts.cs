namespace SESMT.API.Contracts.Browser;

public sealed record BrowserOrganizationItem(
    Guid Id, string Name, string Status, long? Revision = null,
    string? Code = null, string? TaxId = null, string? Classification = null,
    Guid? CompanyId = null, Guid? UnitId = null, string? Description = null,
    string? OccupationCode = null, IReadOnlyCollection<Guid>? ResponsibleIds = null,
    bool SourceReadOnly = true);

public sealed record BrowserOrganizationResponse(
    IReadOnlyCollection<BrowserOrganizationItem> Companies,
    IReadOnlyCollection<BrowserOrganizationItem> Establishments,
    IReadOnlyCollection<BrowserOrganizationItem> Units,
    IReadOnlyCollection<BrowserOrganizationItem> Sectors,
    IReadOnlyCollection<BrowserOrganizationItem> Jobs,
    IReadOnlyCollection<BrowserOrganizationItem> JobFunctions,
    IReadOnlyCollection<BrowserOrganizationItem> WorkCenters);
