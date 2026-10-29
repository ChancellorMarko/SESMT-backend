using SESMT.Domain.Exceptions;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Tests.Unit.WorkPermits;

public sealed class WorkPermitReadinessPolicyTests
{
    private readonly Guid _actor = Guid.NewGuid(), _personA = Guid.NewGuid(), _personB = Guid.NewGuid();
    private readonly Guid _typeA = Guid.NewGuid(), _typeB = Guid.NewGuid();
    private readonly DateOnly _day = new(2030, 1, 10);

    [Fact]
    public void ComposedServicesRequireEveryMatrixAndEvidenceForEveryParticipant()
    {
        var data = Ready();
        Assert.True(Evaluate(data).Ready);
        var missingMatrix = data with { Requirements = [data.Requirements[0]] };
        Assert.Contains(Evaluate(missingMatrix).Issues, issue => issue.Code == "MATRIZ_AUSENTE" && issue.ServiceCode == "CONFINADO");
        var missingPerson = data with { Evidence = data.Evidence.Where(item => item.ParticipantUserId != _personB).ToArray() };
        var result = Evaluate(missingPerson);
        Assert.False(result.Ready);
        Assert.Equal(2, result.Issues.Count(issue => issue.ParticipantUserId == _personB && issue.Code == "DOCUMENTO_AUSENTE"));
    }

    [Theory]
    [InlineData(DocumentVersionState.InReview, "AGUARDANDO_CONFERENCIA")]
    [InlineData(DocumentVersionState.Rejected, "DOCUMENTO_REPROVADO")]
    public void UploadOrRejectedVersionDoesNotSatisfyRequirement(string state, string reason)
    {
        var data = Ready();
        var changed = data.Evidence.Select(item => item with { ReviewedCurrentVersion = false, ReviewState = state }).ToArray();
        Assert.All(Evaluate(data with { Evidence = changed }).Issues, issue => Assert.Equal(reason, issue.Code));
    }

    [Fact]
    public void ExpiryAndFutureValidityAreEvaluatedAgainstTheApplicablePeriod()
    {
        var data = Ready();
        var expired = data.Evidence.Select(item => item with { ValidUntil = _day.AddDays(-1) }).ToArray();
        Assert.All(Evaluate(data with { Evidence = expired }).Issues, issue => Assert.Equal("DOCUMENTO_FORA_DA_VALIDADE", issue.Code));
        var future = data.Evidence.Select(item => item with { ValidFrom = _day.AddDays(1) }).ToArray();
        Assert.False(Evaluate(data with { Evidence = future }).Ready);
        Assert.True(Evaluate(data with { Evidence = data.Evidence.Select(item => item with { ValidFrom = null, ValidUntil = null }).ToArray() }).Ready);
    }

    [Fact]
    public void MissingFileAndRevokedCompanyScopeAreIndependentBlocks()
    {
        var data = Ready();
        Assert.Contains(Evaluate(data with { Evidence = data.Evidence.Select(item => item with { FileAvailable = false }).ToArray() }).Issues,
            issue => issue.Code == "ARQUIVO_INDISPONIVEL");
        Assert.Contains(Evaluate(data with { ActiveUsers = new HashSet<Guid> { _personA } }).Issues,
            issue => issue.Code == "PARTICIPANTE_FORA_DO_CONTEXTO" && issue.ParticipantUserId == _personB);
        Assert.Contains(WorkPermitReadinessPolicy.Evaluate(data.Permit, false, true, data.Participants, data.ActiveUsers,
            data.Requirements, data.Evidence, _day, _day).Issues, issue => issue.Code == "AGENDAMENTO_NAO_CONFIRMADO");
    }

    [Fact]
    public void ContextChangeInvalidatesCurrentAcknowledgementButConservesItsEvidence()
    {
        var data = Ready();
        data.Permit.SaveChecklist([], _actor);
        var assessment = Evaluate(data);
        Assert.Equal(2, assessment.Issues.Count(issue => issue.Code == "CIENCIA_PENDENTE"));
        Assert.All(data.Participants, person => Assert.Single(person.Acknowledgements));
    }

