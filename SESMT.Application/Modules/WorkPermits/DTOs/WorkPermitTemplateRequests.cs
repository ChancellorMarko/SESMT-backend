using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record CreateWorkPermitTemplateRequest(
    Guid CompanyId,
    [property: Required, StringLength(120, MinimumLength = 1), JsonPropertyName("nome")] string Name,
    [property: MinLength(1), JsonPropertyName("servicoIds")] IReadOnlyList<Guid> ServiceIds,
    [property: MinLength(1), JsonPropertyName("perguntas")] IReadOnlyList<WorkPermitChecklistQuestion> Questions,
    [property: Required, StringLength(2000, MinimumLength = 1), JsonPropertyName("declaracao")] string Declaration,
    [property: JsonPropertyName("ilustrativo")] bool Illustrative);

public sealed record CreateWorkPermitTemplateVersionRequest(
    [property: MinLength(1), JsonPropertyName("servicoIds")] IReadOnlyList<Guid> ServiceIds,
    [property: MinLength(1), JsonPropertyName("perguntas")] IReadOnlyList<WorkPermitChecklistQuestion> Questions,
    [property: Required, StringLength(2000, MinimumLength = 1), JsonPropertyName("declaracao")] string Declaration,
    [property: JsonPropertyName("ilustrativo")] bool Illustrative);

public sealed record WorkPermitTemplateResponse(Guid Id, Guid TemplateId, Guid CompanyId,
    [property: JsonPropertyName("nome")] string Name,
    [property: JsonPropertyName("versao")] int Version,
    [property: JsonPropertyName("servicoIds")] IReadOnlyList<Guid> ServiceIds,
    IReadOnlyList<string> ServiceCodes,
    [property: JsonPropertyName("perguntas")] IReadOnlyList<WorkPermitChecklistQuestion> Questions,
    [property: JsonPropertyName("declaracao")] string Declaration,
    [property: JsonPropertyName("ilustrativo")] bool Illustrative)
{
    public static WorkPermitTemplateResponse From(WorkPermitTemplateVersion template) =>
        new(template.Id, template.TemplateId, template.CompanyId, template.Name, template.Version,
            WorkPermitServiceCatalogEntry.All.Where(service => template.ServiceCodes.Contains(service.Code)).Select(service => service.Id).ToArray(),
            template.ServiceCodes, template.Questions, template.Declaration, template.Illustrative);
}

public sealed record SaveWorkPermitChecklistRequest(IReadOnlyList<WorkPermitChecklistAnswer> Answers);
