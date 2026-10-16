using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.WorkPermits.Interfaces;
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Domain.Modules.IdentityAccess.Constants;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Infrastructure.Persistence.Repositories.WorkPermits;

public sealed class WorkPermitRepository : IWorkPermitRepository
{
    private readonly SesmtDbContext _dbContext;

    public WorkPermitRepository(SesmtDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(WorkPermit permit, CancellationToken cancellationToken = default) =>
        await _dbContext.WorkPermits.AddAsync(permit, cancellationToken);

    public async Task AddAppointmentAsync(WorkPermitAppointment appointment, CancellationToken cancellationToken = default) =>
        await _dbContext.WorkPermitAppointments.AddAsync(appointment, cancellationToken);

    public Task<WorkPermitAppointment?> GetAppointmentAsync(Guid appointmentId, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default) =>
        _dbContext.WorkPermitAppointments.FirstOrDefaultAsync(appointment => appointment.Id == appointmentId
            && companyIds.Contains(appointment.CompanyId) && appointment.Active && appointment.DeletedAt == null, cancellationToken);

    public Task<bool> IsActiveDocumentTypeAsync(Guid documentTypeId, CancellationToken cancellationToken = default) =>
        _dbContext.DocumentTypes.AnyAsync(documentType => documentType.Id == documentTypeId
            && documentType.Active && documentType.DeletedAt == null, cancellationToken);

    public Task<bool> HasWorkPermitForAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken = default) =>
        _dbContext.WorkPermits.AnyAsync(permit => permit.AppointmentId == appointmentId && permit.Active && permit.DeletedAt == null, cancellationToken);

    public Task<bool> IsActiveCompanyUserAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default) =>
        (from scope in _dbContext.UserScopes
         join user in _dbContext.Users on scope.UserId equals user.Id
         where scope.UserId == userId && scope.ScopeType == ScopeType.EMPRESA && scope.ReferenceId == companyId
             && scope.Active && scope.DeletedAt == null && user.Active && user.DeletedAt == null
         select scope.Id).AnyAsync(cancellationToken);

    public async Task AddParticipantAsync(WorkPermitParticipant participant, CancellationToken cancellationToken = default) =>
        await _dbContext.WorkPermitParticipants.AddAsync(participant, cancellationToken);

    public Task<WorkPermitParticipant?> GetParticipantAsync(Guid permitId, Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.WorkPermitParticipants.FirstOrDefaultAsync(participant => participant.WorkPermitId == permitId
            && participant.UserId == userId && participant.Active && participant.DeletedAt == null, cancellationToken);

    public async Task AddRequirementAsync(WorkPermitDocumentRequirement requirement, CancellationToken cancellationToken = default) =>
        await _dbContext.WorkPermitDocumentRequirements.AddAsync(requirement, cancellationToken);

    public async Task<IReadOnlyList<WorkPermitDocumentRequirement>> ListRequirementsAsync(Guid companyId, string serviceCode, CancellationToken cancellationToken = default)
    {
        var normalizedServiceCode = serviceCode.Trim().ToUpperInvariant();
        return await _dbContext.WorkPermitDocumentRequirements
            .Where(requirement => requirement.CompanyId == companyId && requirement.ServiceCode == normalizedServiceCode
                && requirement.Active && requirement.DeletedAt == null)
            .OrderBy(requirement => requirement.Description)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> IsActiveCompanyDocumentAsync(Guid documentId, Guid companyId, CancellationToken cancellationToken = default) =>
        _dbContext.Documents.AnyAsync(document => document.Id == documentId && document.CompanyId == companyId
            && document.Active && document.DeletedAt == null, cancellationToken);

    public async Task AddDocumentLinkAsync(DocumentLink documentLink, CancellationToken cancellationToken = default) =>
        await _dbContext.DocumentLinks.AddAsync(documentLink, cancellationToken);

    public async Task<bool> IsReadyAsync(WorkPermit permit, CancellationToken cancellationToken = default)
    {
        if (!permit.AppointmentId.HasValue)
            return false;

        var appointmentExists = await _dbContext.WorkPermitAppointments.AnyAsync(appointment => appointment.Id == permit.AppointmentId.Value
            && appointment.CompanyId == permit.CompanyId && appointment.Active && appointment.DeletedAt == null, cancellationToken);
        if (!appointmentExists)
            return false;

        var participants = await _dbContext.WorkPermitParticipants
            .Where(participant => participant.WorkPermitId == permit.Id && participant.Active && participant.DeletedAt == null)
            .ToListAsync(cancellationToken);
        if (participants.Count == 0 || participants.Any(participant => participant.AcknowledgedAt == null
                || participant.AcknowledgedVersion != permit.ContentVersion))
            return false;

        var participantUserIds = participants.Select(participant => participant.UserId).Distinct().ToArray();
        var activeParticipantCount = await _dbContext.Users
            .CountAsync(user => participantUserIds.Contains(user.Id) && user.Active && user.DeletedAt == null, cancellationToken);
        if (activeParticipantCount != participantUserIds.Length)
            return false;

        var requirements = await _dbContext.WorkPermitDocumentRequirements
            .Where(requirement => requirement.CompanyId == permit.CompanyId && requirement.ServiceCode == permit.ServiceCode
                && requirement.Active && requirement.DeletedAt == null)
            .Select(requirement => requirement.DocumentTypeId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (requirements.Count == 0)
            return false;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var documentTypeId in requirements)
        {
            var validEvidenceExists = await (from link in _dbContext.DocumentLinks
                join document in _dbContext.Documents on link.DocumentId equals document.Id
                where link.EntityType == LinkedEntityType.WorkPermit && link.EntityId == permit.Id
                    && link.Active && link.DeletedAt == null
                    && document.CompanyId == permit.CompanyId && document.DocumentTypeId == documentTypeId
                    && document.Status == DocumentStatus.Active && document.Active && document.DeletedAt == null
                    && (document.ValidFromDate == null || document.ValidFromDate <= today)
                    && (document.ValidUntilDate == null || document.ValidUntilDate >= today)
                select document.Id).AnyAsync(cancellationToken);
            if (!validEvidenceExists)
                return false;
        }

        return true;
    }

    public async Task<IReadOnlyList<Guid>> GetAccessibleCompanyIdsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _dbContext.UserScopes
            .Where(scope => scope.UserId == userId
                && scope.ScopeType == ScopeType.EMPRESA
                && scope.ReferenceId.HasValue
                && scope.Active
                && scope.DeletedAt == null
                && _dbContext.Users.Any(user => user.Id == scope.UserId && user.Active && user.DeletedAt == null)
                && _dbContext.Companies.Any(company => company.Id == scope.ReferenceId.Value && company.Active && company.DeletedAt == null))
            .Select(scope => scope.ReferenceId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

    public Task<bool> HasRoleInCompanyAsync(Guid userId, Guid companyId, string roleCode, CancellationToken cancellationToken = default) =>
        (from userRole in _dbContext.UserRoles
         join role in _dbContext.Roles on userRole.RoleId equals role.Id
         where userRole.UserId == userId
             && userRole.CompanyId == companyId
             && userRole.Active
             && userRole.DeletedAt == null
             && _dbContext.Users.Any(user => user.Id == userId && user.Active && user.DeletedAt == null)
             && role.Code == roleCode
             && role.Active
             && role.DeletedAt == null
         select userRole.Id).AnyAsync(cancellationToken);

    public Task<WorkPermit?> GetByIdAsync(Guid permitId, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default) =>
        _dbContext.WorkPermits.Include(permit => permit.Participants).FirstOrDefaultAsync(
            permit => permit.Id == permitId && companyIds.Contains(permit.CompanyId) && permit.Active && permit.DeletedAt == null,
            cancellationToken);

    public async Task<IReadOnlyList<WorkPermit>> ListAsync(IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default) =>
        await _dbContext.WorkPermits
            .Include(permit => permit.Participants)
            .Where(permit => companyIds.Contains(permit.CompanyId) && permit.Active && permit.DeletedAt == null)
            .OrderByDescending(permit => permit.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WorkPermitTransition>> ListHistoryAsync(Guid permitId, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default) =>
        await (from transition in _dbContext.WorkPermitTransitions
            join permit in _dbContext.WorkPermits on transition.WorkPermitId equals permit.Id
            where transition.WorkPermitId == permitId
                && companyIds.Contains(permit.CompanyId)
                && permit.Active
                && permit.DeletedAt == null
            orderby transition.Version
            select transition)
            .ToListAsync(cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.SaveChangesAsync(cancellationToken);
}
