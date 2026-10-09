namespace SESMT.Domain.Modules.Documents.Constants;

public static class LinkedEntityType
{
    public const string Worker       = "TRABALHADOR";
    public const string Department   = "SETOR";
    public const string Unit         = "UNIDADE";
    public const string Company      = "EMPRESA";
    public const string Risk         = "RISCO";        // Fase 2
    public const string Accident     = "ACIDENTE";     // Fase 4
    public const string Training     = "TREINAMENTO";  // Fase 2
    public const string Position     = "CARGO";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Worker, Department, Unit, Company,
        Risk, Accident, Training, Position
    };

    public static bool IsValid(string value) => All.Contains(value);
}