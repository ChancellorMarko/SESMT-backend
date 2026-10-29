using SESMT.Application.Exceptions;
using SESMT.Application.Modules.WorkPermits.DTOs;
using SESMT.Application.Modules.WorkPermits.Constants;
using SESMT.Application.Modules.WorkPermits.Interfaces;
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Domain.Modules.WorkPermits.Constants;
using SESMT.Domain.Exceptions;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Application.Modules.Notifications;

namespace SESMT.Application.Modules.WorkPermits.Services;

public sealed class WorkPermitService : IWorkPermitService
{
    private readonly IWorkPermitRepository _repository;
    private readonly IWorkPermitReadinessService _readinessService;
    private readonly IOperationalDesignationRepository? _designationRepository;
    private readonly NotificationService? _notifications;

    public WorkPermitService(IWorkPermitRepository repository, IWorkPermitReadinessService readinessService, IOperationalDesignationRepository? designationRepository = null, NotificationService? notifications = null)
    {
        _repository = repository;
        _readinessService = readinessService;
        _designationRepository = designationRepository;
        _notifications = notifications;
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
            WorkPermitServiceCatalogEntry.ResolveServiceCodes(request.ServiceIds), request.Questions, request.Declaration, request.Illustrative, actorId, request.ClosingQuestions);
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
        WorkPermitTemplateVersion.EnsureSuccessorPreservesRequiredChecks(current, nextServiceCodes, request.Questions, request.ClosingQuestions);
        var version = new WorkPermitTemplateVersion(current.CompanyId, current.TemplateId, current.Name,
            current.Version + 1, nextServiceCodes, request.Questions, request.Declaration, request.Illustrative, actorId, request.ClosingQuestions);
        await _repository.AddTemplateVersionAsync(version, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return WorkPermitTemplateResponse.From(version);
    }

