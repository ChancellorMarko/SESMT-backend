namespace SESMT.Domain.Modules.Documents.Constants
{
    public static class DocumentVersionStateExtensions
    {
        public static bool IsEditable(this string estado)
        {
            return estado == "RASCUNHO";
        }

        public static bool IsTerminal(this string estado)
        {
            return estado == "APROVADO" || estado == "REJEITADO";
        }
    }
}