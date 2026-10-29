using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record PinnedTemplateVersionRequest(
    [property: JsonPropertyName("templateId")] Guid TemplateId,
    [Range(1, int.MaxValue)] [property: JsonPropertyName("versao")] int Version);

public sealed record OpenWorkPermitRequest(
    [Required, MinLength(1)] [property: JsonPropertyName("templateVersoes")] IReadOnlyList<PinnedTemplateVersionRequest> TemplateVersions,
    [Required, MinLength(1)] [property: JsonPropertyName("participanteIds")] IReadOnlyList<Guid> ParticipantIds);

public sealed record ChecklistAnswerRequest(
    [property: JsonPropertyName("perguntaId")] Guid QuestionId,
    [property: JsonPropertyName("valor")] JsonElement Value,
    [property: JsonPropertyName("justificativa")] string? Justification = null);

public sealed record ChecklistAnswerResponse(
    [property: JsonPropertyName("perguntaId")] Guid QuestionId,
    [property: JsonPropertyName("valor")] JsonElement Value,
    [property: JsonPropertyName("justificativa")] string? Justification);

public sealed record PatchWorkPermitChecklistRequest(
    [Range(1, int.MaxValue)] [property: JsonPropertyName("versaoEsperada")] int ExpectedVersion,
    [Required] [property: JsonPropertyName("respostas")] IReadOnlyList<ChecklistAnswerRequest> Answers);

public sealed record WorkPermitChecklistResponse(
    Guid Id, string Numero,
    [property: JsonPropertyName("statusOperacional")] string OperationalStatus,
    [property: JsonPropertyName("statusRevisao")] string ReviewStatus,
    int Versao,
    [property: JsonPropertyName("servicoIds")] IReadOnlyList<Guid> ServiceIds,
    IReadOnlyList<Guid> ParticipanteIds,
    IReadOnlyList<Guid> TemplateVersionIds,
    IReadOnlyList<ChecklistAnswerResponse> Respostas)
{
    public static WorkPermitChecklistResponse From(WorkPermitResponse permit, IReadOnlyList<Guid> serviceIds,
        IReadOnlyList<ChecklistAnswerResponse>? answers = null) =>
        new(permit.Id, permit.Id.ToString("D"), permit.Status.ToLowerInvariant(), permit.ReviewStatus.ToLowerInvariant(), permit.Version,
            serviceIds, permit.Participants.Select(participant => participant.UserId).ToArray(),
            permit.TemplateVersionIds, answers ?? []);
}
