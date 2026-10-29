using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.WorkPermits.Interfaces;
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Domain.Modules.IdentityAccess.Constants;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.Notifications.Entities;
<<<<<<< HEAD
using SESMT.Application.Modules.WorkPermits.DTOs;
=======
>>>>>>> main

namespace SESMT.Infrastructure.Persistence.Repositories.WorkPermits;

public sealed class WorkPermitRepository : IWorkPermitRepository
{
    private readonly SesmtDbContext _dbContext;

    public WorkPermitRepository(SesmtDbContext dbContext) => _dbContext = dbContext;

    public async Task<WorkPermitSearchResult> SearchAsync(WorkPermitSearchRequest request, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default)
    {
        request = request.Validated();
        // Scope is applied before every filter, count and page operation.
        var query = _dbContext.WorkPermits.AsNoTracking().Where(item => companyIds.Contains(item.CompanyId) && item.Active && item.DeletedAt == null);
        if (request.CompanyId.HasValue) query = query.Where(item => item.CompanyId == request.CompanyId);
        if (!string.IsNullOrEmpty(request.Search))
        {
            var literal = request.Search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = "%" + literal + "%";
            query = query.Where(item => EF.Functions.ILike(item.Title, pattern) || EF.Functions.ILike(item.ServiceCode, pattern));
        }
        if (!string.IsNullOrEmpty(request.ServiceCode))
        {
            var code = request.ServiceCode;
            query = query.Where(item => item.ServiceCode == code || item.ServiceCode.StartsWith(code + ",")
                || item.ServiceCode.EndsWith("," + code) || item.ServiceCode.Contains("," + code + ","));
        }
        if (!string.IsNullOrEmpty(request.Status)) query = query.Where(item => item.Status == request.Status);
        if (request.ParticipantUserId.HasValue) query = query.Where(item => item.Participants.Any(person =>
            person.UserId == request.ParticipantUserId && person.Active && person.DeletedAt == null));
        if (request.LocationId.HasValue || request.From.HasValue || request.To.HasValue)
            query = query.Where(item => _dbContext.WorkPermitAppointments.Any(appointment => appointment.Id == item.AppointmentId
                && appointment.CompanyId == item.CompanyId && appointment.Active && appointment.DeletedAt == null
                && (!request.LocationId.HasValue || appointment.LocationId == request.LocationId)
                && (!request.From.HasValue || appointment.EndsAt > request.From)
                && (!request.To.HasValue || appointment.StartsAt < request.To)));
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Include(item => item.Participants).OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        return new(items, total, request.Page, request.PageSize);
    }

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

    public Task<bool> IsActiveLocationInCompanyAsync(Guid locationId, Guid companyId, CancellationToken cancellationToken = default) =>
        _dbContext.Units.AnyAsync(unit => unit.Id == locationId && unit.CompanyId == companyId && unit.Active && unit.DeletedAt == null, cancellationToken);

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

    public async Task AddTemplateVersionAsync(WorkPermitTemplateVersion template, CancellationToken cancellationToken = default) =>
        await _dbContext.WorkPermitTemplateVersions.AddAsync(template, cancellationToken);

    public Task<WorkPermitTemplateVersion?> GetTemplateVersionAsync(Guid templateVersionId, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default) =>
        _dbContext.WorkPermitTemplateVersions.FirstOrDefaultAsync(template => template.Id == templateVersionId
            && companyIds.Contains(template.CompanyId) && template.Active && template.DeletedAt == null, cancellationToken);

    public async Task<IReadOnlyList<WorkPermitTemplateVersion>> ListTemplateVersionsAsync(IReadOnlyCollection<Guid> companyIds, Guid? templateId = null, CancellationToken cancellationToken = default) =>
        await _dbContext.WorkPermitTemplateVersions.Where(template => companyIds.Contains(template.CompanyId)
                && template.Active && template.DeletedAt == null
                && (!templateId.HasValue || template.TemplateId == templateId.Value))
            .OrderBy(template => template.Name).ThenBy(template => template.TemplateId).ThenBy(template => template.Version)
            .ToListAsync(cancellationToken);

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

