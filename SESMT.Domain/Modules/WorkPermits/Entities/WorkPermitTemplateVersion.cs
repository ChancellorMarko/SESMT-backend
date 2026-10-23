using System.Text.Json;
using System.Text.Json.Serialization;
using SESMT.Domain.Common.Entity;
using SESMT.Domain.Exceptions;

namespace SESMT.Domain.Modules.WorkPermits.Entities;

public sealed class WorkPermitTemplateVersion : EntityBase
{
    private WorkPermitTemplateVersion() { }

    public Guid CompanyId { get; private set; }
    public Guid TemplateId { get; private set; }
    public string Name { get; private set; } = null!;
    public int Version { get; private set; }
    public string ServiceCodesJson { get; private set; } = "[]";
    public string QuestionsJson { get; private set; } = "[]";
    public string Declaration { get; private set; } = null!;
    public bool Illustrative { get; private set; }

    public IReadOnlyList<string> ServiceCodes => JsonSerializer.Deserialize<string[]>(ServiceCodesJson) ?? [];
    public IReadOnlyList<WorkPermitChecklistQuestion> Questions => JsonSerializer.Deserialize<WorkPermitChecklistQuestion[]>(QuestionsJson) ?? [];

    public static bool IsChecklistReady(IEnumerable<WorkPermitTemplateVersion> templates, IEnumerable<WorkPermitChecklistAnswer> answers)
    {
        var responseItems = answers.ToArray();
        if (responseItems.Select(answer => answer.QuestionId).Distinct().Count() != responseItems.Length)
            return false;
        var responseMap = responseItems.ToDictionary(answer => answer.QuestionId, answer => answer.Value);
        return templates.SelectMany(template => template.Questions).All(question =>
            responseMap.TryGetValue(question.Id, out var value)
                ? question.HasValidValue(value) && !question.Blocks(value)
                : !question.Required && !question.Blocking);
    }

    public static void EnsureSuccessorPreservesRequiredChecks(WorkPermitTemplateVersion current,
        IEnumerable<string> serviceCodes, IEnumerable<WorkPermitChecklistQuestion> questions)
    {
        var nextCodes = serviceCodes.ToHashSet(StringComparer.Ordinal);
        if (current.ServiceCodes.Any(code => !nextCodes.Contains(code)))
            throw new DomainException("Uma nova versão não pode remover serviços do template.");
        var nextQuestions = questions.ToDictionary(question => question.Id);
        foreach (var previous in current.Questions.Where(question => question.Required || question.Blocking))
        {
            if (!nextQuestions.TryGetValue(previous.Id, out var next)
                || (previous.Required && !next.Required)
                || (previous.Blocking && !next.Blocking)
                || !string.Equals(previous.Type, next.Type, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(previous.Text, next.Text, StringComparison.Ordinal))
                throw new DomainException("Uma nova versão não pode remover nem enfraquecer verificações obrigatórias ou impeditivas.");
        }
    }

    public WorkPermitTemplateVersion(Guid companyId, Guid templateId, string name, int version,
        IEnumerable<string> serviceCodes, IEnumerable<WorkPermitChecklistQuestion> questions,
        string declaration, bool illustrative, Guid createdBy)
    {
        if (companyId == Guid.Empty || templateId == Guid.Empty || createdBy == Guid.Empty || string.IsNullOrWhiteSpace(name) || version < 1)
            throw new ArgumentException("Empresa, template, nome e versão válida são obrigatórios.");
        var codes = serviceCodes.Select(code => code.Trim().ToUpperInvariant()).Distinct().ToArray();
        if (codes.Length == 0 || codes.Any(code => WorkPermitServiceCatalogEntry.All.All(service => service.Code != code)))
            throw new ArgumentException("Selecione ao menos um serviço válido.");
        var items = questions.ToArray();
        if (items.Length == 0 || items.Any(question => question.Id == Guid.Empty || string.IsNullOrWhiteSpace(question.Text)
                || !new[] { "BOOLEAN", "TEXT", "NUMBER" }.Contains(question.Type, StringComparer.OrdinalIgnoreCase))
            || items.Select(question => question.Id).Distinct().Count() != items.Length)
            throw new ArgumentException("O checklist deve conter perguntas válidas e sem identificadores repetidos.");
        if (items.Any(question => !question.Required && question.Blocking))
            throw new ArgumentException("Uma pergunta impeditiva precisa ser obrigatória.");
        if (string.IsNullOrWhiteSpace(declaration))
            throw new ArgumentException("A declaração do template é obrigatória.");
        if (!illustrative)
            throw new ArgumentException("Templates só podem ser cadastrados como ilustrativos até aprovação formal do SESMT.");

        CompanyId = companyId;
        TemplateId = templateId;
        Name = name.Trim();
        Version = version;
        ServiceCodesJson = JsonSerializer.Serialize(codes);
        QuestionsJson = JsonSerializer.Serialize(items);
        Declaration = declaration.Trim();
        Illustrative = illustrative;
        CreatedBy = createdBy;
    }
}

public sealed record WorkPermitChecklistQuestion(
    Guid Id,
    [property: JsonPropertyName("texto")] string Text,
    [property: JsonPropertyName("tipo")] string Type,
    [property: JsonPropertyName("obrigatoria")] bool Required,
    [property: JsonPropertyName("impeditiva")] bool Blocking)
{
    public bool HasValidValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return !Required && !Blocking;
        return Type.ToUpperInvariant() switch
        {
            "BOOLEAN" => bool.TryParse(value, out _),
            "NUMBER" => decimal.TryParse(value, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out _),
            "TEXT" => true,
            _ => false
        };
    }

    public bool Blocks(string? value) => Blocking && (Type.ToUpperInvariant() switch
    {
        "BOOLEAN" => bool.TryParse(value, out var result) && !result,
        "NUMBER" => decimal.TryParse(value, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var result) && result == 0,
        _ => false
    });
}

public sealed record WorkPermitChecklistAnswer(
    [property: JsonPropertyName("perguntaId")] Guid QuestionId,
    [property: JsonPropertyName("valor")] string? Value);
