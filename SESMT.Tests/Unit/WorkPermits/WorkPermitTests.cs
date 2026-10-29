using SESMT.Domain.Exceptions;
using SESMT.Domain.Modules.WorkPermits.Constants;
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Tests.Unit.WorkPermits;

public class WorkPermitTests
{
    [Fact]
    public void DocumentLink_AcceptsWorkPermitAsLinkedEntity()
    {
        var link = new DocumentLink(Guid.NewGuid(), LinkedEntityType.WorkPermit, Guid.NewGuid());

        Assert.Equal(LinkedEntityType.WorkPermit, link.EntityType);
    }

    [Fact]
    public void DocumentLink_RejectsUnknownLinkedEntityType()
    {
        Assert.Throws<ArgumentException>(() => new DocumentLink(Guid.NewGuid(), "PT_DESCONHECIDA", Guid.NewGuid()));
    }

    private static readonly Guid ActorId = Guid.NewGuid();

    private static WorkPermit CreatePermit() => new(Guid.NewGuid(), Guid.NewGuid(), "MANUTENCAO", "Manutenção elétrica", ActorId);

    [Fact]
    public void ServiceCatalog_ContainsTheFiveSupportedServices()
    {
        Assert.Equal(new[] { "ALTURA", "QUENTE", "FRIO", "CONFINADO", "ELETRICIDADE" },
            WorkPermitServiceCatalogEntry.All.Select(service => service.Code));
        Assert.Equal(5, WorkPermitServiceCatalogEntry.All.Select(service => service.Id).Distinct().Count());
    }

    [Fact]
    public void TemplateVersion_RequiresIllustrativeContentUntilFormalApprovalExists()
    {
        var question = new WorkPermitChecklistQuestion(Guid.NewGuid(), "Verificação de exemplo", "BOOLEAN", true, true);

        Assert.Throws<ArgumentException>(() => new WorkPermitTemplateVersion(Guid.NewGuid(), Guid.NewGuid(), "Altura", 1,
            ["ALTURA"], [question], "Declaração de exemplo", false, ActorId));
    }

    [Fact]
    public void ChecklistQuestion_RejectsMissingRequiredAndBlockingAnswers()
    {
        var question = new WorkPermitChecklistQuestion(Guid.NewGuid(), "Verificação", "BOOLEAN", true, true);

        Assert.False(question.HasValidValue(null));
        Assert.True(question.HasValidValue("true"));
        Assert.True(question.Blocks("false"));
        Assert.False(question.Blocks("true"));
    }

    [Fact]
    public void ChecklistReadiness_RequiresAllMandatoryAnswersAndRejectsBlockingAnswers()
    {
        var companyId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var required = new WorkPermitChecklistQuestion(Guid.NewGuid(), "Obrigatória", "BOOLEAN", true, false);
        var blocking = new WorkPermitChecklistQuestion(Guid.NewGuid(), "Impeditiva", "BOOLEAN", true, true);
        var template = new WorkPermitTemplateVersion(companyId, Guid.NewGuid(), "Quente", 1,
            ["QUENTE"], [required, blocking], "Exemplo", true, actorId);

        Assert.False(WorkPermitTemplateVersion.IsChecklistReady([template], []));
        Assert.False(WorkPermitTemplateVersion.IsChecklistReady([template],
            [new(required.Id, "true"), new(blocking.Id, "false")]));
        Assert.True(WorkPermitTemplateVersion.IsChecklistReady([template],
            [new(required.Id, "true"), new(blocking.Id, "true")]));
    }

    [Fact]
    public void Permit_PinsComposedTemplateVersionsAndKeepsTheirIds()
    {
        var permit = CreatePermit();
        var heightQuestion = new WorkPermitChecklistQuestion(Guid.NewGuid(), "Altura", "BOOLEAN", true, true);
        var hotWorkQuestion = new WorkPermitChecklistQuestion(Guid.NewGuid(), "Quente", "BOOLEAN", true, true);
        var height = new WorkPermitTemplateVersion(permit.CompanyId, Guid.NewGuid(), "Altura", 1,
            ["ALTURA"], [heightQuestion], "Exemplo", true, ActorId);
        var hotWork = new WorkPermitTemplateVersion(permit.CompanyId, Guid.NewGuid(), "Quente", 1,
            ["QUENTE"], [hotWorkQuestion], "Exemplo", true, ActorId);

        permit.PinTemplates([height, hotWork], ActorId);

        Assert.Equal("ALTURA,QUENTE", permit.ServiceCode);
        Assert.Equal(new[] { height.Id, hotWork.Id }, permit.TemplateVersionIds);
        Assert.Equal(2, permit.ContentVersion);
    }

