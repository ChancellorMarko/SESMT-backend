using SESMT.Application.Exceptions;
using SESMT.Application.Modules.WorkPermits.DTOs;
using SESMT.Application.Modules.WorkPermits.Constants;
using SESMT.Application.Modules.WorkPermits.Interfaces;
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Domain.Modules.WorkPermits.Constants;
using SESMT.Domain.Exceptions;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Application.Modules.WorkPermits.Services;

public sealed class WorkPermitService : IWorkPermitService
{
    private readonly IWorkPermitRepository _repository;
    private readonly IWorkPermitReadinessService _readinessService;

    public WorkPermitService(IWorkPermitRepository repository, IWorkPermitReadinessService readinessService)
    {
        _repository = repository;
        _readinessService = readinessService;
    }

    public IReadOnlyList<WorkPermitServiceResponse> GetServices() =>
        WorkPermitServiceCatalogEntry.All.Select(WorkPermitServiceResponse.From).ToArray();

    public async Task<IReadOnlyList<WorkPermitTemplateResponse>> ListTemplatesAsync(Guid actorId, Guid? companyId, Guid? templateId, CancellationToken cancellationToken = default)
    {
        var companyIds = await _repository.GetAccessibleCompanyIdsAsync(actorId, cancellationToken);
        if (companyId.HasValue)
        {
            if (!companyIds.Contains(companyId.Value))
                throw new NotFoundException("Empresa não encontrada.");
            await EnsureRoleAsync(actorId, companyId.Value, WorkPermitRoleCodes.Configurator, cancellationToken);
            companyIds = [companyId.Value];
        }
        else if (companyIds.Count == 0)
        {
            throw new NotFoundException("Templates não encontrados.");
        }
        if (!companyId.HasValue)
        {
            var authorized = new List<Guid>();
            foreach (var accessibleCompanyId in companyIds)
                if (await _repository.HasRoleInCompanyAsync(actorId, accessibleCompanyId, WorkPermitRoleCodes.Configurator, cancellationToken))
                    authorized.Add(accessibleCompanyId);
            companyIds = authorized;
        }
        if (companyIds.Count == 0)
            throw new WorkPermitForbiddenException();
        return (await _repository.ListTemplateVersionsAsync(companyIds, templateId, cancellationToken))
            .Select(WorkPermitTemplateResponse.From).ToArray();
    }

