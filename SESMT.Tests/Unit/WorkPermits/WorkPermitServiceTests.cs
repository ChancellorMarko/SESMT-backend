using SESMT.Application.Exceptions;
using SESMT.Application.Modules.WorkPermits.DTOs;
using SESMT.Application.Modules.WorkPermits.Constants;
using SESMT.Application.Modules.WorkPermits.Interfaces;
using SESMT.Application.Modules.WorkPermits.Services;
using SESMT.Domain.Modules.WorkPermits.Constants;
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Domain.Exceptions;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Tests.Unit.WorkPermits;

public class WorkPermitServiceTests
{
    [Fact]
    public async Task CreateTemplateVersionAsync_ShouldAppendVersionWithoutChangingPreviousVersion()
    {
        var companyId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var repository = new FakeWorkPermitRepository(companyId, [WorkPermitRoleCodes.Configurator]);
        var service = new WorkPermitService(repository, new NotReadyService());
        var question = new WorkPermitChecklistQuestion(Guid.NewGuid(), "Verifique", "BOOLEAN", false, false);
        var original = await service.CreateTemplateAsync(new CreateWorkPermitTemplateRequest(companyId, "Altura",
            [WorkPermitServiceCatalogEntry.All.Single(item => item.Code == "ALTURA").Id], [question], "Exemplo", true), actorId);

        var revised = await service.CreateTemplateVersionAsync(original.TemplateId,
            new CreateWorkPermitTemplateVersionRequest([WorkPermitServiceCatalogEntry.All.Single(item => item.Code == "ALTURA").Id],
                [question with { Text = "Verifique novamente" }], "Exemplo", true), actorId);

        Assert.Equal(1, original.Version);
        Assert.Equal(2, revised.Version);
        Assert.Equal("Verifique", repository.Templates.Single(template => template.Version == 1).Questions.Single().Text);
        Assert.Equal(2, repository.Templates.Count);
    }

    [Fact]
    public async Task CreateTemplateAsync_ShouldRequireConfiguratorRole()
    {
        var companyId = Guid.NewGuid();
        var repository = new FakeWorkPermitRepository(companyId);
        var service = new WorkPermitService(repository, new NotReadyService());
        var request = new CreateWorkPermitTemplateRequest(companyId, "Altura", [WorkPermitServiceCatalogEntry.All[0].Id],
            [new WorkPermitChecklistQuestion(Guid.NewGuid(), "Verifique", "BOOLEAN", true, true)], "Exemplo", true);

        await Assert.ThrowsAsync<WorkPermitForbiddenException>(() => service.CreateTemplateAsync(request, Guid.NewGuid()));
        Assert.Empty(repository.Templates);
    }

    [Fact]
    public async Task CreateTemplateVersionAsync_ShouldRejectRemovingRequiredChecks()
    {
        var companyId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var repository = new FakeWorkPermitRepository(companyId, [WorkPermitRoleCodes.Configurator]);
        var service = new WorkPermitService(repository, new NotReadyService());
        var question = new WorkPermitChecklistQuestion(Guid.NewGuid(), "Verifique", "BOOLEAN", true, true);
        var original = await service.CreateTemplateAsync(new CreateWorkPermitTemplateRequest(companyId, "Altura",
            [WorkPermitServiceCatalogEntry.All[0].Id], [question], "Exemplo", true), actorId);

        await Assert.ThrowsAsync<DomainException>(() => service.CreateTemplateVersionAsync(original.TemplateId,
            new CreateWorkPermitTemplateVersionRequest([WorkPermitServiceCatalogEntry.All[0].Id], [], "Exemplo", true), actorId));
        Assert.Single(repository.Templates);
    }

