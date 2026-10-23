using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record PinnedTemplateVersionRequest(
    [property: JsonPropertyName("templateId")] Guid TemplateId,
    [property: Range(1, int.MaxValue), JsonPropertyName("versao")] int Version);

public sealed record OpenWorkPermitRequest(
    [property: Required, MinLength(1), JsonPropertyName("templateVersoes")] IReadOnlyList<PinnedTemplateVersionRequest> TemplateVersions,
    [property: Required, MinLength(1), JsonPropertyName("participanteIds")] IReadOnlyList<Guid> ParticipantIds);

public sealed record ChecklistAnswerRequest(
    [property: JsonPropertyName("perguntaId")] Guid QuestionId,
    [property: JsonPropertyName("valor")] JsonElement Value,
    [property: JsonPropertyName("justificativa")] string? Justification = null);

public sealed record ChecklistAnswerResponse(
    [property: JsonPropertyName("perguntaId")] Guid QuestionId,
    [property: JsonPropertyName("valor")] JsonElement Value,
    [property: JsonPropertyName("justificativa")] string? Justification);

public sealed record PatchWorkPermitChecklistRequest(
    [property: Range(1, int.MaxValue), JsonPropertyName("versaoEsperada")] int ExpectedVersion,
    [property: Required, JsonPropertyName("respostas")] IReadOnlyList<ChecklistAnswerRequest> Answers);

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
        new(permit.Id, permit.Id.ToString("D"), "em_verificacao", "nao_enviada", permit.Version,
            serviceIds, permit.Participants.Select(participant => participant.UserId).ToArray(),
            permit.TemplateVersionIds, answers ?? []);
}