    public Task<WorkPermitResponse> SaveChecklistAsync(Guid permitId, SaveWorkPermitChecklistRequest request, Guid actorId, CancellationToken cancellationToken = default) =>
        _repository.ExecuteInTransactionAsync(async _ =>
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
            permit.EnsureExpectedVersion(request.ExpectedVersion);
            permit.SaveChecklist(request.Answers, actorId);
            await _repository.SaveChangesAsync(cancellationToken);
            return WorkPermitResponse.From(permit);
        }, cancellationToken);

    public async Task<WorkPermitChecklistResponse> OpenAppointmentAsync(Guid appointmentId, OpenWorkPermitRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var companyIds = await _repository.GetAccessibleCompanyIdsAsync(actorId, cancellationToken);
        var appointment = await _repository.GetAppointmentAsync(appointmentId, companyIds, cancellationToken)
            ?? throw new NotFoundException("Agendamento não encontrado.");
        await EnsureRoleAsync(actorId, appointment.CompanyId, WorkPermitRoleCodes.Requester, cancellationToken);
        if (await _repository.HasWorkPermitForAppointmentAsync(appointmentId, cancellationToken))
            throw new DomainException("Este agendamento já possui uma permissão de trabalho.");
        if (request.TemplateVersions.Count == 0 || request.TemplateVersions.Select(item => item.TemplateId).Distinct().Count() != request.TemplateVersions.Count
            || request.ParticipantIds.Count == 0 || request.ParticipantIds.Any(id => id == Guid.Empty)
            || request.ParticipantIds.Distinct().Count() != request.ParticipantIds.Count)
            throw new DomainException("Informe versões de checklist e participantes válidos, sem duplicidade.");

        var available = await _repository.ListTemplateVersionsAsync([appointment.CompanyId], cancellationToken: cancellationToken);
        var templates = new List<WorkPermitTemplateVersion>();
        foreach (var requested in request.TemplateVersions)
            templates.Add(available.SingleOrDefault(template => template.TemplateId == requested.TemplateId && template.Version == requested.Version)
                ?? throw new NotFoundException("Versão de template não encontrada."));
        var codes = templates.SelectMany(template => template.ServiceCodes).Distinct().Order().ToArray();
        if (codes.Length == 0) throw new DomainException("Selecione ao menos um serviço.");
        var permit = new WorkPermit(appointment.CompanyId, appointment.Id, string.Join(",", codes), appointment.Title, actorId);
        permit.PinTemplates(templates, actorId);
        foreach (var userId in request.ParticipantIds)
        {
            if (!await _repository.IsActiveCompanyUserAsync(userId, appointment.CompanyId, cancellationToken))
                throw new NotFoundException("Participante da empresa não encontrado.");
            var participant = new WorkPermitParticipant(permit.Id, userId, "PARTICIPANTE", actorId);
            permit.AddParticipant(participant, actorId);
            await _repository.AddParticipantAsync(participant, cancellationToken);
        }
        await _repository.AddAsync(permit, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return WorkPermitChecklistResponse.From(WorkPermitResponse.From(permit),
            codes.Select(code => WorkPermitServiceCatalogEntry.All.Single(service => service.Code == code).Id).ToArray());
    }

    public async Task<WorkPermitChecklistResponse> GetChecklistAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var permit = await GetPermitAsync(permitId, actorId, cancellationToken);
        var templates = await GetPinnedTemplatesAsync(permit, cancellationToken);
        var services = templates.SelectMany(template => template.ServiceCodes).Distinct()
            .Select(code => WorkPermitServiceCatalogEntry.All.Single(service => service.Code == code).Id).ToArray();
        return WorkPermitChecklistResponse.From(WorkPermitResponse.From(permit), services, MapAnswers(permit.ChecklistAnswers, templates));
    }

    public Task<WorkPermitChecklistResponse> PatchChecklistAsync(Guid permitId, PatchWorkPermitChecklistRequest request, Guid actorId, CancellationToken cancellationToken = default) =>
        _repository.ExecuteInTransactionAsync(async _ =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var permit = await GetPermitAsync(permitId, actorId, cancellationToken);
            await EnsureRoleAsync(actorId, permit.CompanyId, WorkPermitRoleCodes.Requester, cancellationToken);
            permit.EnsureExpectedVersion(request.ExpectedVersion);
            var answers = request.Answers.Select(answer => new WorkPermitChecklistAnswer(answer.QuestionId,
                    answer.Value.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => answer.Value.GetString(),
                System.Text.Json.JsonValueKind.True => "true",
                System.Text.Json.JsonValueKind.False => "false",
                System.Text.Json.JsonValueKind.Number => answer.Value.GetRawText(),
                System.Text.Json.JsonValueKind.Null => null,
                _ => throw new DomainException("O valor da resposta deve ser texto, número ou booleano.")
            }, answer.Justification)).ToArray();
            var templates = new List<WorkPermitTemplateVersion>();
            foreach (var versionId in permit.TemplateVersionIds)
                templates.Add(await _repository.GetTemplateVersionAsync(versionId, [permit.CompanyId], cancellationToken)
                        ?? throw new NotFoundException("Versão de template não encontrada."));
            var questions = templates.SelectMany(template => template.Questions).ToDictionary(question => question.Id);
            if (answers.Any(answer => !questions.TryGetValue(answer.QuestionId, out var question) || !question.HasValidValue(answer.Value)))
                throw new DomainException("A resposta não pertence às versões fixadas nesta PT ou tem tipo inválido.");
            permit.SaveChecklist(answers, actorId);
            await _repository.SaveChangesAsync(cancellationToken);
            return WorkPermitChecklistResponse.From(WorkPermitResponse.From(permit),
                    templates.SelectMany(template => template.ServiceCodes).Distinct()
                        .Select(code => WorkPermitServiceCatalogEntry.All.Single(service => service.Code == code).Id).ToArray(),
                    MapAnswers(permit.ChecklistAnswers, templates));
        }, cancellationToken);

    private async Task<List<WorkPermitTemplateVersion>> GetPinnedTemplatesAsync(WorkPermit permit, CancellationToken cancellationToken)
    {
        var templates = new List<WorkPermitTemplateVersion>();
        foreach (var versionId in permit.TemplateVersionIds)
            templates.Add(await _repository.GetTemplateVersionAsync(versionId, [permit.CompanyId], cancellationToken)
                ?? throw new NotFoundException("Versão de template não encontrada."));
        return templates;
    }

    private static IReadOnlyList<ChecklistAnswerResponse> MapAnswers(
        IEnumerable<WorkPermitChecklistAnswer> answers, IEnumerable<WorkPermitTemplateVersion> templates)
    {
        var questions = templates.SelectMany(template => template.Questions).ToDictionary(question => question.Id);
        return answers.Select(answer =>
        {
            var question = questions[answer.QuestionId];
            var element = answer.Value is null
                ? System.Text.Json.JsonDocument.Parse("null").RootElement.Clone()
                : question.Type.ToUpperInvariant() switch
                {
                    "BOOLEAN" => System.Text.Json.JsonSerializer.SerializeToElement(bool.Parse(answer.Value)),
                    "NUMBER" => System.Text.Json.JsonSerializer.SerializeToElement(decimal.Parse(answer.Value, System.Globalization.CultureInfo.InvariantCulture)),
                    _ => System.Text.Json.JsonSerializer.SerializeToElement(answer.Value)
                };
            return new ChecklistAnswerResponse(answer.QuestionId, element, answer.Justification);
        }).ToArray();
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
        if (!request.LocationId.HasValue || !await _repository.IsActiveLocationInCompanyAsync(request.LocationId.Value, request.CompanyId, cancellationToken))
            throw new NotFoundException("Local não encontrado na empresa e escopo da sessão.");
        var appointment = new WorkPermitAppointment(request.CompanyId, request.Title, request.StartsAt, request.EndsAt, actorId, request.LocationId);
        await _repository.AddAppointmentAsync(appointment, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return WorkPermitAppointmentResponse.From(appointment);
    }

    public Task<WorkPermitParticipantResponse> AddParticipantAsync(Guid permitId, AddWorkPermitParticipantRequest request, Guid actorId, CancellationToken cancellationToken = default) =>
        _repository.ExecuteInTransactionAsync(async _ =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var permit = await GetPermitAsync(permitId, actorId, cancellationToken);
            await EnsureRoleAsync(actorId, permit.CompanyId, WorkPermitRoleCodes.Requester, cancellationToken);
            if (!await _repository.IsActiveCompanyUserAsync(request.UserId, permit.CompanyId, cancellationToken))
                throw new NotFoundException("Usuário da empresa não encontrado.");

            permit.EnsureExpectedVersion(request.ExpectedVersion);
            var participant = new WorkPermitParticipant(permit.Id, request.UserId, request.Responsibility, actorId);
            permit.AddParticipant(participant, actorId);
            await _repository.AddParticipantAsync(participant, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            return WorkPermitParticipantResponse.From(participant);
        }, cancellationToken);

    public Task<WorkPermitParticipantResponse> AcknowledgeAsync(Guid permitId, Guid actorId,
        WorkPermitAcknowledgementRequest request, CancellationToken cancellationToken = default) =>
        _repository.ExecuteInTransactionAsync(async ct =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!request.Confirmation || request.ParticipantUserId == Guid.Empty)
                throw new ArgumentException("A confirmação explícita do participante é obrigatória.");
            var permit = await GetPermitAsync(permitId, actorId, ct);
            permit.EnsureExpectedVersion(request.ExpectedVersion);
            if (permit.ContentVersion != request.ContentVersion)
                throw new DomainException("A declaração foi alterada. Consulte o conteúdo atual antes da ciência.");
            if (permit.Status != WorkPermitStatus.UnderReview)
                throw new DomainException("A ciência deve ser registrada antes da liberação da permissão.");
            if (!await _repository.IsActiveCompanyUserAsync(request.ParticipantUserId, permit.CompanyId, ct))
                throw new NotFoundException("Participante não encontrado.");
            if (request.ParticipantUserId != actorId)
            {
                await EnsureRoleAsync(actorId, permit.CompanyId, WorkPermitRoleCodes.Operator, ct);
                await EnsureDesignationAsync(permit, actorId, "atender", ct);
            }
            var participant = await _repository.GetParticipantAsync(permitId, request.ParticipantUserId, ct)
                ?? throw new NotFoundException("Participante não encontrado.");
            var templates = await GetPinnedTemplatesAsync(permit, ct);
            if (templates.Count == 0) throw new WorkPermitNotReadyException();
            var appointment = permit.AppointmentId.HasValue
                ? await _repository.GetAppointmentAsync(permit.AppointmentId.Value, [permit.CompanyId], ct) : null;
            if (appointment is null) throw new WorkPermitNotReadyException();
            var declaration = WorkPermitDeclarationContent.Create(appointment, templates);
            participant.Acknowledge(actorId, permit.ContentVersion, request.ConfirmedName, request.IdentificationMethod,
                declaration, request.Confirmation);
            permit.RecordAcknowledgement(actorId);
            await _repository.SaveChangesAsync(ct);
            return WorkPermitParticipantResponse.From(participant);
        }, cancellationToken);

    public async Task<WorkPermitDeclarationResponse> GetDeclarationsAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var permit = await GetPermitAsync(permitId, actorId, cancellationToken);
        var templates = await GetPinnedTemplatesAsync(permit, cancellationToken);
        var appointment = permit.AppointmentId.HasValue
            ? await _repository.GetAppointmentAsync(permit.AppointmentId.Value, [permit.CompanyId], cancellationToken) : null;
        return new(permit.Id, permit.Version, permit.ContentVersion,
            templates.Select(item => new WorkPermitDeclaration(item.Id, item.Version, item.Declaration)).ToArray(),
            appointment is null ? null : new(appointment.Id, appointment.Version, appointment.LocationId, appointment.ResponsibleId,
                appointment.StartsAt, appointment.EndsAt, appointment.ServiceCodes, appointment.ParticipantIds));
    }

    public async Task<WorkPermitReadinessAssessment> GetEligibilityAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken = default) =>
        await _readinessService.EvaluateAsync(await GetPermitAsync(permitId, actorId, cancellationToken), cancellationToken);

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

    public Task LinkDocumentAsync(Guid permitId, LinkWorkPermitDocumentRequest request, Guid actorId, CancellationToken cancellationToken = default) =>
        _repository.ExecuteInTransactionAsync(async _ =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var permit = await GetPermitAsync(permitId, actorId, cancellationToken);
            if (permit.Status != WorkPermitStatus.UnderReview)
                throw new DomainException("Evidências só podem ser vinculadas durante a verificação.");
            await EnsureRoleAsync(actorId, permit.CompanyId, WorkPermitRoleCodes.Requester, cancellationToken);
            if (!await _repository.IsActiveCompanyDocumentAsync(request.DocumentId, permit.CompanyId, cancellationToken))
                throw new NotFoundException("Documento da empresa não encontrado.");

            permit.EnsureExpectedVersion(request.ExpectedVersion);
            permit.MarkContentChanged(actorId);
            await _repository.AddDocumentLinkAsync(
                    new DocumentLink(request.DocumentId, LinkedEntityType.WorkPermit, permit.Id), cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);

            return true;
        }, cancellationToken);

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

    public async Task<WorkPermitPage> SearchAsync(WorkPermitSearchRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request = request.Validated();
        var companyIds = await _repository.GetAccessibleCompanyIdsAsync(actorId, cancellationToken);
        if (request.CompanyId.HasValue)
        {
            if (!companyIds.Contains(request.CompanyId.Value)) throw new NotFoundException("Empresa não encontrada.");
            companyIds = [request.CompanyId.Value];
        }
        if (companyIds.Count == 0) return new([], 0, request.Page, request.PageSize);
        var page = await _repository.SearchAsync(request, companyIds, cancellationToken);
        return new(page.Items.Select(WorkPermitResponse.From).ToArray(), page.Total, page.Page, page.PageSize);
    }

    public Task<WorkPermitResponse> ReleaseAsync(Guid permitId, Guid actorId, WorkPermitReleaseRequest request, CancellationToken cancellationToken = default) =>
        _repository.ExecuteInTransactionAsync(async ct =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var permit = await GetPermitAsync(permitId, actorId, ct);
            await EnsureRoleAsync(actorId, permit.CompanyId, WorkPermitRoleCodes.Releaser, ct);
            permit.EnsureExpectedVersion(request.ExpectedVersion);
            await EnsureDesignationAsync(permit, actorId, "liberar", ct);
            var appointment = await _repository.GetAppointmentAsync(permit.AppointmentId!.Value, [permit.CompanyId], ct);
            if (appointment is null || appointment.Status != "CONFIRMED") throw new WorkPermitNotReadyException();
            if (!request.Confirmation || string.IsNullOrWhiteSpace(request.Conditions)
                || request.AuthorizedUntil <= request.AuthorizedFrom || request.AuthorizedUntil <= DateTimeOffset.UtcNow
                || request.AuthorizedFrom < appointment.StartsAt || request.AuthorizedUntil > appointment.EndsAt)
                throw new ArgumentException("Confirme as condições e uma janela contida no atendimento confirmado.");
            var assessment = await _readinessService.EvaluateAsync(permit, ct);
            if (!assessment.Ready) throw new WorkPermitNotReadyException(assessment);
            var ready = assessment.Ready;
            permit.Release(actorId, ready, request.AuthorizedFrom, request.AuthorizedUntil, request.Conditions, request.Confirmation);
            await _repository.SaveChangesAsync(ct);
            return WorkPermitResponse.From(permit);
        }, cancellationToken);

    public Task<WorkPermitResponse> StartAsync(Guid permitId, Guid actorId, int expectedVersion, CancellationToken cancellationToken = default) =>
        TransitionWithDesignationAsync(permitId, actorId, expectedVersion, WorkPermitStatus.Released,
            (permit, ready) => permit.Start(actorId, ready), cancellationToken);

    public Task<WorkPermitResponse> SuspendAsync(Guid permitId, Guid actorId, string reason, int expectedVersion, CancellationToken cancellationToken = default) =>
        _repository.ExecuteInTransactionAsync(async ct =>
        {
            var permit = await GetPermitAsync(permitId, actorId, ct);
            await EnsureRoleAsync(actorId, permit.CompanyId, WorkPermitRoleCodes.Operator, ct);
            permit.EnsureExpectedVersion(expectedVersion);
            await EnsureDesignationAsync(permit, actorId, "atender", ct);
            permit.Suspend(actorId, reason);
            await _repository.SaveChangesAsync(ct);
            return WorkPermitResponse.From(permit);
        }, cancellationToken);