    public async Task<bool> IsReadyAsync(WorkPermit permit, CancellationToken cancellationToken = default) =>
        (await EvaluateReadinessAsync(permit, cancellationToken)).Ready;

    public async Task<WorkPermitReadinessAssessment> EvaluateReadinessAsync(WorkPermit permit, CancellationToken cancellationToken = default)
    {
        var appointment = await _dbContext.WorkPermitAppointments.AsNoTracking().FirstOrDefaultAsync(item =>
            item.Id == permit.AppointmentId && item.CompanyId == permit.CompanyId && item.Active && item.DeletedAt == null, cancellationToken);
        var templates = await _dbContext.WorkPermitTemplateVersions.AsNoTracking().Where(template =>
            permit.TemplateVersionIds.Contains(template.Id) && template.CompanyId == permit.CompanyId && template.Active && template.DeletedAt == null)
            .ToListAsync(cancellationToken);
        var checklistReady = templates.Count > 0 && templates.Count == permit.TemplateVersionIds.Count
            && WorkPermitTemplateVersion.IsChecklistReady(templates, permit.ChecklistAnswers);
        var participants = await _dbContext.WorkPermitParticipants.AsNoTracking().Where(item =>
            item.WorkPermitId == permit.Id && item.Active && item.DeletedAt == null).ToListAsync(cancellationToken);
        var participantIds = participants.Select(item => item.UserId).Distinct().ToArray();
        var activeUsers = await (from scope in _dbContext.UserScopes
            join user in _dbContext.Users on scope.UserId equals user.Id
            where participantIds.Contains(user.Id) && user.Active && user.DeletedAt == null
                && scope.ScopeType == ScopeType.EMPRESA && scope.ReferenceId == permit.CompanyId && scope.Active && scope.DeletedAt == null
            select user.Id).Distinct().ToListAsync(cancellationToken);
        var codes = permit.ServiceCode.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var requirements = await (from requirement in _dbContext.WorkPermitDocumentRequirements
            join type in _dbContext.DocumentTypes on requirement.DocumentTypeId equals type.Id
            where requirement.CompanyId == permit.CompanyId && codes.Contains(requirement.ServiceCode)
                && requirement.Active && requirement.DeletedAt == null && type.Active && type.DeletedAt == null
            select requirement).AsNoTracking().ToListAsync(cancellationToken);
        // Both links are required: the PT context and the actual worker of this participant.
        // OwnerId is a document administrator, not proof of individual ownership.
        var evidence = await (from ptLink in _dbContext.DocumentLinks
            join document in _dbContext.Documents on ptLink.DocumentId equals document.Id
            join workerLink in _dbContext.DocumentLinks on document.Id equals workerLink.DocumentId
            join worker in _dbContext.Workers on workerLink.EntityId equals worker.Id
            join version in _dbContext.DocumentVersions on document.Id equals version.DocumentId
            join file in _dbContext.StoredFiles on version.StoredFileId equals file.Id
            where ptLink.EntityType == LinkedEntityType.WorkPermit && ptLink.EntityId == permit.Id && ptLink.Active && ptLink.DeletedAt == null
                && workerLink.EntityType == LinkedEntityType.Worker && workerLink.Active && workerLink.DeletedAt == null
                && worker.CompanyId == permit.CompanyId && worker.Active && worker.DeletedAt == null
                && worker.UserId.HasValue && participantIds.Contains(worker.UserId.Value)
                && document.CompanyId == permit.CompanyId && document.Active && document.DeletedAt == null
                && version.VersionNumber == document.CurrentVersionNumber && version.Active && version.DeletedAt == null
            select new WorkPermitEvidence(worker.UserId!.Value, document.DocumentTypeId,
                document.Status == DocumentStatus.Active && version.IsCurrent && version.State == DocumentVersionState.Current,
                file.Active && file.DeletedAt == null && file.Size > 0 && file.ObjectKey != "" && file.FileHash == version.FileHash,
                document.ValidFromDate ?? document.IssueDate, document.ValidUntilDate, version.State)).ToListAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = appointment is null ? today : DateOnly.FromDateTime(appointment.StartsAt.UtcDateTime);
        var until = appointment is null ? today : DateOnly.FromDateTime(appointment.EndsAt.UtcDateTime);
        if (from < today) from = today;
        if (until < from) until = from;
        var workers = await _dbContext.Workers.Where(worker => worker.CompanyId == permit.CompanyId && worker.Active && worker.DeletedAt == null
            && worker.UserId.HasValue && participantIds.Contains(worker.UserId.Value)).Select(worker => new { worker.Id, worker.UserId }).ToListAsync(cancellationToken);
        var rosterMatches = appointment is not null && (appointment.ParticipantIds.Length == 0
            || appointment.ParticipantIds.Order().SequenceEqual(workers.Select(worker => worker.Id).Order()));
        var servicesMatch = appointment is not null && (appointment.ServiceCodes.Length == 0
            || appointment.ServiceCodes.Order().SequenceEqual(codes.Order()));
        return WorkPermitReadinessPolicy.Evaluate(permit,
            appointment is not null && appointment.Status == "CONFIRMED" && appointment.EndsAt > DateTimeOffset.UtcNow && rosterMatches && servicesMatch,
            checklistReady, participants, activeUsers.ToHashSet(), requirements, evidence, from, until,
            appointment is null ? null : WorkPermitDeclarationContent.Create(appointment, templates));
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.CurrentTransaction is not null) return await operation(cancellationToken);
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Npgsql.PostgresException exception) when (exception.SqlState == Npgsql.PostgresErrorCodes.SerializationFailure)
        {
            throw new SESMT.Domain.Exceptions.DomainException("O contexto da PT foi alterado durante a operação. Recarregue e confirme novamente.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.SerializationFailure })
        {
            throw new SESMT.Domain.Exceptions.DomainException("O contexto da PT foi alterado durante a operação. Recarregue e confirme novamente.");
        }
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

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var returnedTransitions = _dbContext.ChangeTracker.Entries<WorkPermitTransition>()
            .Where(entry => entry.State == EntityState.Added && entry.Entity.NewStatus == "DEVOLVIDA")
            .Select(entry => entry.Entity).ToArray();
        foreach (var transition in returnedTransitions)
        {
            var permit = _dbContext.WorkPermits.Local.FirstOrDefault(x => x.Id == transition.WorkPermitId)
                ?? await _dbContext.WorkPermits.FirstOrDefaultAsync(x => x.Id == transition.WorkPermitId, cancellationToken);
            if (permit is null || !permit.CreatedBy.HasValue
                || !await _dbContext.Users.AnyAsync(u => u.Id == permit.CreatedBy.Value && u.Active && u.DeletedAt == null, cancellationToken)
                || !await _dbContext.UserScopes.AnyAsync(s => s.UserId == permit.CreatedBy.Value && s.ScopeType == ScopeType.EMPRESA
                    && s.ReferenceId == permit.CompanyId && s.Active && s.DeletedAt == null, cancellationToken)
                || await _dbContext.Alerts.AnyAsync(a => a.SourceEventId == transition.Id && a.DestinationUserId == permit.CreatedBy.Value, cancellationToken))
                continue;

            await _dbContext.Alerts.AddAsync(new Alert("PT_DEVOLVIDA", "WORK_PERMIT", permit.Id, transition.Id,
                permit.CreatedBy.Value, "IN_APP", "Permissão devolvida", "Uma permissão de trabalho foi devolvida para correção.", transition.OccurredAt), cancellationToken);
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
