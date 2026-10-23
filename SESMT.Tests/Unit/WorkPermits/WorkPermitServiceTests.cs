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
    public async Task AddParticipantAsync_ShouldAssignCompanyUserAndPersist()
    {
        var companyId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var permit = new WorkPermit(companyId, Guid.NewGuid(), "MANUTENCAO", "Manutenção", actorId);
        var repository = new FakeWorkPermitRepository(companyId, [WorkPermitRoleCodes.Requester], permit);
        var service = new WorkPermitService(repository, new NotReadyService());

        var result = await service.AddParticipantAsync(permit.Id,
            new AddWorkPermitParticipantRequest(actorId, "OPERADOR"), actorId);

        Assert.Equal(actorId, result.UserId);
        Assert.Single(permit.Participants);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task AcknowledgeAsync_ShouldRecordAuthenticatedParticipantOnly()
    {
        var companyId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var permit = new WorkPermit(companyId, Guid.NewGuid(), "MANUTENCAO", "Manutenção", actorId);
        permit.AddParticipant(new WorkPermitParticipant(permit.Id, actorId, "OPERADOR", actorId), actorId);
        var repository = new FakeWorkPermitRepository(companyId, permits: [permit]);
        var service = new WorkPermitService(repository, new NotReadyService());

        var result = await service.AcknowledgeAsync(permit.Id, actorId);

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

        await service.LinkDocumentAsync(permit.Id, new LinkWorkPermitDocumentRequest(Guid.NewGuid()), actorId);

        Assert.Single(repository.DocumentLinks);
        Assert.Equal(permit.Id, repository.DocumentLinks[0].EntityId);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task ReleaseAsync_ShouldFailClosed_WhenReadinessCannotBeVerified()
    {
        var companyId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var permit = new WorkPermit(companyId, Guid.NewGuid(), "MANUTENCAO", "Manutenção", actorId);
        var repository = new FakeWorkPermitRepository(companyId, [WorkPermitRoleCodes.Releaser], permit);
        var service = new WorkPermitService(repository, new NotReadyService());

        await Assert.ThrowsAsync<WorkPermitNotReadyException>(() => service.ReleaseAsync(permit.Id, actorId));

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

        await Assert.ThrowsAsync<WorkPermitForbiddenException>(() => service.ReleaseAsync(permit.Id, actorId));

        Assert.Equal(WorkPermitStatus.UnderReview, permit.Status);
        Assert.Equal(0, repository.SaveCount);
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
        public WorkPermitAppointment Appointment { get; }
        public int SaveCount { get; private set; }

        public FakeWorkPermitRepository(Guid companyId, string[]? roles = null, params WorkPermit[] permits)
        {
            _companyIds = [companyId];
            _roles = roles ?? [];
            Appointment = new WorkPermitAppointment(companyId, "Agenda", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(2), Guid.NewGuid());
            Permits.AddRange(permits);
        }

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