    public async Task<WorkPermitTemplateResponse> CreateTemplateAsync(CreateWorkPermitTemplateRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var companyIds = await _repository.GetAccessibleCompanyIdsAsync(actorId, cancellationToken);
        if (!companyIds.Contains(request.CompanyId))
            throw new NotFoundException("Empresa não encontrada.");
        await EnsureRoleAsync(actorId, request.CompanyId, WorkPermitRoleCodes.Configurator, cancellationToken);
        var template = new WorkPermitTemplateVersion(request.CompanyId, Guid.NewGuid(), request.Name, 1,
            WorkPermitServiceCatalogEntry.ResolveServiceCodes(request.ServiceIds), request.Questions, request.Declaration, request.Illustrative, actorId);
        await _repository.AddTemplateVersionAsync(template, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return WorkPermitTemplateResponse.From(template);
    }

    public async Task<WorkPermitTemplateResponse> CreateTemplateVersionAsync(Guid templateId, CreateWorkPermitTemplateVersionRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var companyIds = await _repository.GetAccessibleCompanyIdsAsync(actorId, cancellationToken);
        var versions = await _repository.ListTemplateVersionsAsync(companyIds, templateId, cancellationToken);
        var current = versions.OrderByDescending(template => template.Version).FirstOrDefault()
            ?? throw new NotFoundException("Template não encontrado.");
        await EnsureRoleAsync(actorId, current.CompanyId, WorkPermitRoleCodes.Configurator, cancellationToken);
        var nextServiceCodes = WorkPermitServiceCatalogEntry.ResolveServiceCodes(request.ServiceIds);
        WorkPermitTemplateVersion.EnsureSuccessorPreservesRequiredChecks(current, nextServiceCodes, request.Questions);
        var version = new WorkPermitTemplateVersion(current.CompanyId, current.TemplateId, current.Name,
            current.Version + 1, nextServiceCodes, request.Questions, request.Declaration, request.Illustrative, actorId);
        await _repository.AddTemplateVersionAsync(version, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return WorkPermitTemplateResponse.From(version);
    }

    public async Task<WorkPermitResponse> SaveChecklistAsync(Guid permitId, SaveWorkPermitChecklistRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var permit = await GetPermitAsync(permitId, actorId, cancellationToken);
        await EnsureRoleAsync(actorId, permit.CompanyId, WorkPermitRoleCodes.Requester, cancellationToken);
        var templates = new List<WorkPermitTemplateVersion>();
        foreach (var versionId in permit.TemplateVersionIds)
            templates.Add(await _repository.GetTemplateVersionAsync(versionId, [permit.CompanyId], cancellationToken)
                ?? throw new NotFoundException("Versão de template não encontrada."));
        var questions = templates.SelectMany(template => template.Questions).ToDictionary(question => question.Id);
        if (request.Answers.Any(answer => !questions.TryGetValue(answer.QuestionId, out var question)
            || !question.HasValidValue(answer.Value)))
            throw new DomainException("A resposta não pertence às versões fixadas nesta PT.");
        permit.SaveChecklist(request.Answers, actorId);
        await _repository.SaveChangesAsync(cancellationToken);
        return WorkPermitResponse.From(permit);
    }

    public async Task<WorkPermitResponse> CreateAsync(CreateWorkPermitRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var companyIds = await _repository.GetAccessibleCompanyIdsAsync(actorId, cancellationToken);
        var appointment = await _repository.GetAppointmentAsync(request.AppointmentId, companyIds, cancellationToken);
        if (appointment is null)
            throw new NotFoundException("Empresa não encontrada.");
        await EnsureRoleAsync(actorId, appointment.CompanyId, WorkPermitRoleCodes.Requester, cancellationToken);
        if (await _repository.HasWorkPermitForAppointmentAsync(appointment.Id, cancellationToken))
            throw new DomainException("Este agendamento já possui uma permissão de trabalho.");

        var permit = new WorkPermit(appointment.CompanyId, appointment.Id, request.ServiceCode, request.Title, actorId);
        var requestedServiceCodes = request.ServiceCode.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(code => code.ToUpperInvariant()).Distinct().ToArray();
        var catalogServiceCodes = WorkPermitServiceCatalogEntry.All.Select(service => service.Code).ToHashSet(StringComparer.Ordinal);
        var containsCatalogService = requestedServiceCodes.Any(catalogServiceCodes.Contains);
        if (request.TemplateVersionIds is { Count: > 0 })
        {
            var templates = new List<WorkPermitTemplateVersion>();
            foreach (var templateVersionId in request.TemplateVersionIds.Distinct())
                templates.Add(await _repository.GetTemplateVersionAsync(templateVersionId, [appointment.CompanyId], cancellationToken)
                    ?? throw new NotFoundException("Versão de template não encontrada."));
            if (templates.Count != request.TemplateVersionIds.Count)
                throw new DomainException("Uma mesma versão não pode ser selecionada mais de uma vez.");
            permit.PinTemplates(templates, actorId);
            var pinnedServiceCodes = templates.SelectMany(template => template.ServiceCodes).Distinct().Order().ToArray();
            if (!pinnedServiceCodes.SequenceEqual(requestedServiceCodes.Order()))
                throw new DomainException("Os serviços da PT devem corresponder aos serviços das versões selecionadas.");
        }
        else if (containsCatalogService)
            throw new DomainException("Selecione uma versão de checklist para cada serviço da PT.");
        await _repository.AddAsync(permit, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return WorkPermitResponse.From(permit);
    }

    public async Task<WorkPermitAppointmentResponse> CreateAppointmentAsync(CreateWorkPermitAppointmentRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var companyIds = await _repository.GetAccessibleCompanyIdsAsync(actorId, cancellationToken);
        if (!companyIds.Contains(request.CompanyId))
            throw new NotFoundException("Empresa não encontrada.");
        await EnsureRoleAsync(actorId, request.CompanyId, WorkPermitRoleCodes.Requester, cancellationToken);
        var appointment = new WorkPermitAppointment(request.CompanyId, request.Title, request.StartsAt, request.EndsAt, actorId);
        await _repository.AddAppointmentAsync(appointment, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return WorkPermitAppointmentResponse.From(appointment);
    }

    public async Task<WorkPermitParticipantResponse> AddParticipantAsync(Guid permitId, AddWorkPermitParticipantRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var permit = await GetPermitAsync(permitId, actorId, cancellationToken);
        await EnsureRoleAsync(actorId, permit.CompanyId, WorkPermitRoleCodes.Requester, cancellationToken);
        if (!await _repository.IsActiveCompanyUserAsync(request.UserId, permit.CompanyId, cancellationToken))
            throw new NotFoundException("Usuário da empresa não encontrado.");

        var participant = new WorkPermitParticipant(permit.Id, request.UserId, request.Responsibility, actorId);
        permit.AddParticipant(participant, actorId);
        await _repository.AddParticipantAsync(participant, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return WorkPermitParticipantResponse.From(participant);
    }

    public async Task<WorkPermitParticipantResponse> AcknowledgeAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var permit = await GetPermitAsync(permitId, actorId, cancellationToken);
        if (permit.Status != WorkPermitStatus.UnderReview)
            throw new DomainException("A ciência deve ser registrada antes da liberação da permissão.");
        var participant = await _repository.GetParticipantAsync(permitId, actorId, cancellationToken)
            ?? throw new NotFoundException("Participante não encontrado.");
        participant.Acknowledge(actorId, permit.ContentVersion);
        await _repository.SaveChangesAsync(cancellationToken);
        return WorkPermitParticipantResponse.From(participant);
    }

    public async Task ConfigureRequirementAsync(CreateWorkPermitDocumentRequirementRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var companyIds = await _repository.GetAccessibleCompanyIdsAsync(actorId, cancellationToken);
        if (!companyIds.Contains(request.CompanyId))
            throw new NotFoundException("Empresa não encontrada.");
        await EnsureRoleAsync(actorId, request.CompanyId, WorkPermitRoleCodes.Configurator, cancellationToken);
        if (!await _repository.IsActiveDocumentTypeAsync(request.DocumentTypeId, cancellationToken))
            throw new NotFoundException("Tipo documental não encontrado.");
        await _repository.AddRequirementAsync(new WorkPermitDocumentRequirement(
            request.CompanyId, request.ServiceCode, request.DocumentTypeId, request.Description, actorId), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkPermitDocumentRequirementResponse>> GetRequirementsAsync(Guid companyId, string serviceCode, Guid actorId, CancellationToken cancellationToken = default)
    {
        var companyIds = await _repository.GetAccessibleCompanyIdsAsync(actorId, cancellationToken);
        if (!companyIds.Contains(companyId))
            throw new NotFoundException("Empresa não encontrada.");
        await EnsureRoleAsync(actorId, companyId, WorkPermitRoleCodes.Configurator, cancellationToken);
        var requirements = await _repository.ListRequirementsAsync(companyId, serviceCode, cancellationToken);
        return requirements.Select(WorkPermitDocumentRequirementResponse.From).ToArray();
    }

    public async Task LinkDocumentAsync(Guid permitId, LinkWorkPermitDocumentRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var permit = await GetPermitAsync(permitId, actorId, cancellationToken);
        if (permit.Status != WorkPermitStatus.UnderReview)
            throw new DomainException("Evidências só podem ser vinculadas durante a verificação.");
        await EnsureRoleAsync(actorId, permit.CompanyId, WorkPermitRoleCodes.Requester, cancellationToken);
        if (!await _repository.IsActiveCompanyDocumentAsync(request.DocumentId, permit.CompanyId, cancellationToken))
            throw new NotFoundException("Documento da empresa não encontrado.");

        await _repository.AddDocumentLinkAsync(
            new DocumentLink(request.DocumentId, LinkedEntityType.WorkPermit, permit.Id), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkPermitResponse> GetAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken = default) =>
        WorkPermitResponse.From(await GetPermitAsync(permitId, actorId, cancellationToken));

    public async Task<IReadOnlyList<WorkPermitResponse>> ListAsync(Guid actorId, CancellationToken cancellationToken = default)
    {
        var companyIds = await _repository.GetAccessibleCompanyIdsAsync(actorId, cancellationToken);
        var permits = await _repository.ListAsync(companyIds, cancellationToken);
        return permits.Select(WorkPermitResponse.From).ToArray();
    }

    public async Task<IReadOnlyList<WorkPermitTransitionResponse>> GetHistoryAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var companyIds = await _repository.GetAccessibleCompanyIdsAsync(actorId, cancellationToken);
        await GetPermitAsync(permitId, actorId, cancellationToken);
        var history = await _repository.ListHistoryAsync(permitId, companyIds, cancellationToken);
        return history.Select(WorkPermitTransitionResponse.From).ToArray();
    }

    public Task<WorkPermitResponse> ReleaseAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken = default) =>
        TransitionAsync(permitId, actorId, WorkPermitRoleCodes.Releaser, (permit, ready) => permit.Release(actorId, ready), cancellationToken);

    public Task<WorkPermitResponse> StartAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken = default) =>
        TransitionAsync(permitId, actorId, WorkPermitRoleCodes.Operator, (permit, ready) => permit.Start(actorId, ready), cancellationToken);

    public async Task<WorkPermitResponse> SuspendAsync(Guid permitId, Guid actorId, string reason, CancellationToken cancellationToken = default)
    {
        var permit = await GetPermitAsync(permitId, actorId, cancellationToken);
        await EnsureRoleAsync(actorId, permit.CompanyId, WorkPermitRoleCodes.Operator, cancellationToken);
        permit.Suspend(actorId, reason);
        await _repository.SaveChangesAsync(cancellationToken);
        return WorkPermitResponse.From(permit);
    }

    public Task<WorkPermitResponse> ResumeAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken = default) =>
        TransitionAsync(permitId, actorId, WorkPermitRoleCodes.Operator, (permit, ready) => permit.Resume(actorId, ready), cancellationToken);

    public async Task<WorkPermitResponse> CompleteAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var permit = await GetPermitAsync(permitId, actorId, cancellationToken);
        await EnsureRoleAsync(actorId, permit.CompanyId, WorkPermitRoleCodes.Operator, cancellationToken);
        permit.Complete(actorId);
        await _repository.SaveChangesAsync(cancellationToken);
        return WorkPermitResponse.From(permit);
    }

    public async Task<WorkPermitResponse> CancelAsync(Guid permitId, Guid actorId, string reason, CancellationToken cancellationToken = default)
    {
        var permit = await GetPermitAsync(permitId, actorId, cancellationToken);
        var hasRequesterRole = await _repository.HasRoleInCompanyAsync(actorId, permit.CompanyId, WorkPermitRoleCodes.Requester, cancellationToken);
        var hasReleaserRole = await _repository.HasRoleInCompanyAsync(actorId, permit.CompanyId, WorkPermitRoleCodes.Releaser, cancellationToken);
        if (!hasRequesterRole && !hasReleaserRole)
            throw new WorkPermitForbiddenException();
        permit.Cancel(actorId, reason);
        await _repository.SaveChangesAsync(cancellationToken);
        return WorkPermitResponse.From(permit);
    }

    private async Task<WorkPermitResponse> TransitionAsync(Guid permitId, Guid actorId, string requiredRole, Action<WorkPermit, bool> transition, CancellationToken cancellationToken)
    {
        var permit = await GetPermitAsync(permitId, actorId, cancellationToken);
        await EnsureRoleAsync(actorId, permit.CompanyId, requiredRole, cancellationToken);
        var ready = await _readinessService.IsReadyAsync(permit, cancellationToken);
        if (!ready)
            throw new WorkPermitNotReadyException();

        transition(permit, ready);
        await _repository.SaveChangesAsync(cancellationToken);
        return WorkPermitResponse.From(permit);
    }

    private async Task EnsureRoleAsync(Guid actorId, Guid companyId, string roleCode, CancellationToken cancellationToken)
    {
        if (!await _repository.HasRoleInCompanyAsync(actorId, companyId, roleCode, cancellationToken))
            throw new WorkPermitForbiddenException();
    }

    private async Task<WorkPermit> GetPermitAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken)
    {
        var companyIds = await _repository.GetAccessibleCompanyIdsAsync(actorId, cancellationToken);
        if (companyIds.Count == 0)
            throw new NotFoundException("Permissão de trabalho não encontrada.");

        return await _repository.GetByIdAsync(permitId, companyIds, cancellationToken)
            ?? throw new NotFoundException("Permissão de trabalho não encontrada.");
    }
}
