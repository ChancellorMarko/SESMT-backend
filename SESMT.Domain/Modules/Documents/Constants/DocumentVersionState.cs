namespace SESMT.Domain.Modules.Documents.Constants;

public static class DocumentVersionState
{
    public const string Draft       = "RASCUNHO";
    public const string InReview    = "EM_REVISAO";
    public const string Approved    = "APROVADA";
    public const string Current     = "VIGENTE";
    public const string Superseded  = "SUBSTITUIDA";
    public const string Rejected    = "REJEITADA";
}