<<<<<<< HEAD
    public Task<WorkPermitResponse> ResumeAsync(Guid permitId, Guid actorId, int expectedVersion, CancellationToken cancellationToken = default) =>
        TransitionWithDesignationAsync(permitId, actorId, expectedVersion, WorkPermitStatus.Suspended,
            (permit, ready) => permit.Resume(actorId, ready), cancellationToken);
=======
    public async Task<WorkPermitResponse> CompleteAsync(Guid permitId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var permit = await GetPermitAsync(permitId, actorId, cancellationToken);
        await EnsureRoleAsync(actorId, permit.CompanyId, WorkPermitRoleCodes.Operator, cancellationToken);
        await EnsureDesignationAsync(permit, actorId, "atender", cancellationToken);
        permit.Complete(actorId);
        if (_notifications is not null)
            await _notifications.EnqueueReviewersAsync(permit, permit.Transitions.Last().Id, "PT_AGUARDANDO_REVISAO", cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return WorkPermitResponse.From(permit);
    }
>>>>>>> main

    public Task<WorkPermitResponse> CompleteAsync(Guid permitId, Guid actorId, WorkPermitCompletionRequest request, CancellationToken cancellationToken = default) =>
        _repository.ExecuteInTransactionAsync(async ct =>
        {
            var permit = await GetPermitAsync(permitId, actorId, ct);
            await EnsureRoleAsync(actorId, permit.CompanyId, WorkPermitRoleCodes.Operator, ct);
            permit.EnsureExpectedVersion(request.ExpectedVersion);
            await EnsureDesignationAsync(permit, actorId, "atender", ct);
            var templates = await GetPinnedTemplatesAsync(permit, ct);
            if (request.ClosingAnswers is null || !WorkPermitTemplateVersion.IsClosingChecklistReady(templates, request.ClosingAnswers))
                throw new WorkPermitNotReadyException();
            permit.Complete(actorId, request.Summary, request.ActualEnd, request.ClosingAnswers, true);
            if (_notifications is not null)
                await _notifications.EnqueueReviewersAsync(permit, permit.Transitions.Last().Id, "PT_AGUARDANDO_REVISAO", ct);
            await _repository.SaveChangesAsync(ct);
            return WorkPermitResponse.From(permit);
        }, cancellationToken);

    public Task<WorkPermitResponse> AddObservationAsync(Guid permitId, Guid actorId, WorkPermitObservationRequest request, CancellationToken cancellationToken = default) =>
        _repository.ExecuteInTransactionAsync(async ct =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var permit = await GetPermitAsync(permitId, actorId, ct);
            await EnsureRoleAsync(actorId, permit.CompanyId, WorkPermitRoleCodes.Operator, ct);
            permit.EnsureExpectedVersion(request.ExpectedVersion);
            await EnsureDesignationAsync(permit, actorId, "atender", ct);
            permit.AddObservation(actorId, request.Observation);
            await _repository.SaveChangesAsync(ct);
            return WorkPermitResponse.From(permit);
        }, cancellationToken);

    public Task<WorkPermitResponse> CancelAsync(Guid permitId, Guid actorId, string reason, int expectedVersion, CancellationToken cancellationToken = default) =>
        _repository.ExecuteInTransactionAsync(async ct =>
        {
            var permit = await GetPermitAsync(permitId, actorId, ct);
            var hasRequesterRole = await _repository.HasRoleInCompanyAsync(actorId, permit.CompanyId, WorkPermitRoleCodes.Requester, ct);
            var hasReleaserRole = await _repository.HasRoleInCompanyAsync(actorId, permit.CompanyId, WorkPermitRoleCodes.Releaser, ct);
            if (!hasRequesterRole && !hasReleaserRole) throw new WorkPermitForbiddenException();
            permit.EnsureExpectedVersion(expectedVersion);
            permit.Cancel(actorId, reason);
            await _repository.SaveChangesAsync(ct);
            return WorkPermitResponse.From(permit);
        }, cancellationToken);

    private Task<WorkPermitResponse> TransitionWithDesignationAsync(Guid permitId, Guid actorId, int expectedVersion,
        string expectedStatus, Action<WorkPermit, bool> transition, CancellationToken cancellationToken) =>
        _repository.ExecuteInTransactionAsync(async ct =>
        {
            var permit = await GetPermitAsync(permitId, actorId, ct);
            await EnsureRoleAsync(actorId, permit.CompanyId, WorkPermitRoleCodes.Operator, ct);
            permit.EnsureExpectedVersion(expectedVersion);
            if (permit.Status != expectedStatus) throw new DomainException("O estado da PT não admite esta ação.");
            await EnsureDesignationAsync(permit, actorId, "atender", ct);
            permit.EnsureAuthorizedWindow(DateTimeOffset.UtcNow);
            var assessment = await _readinessService.EvaluateAsync(permit, ct);
            if (!assessment.Ready) throw new WorkPermitNotReadyException(assessment);
            var ready = assessment.Ready;
            transition(permit, ready);
            await _repository.SaveChangesAsync(ct);
            return WorkPermitResponse.From(permit);
        }, cancellationToken);

    private async Task EnsureDesignationAsync(WorkPermit permit, Guid actorId, string type, CancellationToken cancellationToken)
    {
        if (!permit.AppointmentId.HasValue || _designationRepository is null)
            throw new WorkPermitForbiddenException();
        var appointment = await _repository.GetAppointmentAsync(permit.AppointmentId.Value, [permit.CompanyId], cancellationToken);
        if (appointment?.LocationId is not Guid locationId)
            throw new WorkPermitForbiddenException();
        if (type == "atender" && appointment.ResponsibleId != actorId)
            throw new WorkPermitForbiddenException();
        var serviceCodes = permit.ServiceCode.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var serviceIds = WorkPermitServiceCatalogEntry.All.Where(x => serviceCodes.Contains(x.Code, StringComparer.OrdinalIgnoreCase)).Select(x => x.Id).ToArray();
        if (serviceIds.Length != serviceCodes.Length
            || !await _designationRepository.HasEffectiveDesignationAsync(actorId, permit.CompanyId, locationId, serviceIds, type, DateTimeOffset.UtcNow, cancellationToken))
            throw new WorkPermitForbiddenException();
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
