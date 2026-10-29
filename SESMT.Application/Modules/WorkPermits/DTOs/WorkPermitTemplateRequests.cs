using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record CreateWorkPermitTemplateRequest(
    Guid CompanyId,
    [Required, StringLength(120, MinimumLength = 1)] [property: JsonPropertyName("nome")] string Name,
    [MinLength(1)] [property: JsonPropertyName("servicoIds")] IReadOnlyList<Guid> ServiceIds,
    [MinLength(1)] [property: JsonPropertyName("perguntas")] IReadOnlyList<WorkPermitChecklistQuestion> Questions,
    [Required, StringLength(2000, MinimumLength = 1)] [property: JsonPropertyName("declaracao")] string Declaration,
    [property: JsonPropertyName("ilustrativo")] bool Illustrative,
    IReadOnlyList<WorkPermitChecklistQuestion>? ClosingQuestions = null);

public sealed record CreateWorkPermitTemplateVersionRequest(
    [MinLength(1)] [property: JsonPropertyName("servicoIds")] IReadOnlyList<Guid> ServiceIds,
    [MinLength(1)] [property: JsonPropertyName("perguntas")] IReadOnlyList<WorkPermitChecklistQuestion> Questions,
    [Required, StringLength(2000, MinimumLength = 1)] [property: JsonPropertyName("declaracao")] string Declaration,
    [property: JsonPropertyName("ilustrativo")] bool Illustrative,
    IReadOnlyList<WorkPermitChecklistQuestion>? ClosingQuestions = null);

public sealed record WorkPermitTemplateResponse(Guid Id, Guid TemplateId, Guid CompanyId,
    [property: JsonPropertyName("nome")] string Name,
    [property: JsonPropertyName("versao")] int Version,
    [property: JsonPropertyName("servicoIds")] IReadOnlyList<Guid> ServiceIds,
    IReadOnlyList<string> ServiceCodes,
    [property: JsonPropertyName("perguntas")] IReadOnlyList<WorkPermitChecklistQuestion> Questions,
    [property: JsonPropertyName("declaracao")] string Declaration,
    [property: JsonPropertyName("ilustrativo")] bool Illustrative,
    IReadOnlyList<WorkPermitChecklistQuestion> ClosingQuestions)
{
    public static WorkPermitTemplateResponse From(WorkPermitTemplateVersion template) =>
        new(template.Id, template.TemplateId, template.CompanyId, template.Name, template.Version,
            WorkPermitServiceCatalogEntry.All.Where(service => template.ServiceCodes.Contains(service.Code)).Select(service => service.Id).ToArray(),
            template.ServiceCodes, template.Questions, template.Declaration, template.Illustrative, template.ClosingQuestions);
}

public sealed record SaveWorkPermitChecklistRequest(IReadOnlyList<WorkPermitChecklistAnswer> Answers,
    [Range(1, int.MaxValue)] int ExpectedVersion = 0);
