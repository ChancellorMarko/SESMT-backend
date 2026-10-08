using System;

namespace SESMT.Domain.Modules.Documents.Constants
{
    public static class DocumentStatus
    {
        public const string Draft = "RASCUNHO";
        public const string InReview = "EM_REVISAO";
        public const string Approved = "APROVADO";
        public const string Active = "VIGENTE";
        public const string Expired = "VENCIDO";
        public const string Superseded = "SUBSTITUÍDO";
        public const string Archived = "ARQUIVADO";
    }
}