    [Fact]
    public void ParticipantAcknowledgement_ShouldBeIndividualAndVersioned()
    {
        var permit = CreatePermit();
        var participantId = Guid.NewGuid();
        var participant = new WorkPermitParticipant(permit.Id, participantId, "OPERADOR", ActorId);
        permit.AddParticipant(participant, ActorId);

        Assert.Throws<InvalidOperationException>(() => participant.Acknowledge(ActorId, permit.ContentVersion));
        participant.Acknowledge(participantId, permit.ContentVersion);

        Assert.NotNull(participant.AcknowledgedAt);
        Assert.Equal(permit.ContentVersion, participant.AcknowledgedVersion);
    }

    [Fact]
    public void Appointment_ShouldRejectInvalidTimeRange()
    {
        var startsAt = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => new WorkPermitAppointment(Guid.NewGuid(), "Serviço", startsAt, startsAt, ActorId));
    }

    [Fact]
    public void Constructor_ShouldStartUnderReview_WithoutReleasingWork()
    {
        var permit = CreatePermit();

        Assert.Equal(WorkPermitStatus.UnderReview, permit.Status);
        Assert.Null(permit.ReleasedAt);
        Assert.Equal(1, permit.Version);
        Assert.Collection(permit.Transitions,
            transition =>
            {
                Assert.Null(transition.PreviousStatus);
                Assert.Equal(WorkPermitStatus.UnderReview, transition.NewStatus);
                Assert.Equal("ABERTA", transition.Action);
                Assert.Equal(ActorId, transition.ActorId);
            });
    }

    [Fact]
    public void Release_ShouldRequireReadinessConditions()
    {
        var permit = CreatePermit();

        ReleasePermit(permit);

        Assert.Equal(WorkPermitStatus.Released, permit.Status);
        Assert.Equal(ActorId, permit.ReleasedBy);
        Assert.NotNull(permit.ReleasedAt);
        Assert.Equal(2, permit.Version);
    }

    [Fact]
    public void Release_ShouldRemainUnderReview_WhenReadinessFails()
    {
        var permit = CreatePermit();

        Assert.Throws<DomainException>(() => ReleasePermit(permit, false));

        Assert.Equal(WorkPermitStatus.UnderReview, permit.Status);
        Assert.Equal(1, permit.Version);
    }

    [Fact]
    public void Start_ShouldRevalidateReadiness()
    {
        var permit = CreatePermit();
        ReleasePermit(permit);

        permit.Start(ActorId, true);

        Assert.Equal(WorkPermitStatus.InProgress, permit.Status);
        Assert.Equal(ActorId, permit.StartedBy);
    }

    [Fact]
    public void SuspendAndResume_ShouldRequireReasonAndRevalidate()
    {
        var permit = CreatePermit();
        ReleasePermit(permit);
        permit.Start(ActorId, true);

        Assert.Throws<DomainException>(() => permit.Suspend(ActorId, " "));
        permit.Suspend(ActorId, "Condição insegura identificada");
        permit.Resume(ActorId, true);

        Assert.Equal(WorkPermitStatus.InProgress, permit.Status);
        Assert.Equal("Condição insegura identificada", permit.SuspensionReason);
        Assert.Equal(5, permit.Version);
        Assert.Equal(5, permit.Transitions.Count);
        Assert.Equal("RETOMADA", permit.Transitions.Last().Action);
    }

    [Fact]
    public void Cancel_ShouldRequireReason_AndOnlyBeforeExecution()
    {
        var permit = CreatePermit();

        Assert.Throws<DomainException>(() => permit.Cancel(ActorId, " "));
        permit.Cancel(ActorId, "Atividade cancelada");

        Assert.Equal(WorkPermitStatus.Cancelled, permit.Status);
        Assert.Equal("Atividade cancelada", permit.CancellationReason);
        Assert.Throws<DomainException>(() => ReleasePermit(permit));
    }

    [Fact]
    public void RepeatedRelease_ShouldBeRejected()
    {
        var permit = CreatePermit();
        ReleasePermit(permit);

        Assert.Throws<DomainException>(() => ReleasePermit(permit));
        Assert.Equal(2, permit.Version);
    }

    [Fact]
    public void EveryTransition_ShouldRequireActor()
    {
        var permit = CreatePermit();

        Assert.Throws<DomainException>(() => ReleasePermit(permit, actorId: Guid.Empty));
        Assert.Equal(WorkPermitStatus.UnderReview, permit.Status);
    }
    private static void ReleasePermit(WorkPermit permit, bool ready = true, Guid? actorId = null) =>
        permit.Release(actorId ?? ActorId, ready, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1), "Condições demonstrativas", true);

}