    [Fact]
    public void CollectorAndParticipantAreRecordedSeparatelyAndConfirmationIsRequired()
    {
        var permit = new WorkPermit(Guid.NewGuid(), Guid.NewGuid(), "QUENTE", "Teste", _actor);
        var person = new WorkPermitParticipant(permit.Id, _personA, "Participante", _actor);
        Assert.Throws<ArgumentException>(() => person.Acknowledge(_actor, 1, "Nome", "Identificação assistida fictícia", "Texto apresentado", false));
        Assert.Empty(person.Acknowledgements);
        person.Acknowledge(_actor, 1, "Nome", "Identificação assistida fictícia", "Texto apresentado", true);
        var item = Assert.Single(person.Acknowledgements);
        Assert.Equal(_actor, item.CollectorId); Assert.Equal(_personA, item.ParticipantUserId);
        Assert.Equal("Texto apresentado", item.Declaration); Assert.True(person.HasAcknowledged(1));
    }

    [Fact]
    public void ReschedulingInvalidatesThePresentedContextWithoutErasingEarlierAcknowledgements()
    {
        var data = Ready();
        var appointment = new WorkPermitAppointment(data.Permit.CompanyId, "Fictício", DateTimeOffset.UtcNow.AddHours(1),
            DateTimeOffset.UtcNow.AddHours(2), _actor);
        var template = new WorkPermitTemplateVersion(data.Permit.CompanyId, Guid.NewGuid(), "Exemplo", 1, ["QUENTE"],
            [new(Guid.NewGuid(), "Conferir", "BOOLEAN", true, true)], "Declaração", true, _actor);
        var presented = WorkPermitDeclarationContent.Create(appointment, [template]);
        foreach (var person in data.Participants)
            person.Acknowledge(person.UserId, data.Permit.ContentVersion, "Nome", "Identificação fictícia", presented, true);
        Assert.True(WorkPermitReadinessPolicy.Evaluate(data.Permit, true, true, data.Participants, data.ActiveUsers,
            data.Requirements, data.Evidence, _day, _day, presented).Ready);
        var contentVersion = data.Permit.ContentVersion;
        appointment.Reschedule(appointment.StartsAt.AddDays(1), appointment.EndsAt.AddDays(1), "Nova programação", _actor, appointment.Version);
        var current = WorkPermitDeclarationContent.Create(appointment, [template]);
        var result = WorkPermitReadinessPolicy.Evaluate(data.Permit, true, true, data.Participants, data.ActiveUsers,
            data.Requirements, data.Evidence, _day, _day, current);
        Assert.Equal(contentVersion, data.Permit.ContentVersion);
        Assert.Equal(2, result.Issues.Count(issue => issue.Code == "CIENCIA_PENDENTE"));
        Assert.All(data.Participants, person => Assert.Equal(presented, person.Acknowledgements.Last().Declaration));
    }

    [Fact]
    public void FutureOrExpiredAuthorizationWindowDoesNotStartExecution()
    {
        var permit = new WorkPermit(Guid.NewGuid(), Guid.NewGuid(), "QUENTE", "Teste", _actor);
        var from = DateTimeOffset.UtcNow.AddHours(1); var until = from.AddHours(1);
        permit.Release(_actor, true, from, until, "Condições", true);
        Assert.Throws<DomainException>(() => permit.Start(_actor, true));
        Assert.Throws<DomainException>(() => permit.EnsureAuthorizedWindow(until.AddTicks(1)));
        Assert.Null(permit.StartedAt);
    }

    private WorkPermitReadinessAssessment Evaluate(Data data) => WorkPermitReadinessPolicy.Evaluate(data.Permit, true, true,
        data.Participants, data.ActiveUsers, data.Requirements, data.Evidence, _day, _day);
    private Data Ready()
    {
        var permit = new WorkPermit(Guid.NewGuid(), Guid.NewGuid(), "QUENTE,CONFINADO", "Fictício", _actor);
        var people = new[] { _personA, _personB }.Select(id => new WorkPermitParticipant(permit.Id, id, "Participante", _actor)).ToArray();
        foreach (var person in people) permit.AddParticipant(person, _actor);
        foreach (var person in people) person.Acknowledge(person.UserId, permit.ContentVersion, "Participante", "Sessão fictícia", "Declaração", true);
        return new(permit, people, new HashSet<Guid> { _personA, _personB },
            [new(permit.CompanyId, "QUENTE", _typeA, "Fictício", _actor), new(permit.CompanyId, "CONFINADO", _typeB, "Fictício", _actor)],
            people.SelectMany(person => new[] { _typeA, _typeB }.Select(type => new WorkPermitEvidence(person.UserId, type, true, true, _day, _day, DocumentVersionState.Current))).ToArray());
    }
    private sealed record Data(WorkPermit Permit, WorkPermitParticipant[] Participants, IReadOnlySet<Guid> ActiveUsers,
        WorkPermitDocumentRequirement[] Requirements, WorkPermitEvidence[] Evidence);
}