    [Fact]
    public async Task CreateAsync_ShouldRequirePinnedTemplateForCatalogServices()
    {
        var companyId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var repository = new FakeWorkPermitRepository(companyId, [WorkPermitRoleCodes.Requester]);
        var service = new WorkPermitService(repository, new NotReadyService());

        await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(
            new CreateWorkPermitRequest(repository.Appointment.Id, "ALTURA", "Trabalho em altura"), actorId));
        Assert.Empty(repository.Permits);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectCompanyOutsideActorScope()
    {
        var repository = new FakeWorkPermitRepository(Guid.NewGuid());
        var service = new WorkPermitService(repository, new NotReadyService());

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(
            new CreateWorkPermitRequest(Guid.NewGuid(), "MANUTENCAO", "Manutenção"),
            Guid.NewGuid()));

        Assert.Empty(repository.Permits);
    }

    [Fact]
    public async Task CreateAsync_ShouldSavePermitInReviewAndActor()
    {
        var companyId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var repository = new FakeWorkPermitRepository(companyId, [WorkPermitRoleCodes.Requester]);
        var service = new WorkPermitService(repository, new NotReadyService());

        var result = await service.CreateAsync(
            new CreateWorkPermitRequest(repository.Appointment.Id, "MANUTENCAO", "Manutenção"),
            actorId);

        Assert.Equal(WorkPermitStatus.UnderReview, result.Status);
        Assert.Equal(repository.Appointment.Id, result.AppointmentId);
        Assert.Equal("MANUTENCAO", result.ServiceCode);
        Assert.Equal(actorId, repository.Permits.Single().CreatedBy);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectSecondPermitForSameAppointment()
    {
        var companyId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var repository = new FakeWorkPermitRepository(companyId, [WorkPermitRoleCodes.Requester]);
        repository.Permits.Add(new WorkPermit(companyId, repository.Appointment.Id, "MANUTENCAO", "Existente", actorId));
        var service = new WorkPermitService(repository, new NotReadyService());

        await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(
            new CreateWorkPermitRequest(repository.Appointment.Id, "MANUTENCAO", "Duplicada"), actorId));

        Assert.Single(repository.Permits);
    }

    [Fact]
    public async Task OpenAppointmentAsync_ShouldPinEverySelectedTemplateAndParticipantAndRemainUnderReview()
    {
        var companyId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var repository = new FakeWorkPermitRepository(companyId, [WorkPermitRoleCodes.Requester, WorkPermitRoleCodes.Configurator]);
        var service = new WorkPermitService(repository, new NotReadyService());
        var questionA = new WorkPermitChecklistQuestion(Guid.NewGuid(), "Calor", "BOOLEAN", true, true);
        var questionB = new WorkPermitChecklistQuestion(Guid.NewGuid(), "Confinado", "BOOLEAN", true, true);
        var hot = await service.CreateTemplateAsync(new CreateWorkPermitTemplateRequest(companyId, "Quente",
            [WorkPermitServiceCatalogEntry.All.Single(item => item.Code == "QUENTE").Id], [questionA], "Exemplo", true), actorId);
        var confined = await service.CreateTemplateAsync(new CreateWorkPermitTemplateRequest(companyId, "Confinado",
            [WorkPermitServiceCatalogEntry.All.Single(item => item.Code == "CONFINADO").Id], [questionB], "Exemplo", true), actorId);

        var result = await service.OpenAppointmentAsync(repository.Appointment.Id,
            new OpenWorkPermitRequest([new(hot.TemplateId, hot.Version), new(confined.TemplateId, confined.Version)], [participantId]), actorId);

        Assert.Equal("em_verificacao", result.OperationalStatus);
        Assert.Equal("nao_enviada", result.ReviewStatus);
        Assert.Equal(2, result.TemplateVersionIds.Count);
        Assert.Equal(2, result.ServiceIds.Count);
        Assert.Contains(repository.Permits.Single().Participants, item => item.UserId == participantId);
        Assert.Equal(3, result.Versao);

        using var json = System.Text.Json.JsonDocument.Parse("true");
        var saved = await service.PatchChecklistAsync(result.Id,
            new PatchWorkPermitChecklistRequest(result.Versao, [new ChecklistAnswerRequest(questionA.Id, json.RootElement.Clone())]), actorId);
        Assert.Equal("em_verificacao", saved.OperationalStatus);
        Assert.Single(saved.Respostas);
        Assert.Equal(System.Text.Json.JsonValueKind.True, saved.Respostas[0].Value.ValueKind);
        Assert.Equal(2, saved.TemplateVersionIds.Count);
        Assert.Equal(4, saved.Versao);
        await Assert.ThrowsAsync<DomainException>(() => service.PatchChecklistAsync(result.Id,
            new PatchWorkPermitChecklistRequest(result.Versao, []), actorId));
    }

    [Fact]
    public void SaveChecklist_ShouldAdvanceVersionAndRequireExpectedVersionForNextEdit()
    {
        var permit = new WorkPermit(Guid.NewGuid(), Guid.NewGuid(), "MANUTENCAO", "Manutenção", Guid.NewGuid());
        var expected = permit.Version;
        permit.EnsureExpectedVersion(expected);
        permit.SaveChecklist([], Guid.NewGuid());

        Assert.Equal(expected + 1, permit.Version);
        Assert.Throws<DomainException>(() => permit.EnsureExpectedVersion(expected));
        permit.EnsureExpectedVersion(permit.Version);
    }

    [Fact]
    public async Task AddParticipantAsync_ShouldAssignCompanyUserAndPersist()
    {
        var companyId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var permit = new WorkPermit(companyId, Guid.NewGuid(), "MANUTENCAO", "Manutenção", actorId);
        var repository = new FakeWorkPermitRepository(companyId, [WorkPermitRoleCodes.Requester], permit);
        var service = new WorkPermitService(repository, new NotReadyService());

        var result = await service.AddParticipantAsync(permit.Id,
            new AddWorkPermitParticipantRequest(actorId, "OPERADOR", permit.Version), actorId);

        Assert.Equal(actorId, result.UserId);
        Assert.Single(permit.Participants);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task AcknowledgeAsync_ShouldRecordAuthenticatedParticipantOnly()
    {
        var companyId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var repository = new FakeWorkPermitRepository(companyId);
        var permit = new WorkPermit(companyId, repository.Appointment.Id, "MANUTENCAO", "Manutenção", actorId);
        permit.AddParticipant(new WorkPermitParticipant(permit.Id, actorId, "OPERADOR", actorId), actorId);
        repository.Permits.Add(permit);
        var template = new WorkPermitTemplateVersion(companyId, Guid.NewGuid(), "Exemplo", 1, ["ALTURA"],
            [new(Guid.NewGuid(), "Conferir", "BOOLEAN", true, true)], "Declaração fictícia", true, actorId);
        repository.Templates.Add(template); permit.PinTemplates([template], actorId);
        var service = new WorkPermitService(repository, new NotReadyService());

        var result = await service.AcknowledgeAsync(permit.Id, actorId,
            new(permit.Version, permit.ContentVersion, actorId, "Participante", "Sessão autenticada de demonstração", true));

        Assert.NotNull(result.AcknowledgedAt);
        Assert.Equal(permit.ContentVersion, result.AcknowledgedVersion);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task LinkDocumentAsync_ShouldRequireSameCompanyAndRequesterRole()
    {
        var companyId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var permit = new WorkPermit(companyId, Guid.NewGuid(), "MANUTENCAO", "Manutenção", actorId);
        var repository = new FakeWorkPermitRepository(companyId, [WorkPermitRoleCodes.Requester], permit);
        var service = new WorkPermitService(repository, new NotReadyService());

        await service.LinkDocumentAsync(permit.Id, new LinkWorkPermitDocumentRequest(Guid.NewGuid(), permit.Version), actorId);

        Assert.Single(repository.DocumentLinks);
        Assert.Equal(permit.Id, repository.DocumentLinks[0].EntityId);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task ReleaseAsync_ShouldFailClosed_WhenReadinessCannotBeVerified()
    {
        var companyId = Guid.NewGuid(); var actorId = Guid.NewGuid();
        var repository = new FakeWorkPermitRepository(companyId, [WorkPermitRoleCodes.Releaser]);
        repository.Appointment.Confirm(actorId, actorId, repository.Appointment.Version);
        var permit = new WorkPermit(companyId, repository.Appointment.Id, "ALTURA", "Manutenção", actorId);
        repository.Permits.Add(permit);
        var service = new WorkPermitService(repository, new NotReadyService(), new EffectiveDesignationRepository());
        var request = new WorkPermitReleaseRequest(permit.Version, "Condições demonstrativas", repository.Appointment.StartsAt, repository.Appointment.EndsAt, true);

        await Assert.ThrowsAsync<WorkPermitNotReadyException>(() => service.ReleaseAsync(permit.Id, actorId, request));
        Assert.Equal(WorkPermitStatus.UnderReview, permit.Status);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task ReleaseAsync_ShouldRejectRoleNotAssignedToCompany()
    {
        var companyId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var permit = new WorkPermit(companyId, Guid.NewGuid(), "MANUTENCAO", "Manutenção", actorId);
        var repository = new FakeWorkPermitRepository(companyId, roles: [], permit);
        var service = new WorkPermitService(repository, new NotReadyService());

        await Assert.ThrowsAsync<WorkPermitForbiddenException>(() => service.ReleaseAsync(permit.Id, actorId, new(permit.Version, "Condições", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), true)));

        Assert.Equal(WorkPermitStatus.UnderReview, permit.Status);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Release_ShouldRejectStaleVersionWithoutSavingOrCreatingDecision()
    {
        var data = Safety();
        await Assert.ThrowsAsync<DomainException>(() => data.Service.ReleaseAsync(data.Permit.Id, data.Actor,
            new(0, "Condições", data.Repository.Appointment.StartsAt, data.Repository.Appointment.EndsAt, true)));
        Assert.Equal(0, data.Repository.SaveCount); Assert.Null(data.Permit.ReleasedAt); Assert.Single(data.Permit.Transitions);
    }

    [Fact]
    public async Task Release_ShouldRejectPartialDesignationBeforeReadinessOrMutation()
    {
        var data = Safety(); data.Designations.Effective = false;
        await Assert.ThrowsAsync<WorkPermitForbiddenException>(() => Release(data));
        Assert.Equal(0, data.Repository.SaveCount); Assert.Null(data.Permit.ReleasedAt);
        Assert.NotEmpty(data.Designations.SeenServices);
    }

    [Fact]
    public async Task Release_RequiresExplicitConfirmationAndAppointmentContainedWindow()
    {
        var data = Safety();
        await Assert.ThrowsAsync<ArgumentException>(() => data.Service.ReleaseAsync(data.Permit.Id, data.Actor,
            new(data.Permit.Version, "Condições", data.Repository.Appointment.StartsAt, data.Repository.Appointment.EndsAt, false)));
        await Assert.ThrowsAsync<ArgumentException>(() => data.Service.ReleaseAsync(data.Permit.Id, data.Actor,
            new(data.Permit.Version, "Condições", data.Repository.Appointment.StartsAt, data.Repository.Appointment.EndsAt.AddHours(1), true)));
        Assert.Null(data.Permit.AuthorizedFrom); Assert.Equal(0, data.Repository.SaveCount);
    }

    [Fact]
    public async Task Start_RevalidatesReadinessEvenAfterSuccessfulRelease()
    {
        var data = Safety(); await Release(data); data.Readiness.Ready = false;
        var version = data.Permit.Version;
        await Assert.ThrowsAsync<WorkPermitNotReadyException>(() => data.Service.StartAsync(data.Permit.Id, data.Actor, version));
        Assert.Equal(WorkPermitStatus.Released, data.Permit.Status); Assert.Equal(version, data.Permit.Version);
        Assert.Equal(1, data.Repository.SaveCount); Assert.Null(data.Permit.StartedAt);
    }

    [Fact]
    public async Task CompletionWithoutConfiguredClosingQuestionsFailsClosed()
    {
        var data = Safety(); await Release(data); await data.Service.StartAsync(data.Permit.Id, data.Actor, data.Permit.Version);
        await Assert.ThrowsAsync<WorkPermitNotReadyException>(() => data.Service.CompleteAsync(data.Permit.Id, data.Actor,
            new(data.Permit.Version, "Executado", DateTimeOffset.UtcNow, [])));
        Assert.Equal(WorkPermitStatus.InProgress, data.Permit.Status); Assert.Null(data.Permit.CompletedAt);
    }

    [Fact]
    public async Task ObservationAndCompletionPreservePerformedWorkAndQueueReviewWithoutApproval()
    {
        var data = Safety(); var opening = Guid.NewGuid(); var closing = Guid.NewGuid();
        var template = new WorkPermitTemplateVersion(data.Permit.CompanyId, Guid.NewGuid(), "Exemplo", 1, ["ALTURA"],
            [new(opening, "Preparação", "BOOLEAN", true, true)], "Exemplo", true, data.Actor,
            [new(closing, "Encerramento fictício", "BOOLEAN", true, true)]);
        data.Repository.Templates.Add(template); data.Permit.PinTemplates([template], data.Actor);
        await Release(data); await data.Service.StartAsync(data.Permit.Id, data.Actor, data.Permit.Version);
        await data.Service.AddObservationAsync(data.Permit.Id, data.Actor, new(data.Permit.Version, "Execução acompanhada"));
        var version = data.Permit.Version;
        await Assert.ThrowsAsync<WorkPermitNotReadyException>(() => data.Service.CompleteAsync(data.Permit.Id, data.Actor,
            new(version, "Realizado", DateTimeOffset.UtcNow, [new(closing, "false")])));
        Assert.Equal(version, data.Permit.Version);
        var result = await data.Service.CompleteAsync(data.Permit.Id, data.Actor,
            new(version, "Atividade efetivamente realizada", DateTimeOffset.UtcNow, [new(closing, "true")]));
        Assert.Equal(WorkPermitStatus.Completed, result.Status); Assert.Equal("AGUARDANDO_REVISAO", result.ReviewStatus);
        Assert.Equal("Atividade efetivamente realizada", result.CompletionSummary);
        Assert.Single(result.Observations!); Assert.Single(result.ClosingAnswers!); Assert.NotNull(result.ActualEnd);
    }

    [Fact]
    public async Task SearchCountsOnlyAuthorizedCompanyBeforePaginationAndRejectsForgedCompany()
    {
        var actor = Guid.NewGuid(); var company = Guid.NewGuid(); var other = Guid.NewGuid();
        var repository = new FakeWorkPermitRepository(company);
        for (var index = 0; index < 12; index++) repository.Permits.Add(new(company, Guid.NewGuid(), "ALTURA", "Alvo " + index, actor));
        for (var index = 0; index < 7; index++) repository.Permits.Add(new(other, Guid.NewGuid(), "ALTURA", "Alvo externo " + index, actor));
        var service = new WorkPermitService(repository, new NotReadyService());
        var page = await service.SearchAsync(new(Q: "Alvo", Page: 2, PageSize: 5), actor);
        Assert.Equal(12, page.Total); Assert.Equal(5, page.Items.Count); Assert.All(page.Items, item => Assert.Equal(company, item.CompanyId));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SearchAsync(new(CompanyId: other), actor));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(new(Page: 0), actor));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(new(PageSize: 101), actor));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(new(Status: "inventado"), actor));
    }

    private static SafetyData Safety()
    {
        var actor = Guid.NewGuid(); var company = Guid.NewGuid();
        var repository = new FakeWorkPermitRepository(company, [WorkPermitRoleCodes.Releaser, WorkPermitRoleCodes.Operator]);
        repository.UseCurrentWindow(); repository.Appointment.Confirm(actor, actor, repository.Appointment.Version);
        var permit = new WorkPermit(company, repository.Appointment.Id, "ALTURA", "Fictício", actor); repository.Permits.Add(permit);
        var readiness = new ToggleReadiness(); var designations = new EffectiveDesignationRepository();
        return new(actor, repository, permit, readiness, designations, new WorkPermitService(repository, readiness, designations));
    }
    private static Task<WorkPermitResponse> Release(SafetyData data) => data.Service.ReleaseAsync(data.Permit.Id, data.Actor,
        new(data.Permit.Version, "Condições fictícias", data.Repository.Appointment.StartsAt, data.Repository.Appointment.EndsAt, true));
    private sealed record SafetyData(Guid Actor, FakeWorkPermitRepository Repository, WorkPermit Permit, ToggleReadiness Readiness,
        EffectiveDesignationRepository Designations, WorkPermitService Service);
    private sealed class ToggleReadiness : IWorkPermitReadinessService
    {
        public bool Ready { get; set; } = true;
        public Task<bool> IsReadyAsync(WorkPermit permit, CancellationToken cancellationToken = default) => Task.FromResult(Ready);
    }

    private sealed class EffectiveDesignationRepository : IOperationalDesignationRepository
    {
        public bool Effective { get; set; } = true;
        public Guid[] SeenServices { get; private set; } = [];
        public Task<bool> HasEffectiveDesignationAsync(Guid actorId, Guid companyId, Guid locationId, IReadOnlyCollection<Guid> services, string type, DateTimeOffset instant, CancellationToken cancellationToken = default) { SeenServices = services.ToArray(); return Task.FromResult(Effective); }
        public Task<IReadOnlyList<Guid>> GetScopedCompanyIdsAsync(Guid actorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasRoleInCompanyAsync(Guid actorId, Guid companyId, string roleCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Guid?> GetActiveWorkerCompanyIdAsync(Guid personId, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> AreActiveLocationsInCompanyAsync(IReadOnlyCollection<Guid> locationIds, Guid companyId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(OperationalDesignation designation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OperationalDesignation?> FindDuplicateAsync(OperationalDesignation designation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OperationalDesignation?> GetAsync(Guid id, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<OperationalDesignation>> ListAsync(IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NotReadyService : IWorkPermitReadinessService
    {
        public Task<bool> IsReadyAsync(WorkPermit permit, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeWorkPermitRepository : IWorkPermitRepository
    {
        private readonly Guid[] _companyIds;
        private readonly string[] _roles;

        public List<WorkPermit> Permits { get; } = [];
        public List<DocumentLink> DocumentLinks { get; } = [];
        public List<WorkPermitTemplateVersion> Templates { get; } = [];
        public WorkPermitAppointment Appointment { get; private set; }
        public int SaveCount { get; private set; }

        public FakeWorkPermitRepository(Guid companyId, string[]? roles = null, params WorkPermit[] permits)
        {
            _companyIds = [companyId];
            _roles = roles ?? [];
            Appointment = new WorkPermitAppointment(companyId, "Agenda", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(2), Guid.NewGuid(), Guid.NewGuid());
            Permits.AddRange(permits);
        }

        public void UseCurrentWindow() => Appointment = new WorkPermitAppointment(_companyIds[0], "Em andamento",
            DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), Guid.NewGuid());

        public Task AddAsync(WorkPermit permit, CancellationToken cancellationToken = default)
        {
            Permits.Add(permit);
            return Task.CompletedTask;
        }

        public Task AddAppointmentAsync(WorkPermitAppointment appointment, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WorkPermitAppointment?> GetAppointmentAsync(Guid appointmentId, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<WorkPermitAppointment?>(Appointment.Id == appointmentId && companyIds.Contains(Appointment.CompanyId) ? Appointment : null);
        public Task<bool> IsActiveDocumentTypeAsync(Guid documentTypeId, CancellationToken cancellationToken = default) => Task.FromResult(documentTypeId != Guid.Empty);
        public Task<bool> HasWorkPermitForAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Permits.Any(permit => permit.AppointmentId == appointmentId));
        public Task<bool> IsActiveLocationInCompanyAsync(Guid locationId, Guid companyId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> IsActiveCompanyUserAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_companyIds.Contains(companyId));
        public Task AddParticipantAsync(WorkPermitParticipant participant, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WorkPermitParticipant?> GetParticipantAsync(Guid permitId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Permits.FirstOrDefault(permit => permit.Id == permitId)?.Participants.FirstOrDefault(participant => participant.UserId == userId));
        public Task AddRequirementAsync(WorkPermitDocumentRequirement requirement, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddTemplateVersionAsync(WorkPermitTemplateVersion template, CancellationToken cancellationToken = default)
        {
            Templates.Add(template);
            return Task.CompletedTask;
        }
        public Task<WorkPermitTemplateVersion?> GetTemplateVersionAsync(Guid templateVersionId, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(Templates.FirstOrDefault(template => template.Id == templateVersionId && companyIds.Contains(template.CompanyId)));
        public Task<IReadOnlyList<WorkPermitTemplateVersion>> ListTemplateVersionsAsync(IReadOnlyCollection<Guid> companyIds, Guid? templateId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkPermitTemplateVersion>>(Templates.Where(template => companyIds.Contains(template.CompanyId)
                && (!templateId.HasValue || template.TemplateId == templateId.Value)).ToArray());
        public Task<IReadOnlyList<WorkPermitDocumentRequirement>> ListRequirementsAsync(Guid companyId, string serviceCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkPermitDocumentRequirement>>([]);
        public Task<bool> IsActiveCompanyDocumentAsync(Guid documentId, Guid companyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(documentId != Guid.Empty && _companyIds.Contains(companyId));
        public Task AddDocumentLinkAsync(DocumentLink documentLink, CancellationToken cancellationToken = default)
        {
            DocumentLinks.Add(documentLink);
            return Task.CompletedTask;
        }
        public Task<bool> IsReadyAsync(WorkPermit permit, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<IReadOnlyList<Guid>> GetAccessibleCompanyIdsAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(_companyIds);

        public Task<bool> HasRoleInCompanyAsync(Guid userId, Guid companyId, string roleCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(_companyIds.Contains(companyId) && _roles.Contains(roleCode));

        public Task<WorkPermit?> GetByIdAsync(Guid permitId, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(Permits.FirstOrDefault(permit => permit.Id == permitId && companyIds.Contains(permit.CompanyId)));

        public Task<IReadOnlyList<WorkPermit>> ListAsync(IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkPermit>>(Permits.Where(permit => companyIds.Contains(permit.CompanyId)).ToArray());

        public Task<WorkPermitSearchResult> SearchAsync(WorkPermitSearchRequest request, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default)
        {
            var query = Permits.Where(permit => companyIds.Contains(permit.CompanyId));
            if (!string.IsNullOrEmpty(request.Search)) query = query.Where(permit => permit.Title.Contains(request.Search, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(request.Status)) query = query.Where(permit => permit.Status == request.Status);
            var array = query.OrderBy(permit => permit.Id).ToArray();
            return Task.FromResult(new WorkPermitSearchResult(array.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArray(), array.Length, request.Page, request.PageSize));
        }

        public Task<IReadOnlyList<SESMT.Domain.Modules.WorkPermits.Entities.WorkPermitTransition>> ListHistoryAsync(Guid permitId, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SESMT.Domain.Modules.WorkPermits.Entities.WorkPermitTransition>>(
                Permits.Where(permit => permit.Id == permitId && companyIds.Contains(permit.CompanyId)).SelectMany(permit => permit.Transitions).ToArray());

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
