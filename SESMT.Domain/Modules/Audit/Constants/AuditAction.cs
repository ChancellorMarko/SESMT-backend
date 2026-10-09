namespace SESMT.Domain.Modules.Audit.Constants;

public static class AuditAction
{
    public const string Login              = "LOGIN";
    public const string LoginFailed        = "LOGIN_FALHOU";
    public const string Logout             = "LOGOUT";
    public const string Create             = "CRIACAO";
    public const string Update             = "ALTERACAO";
    public const string Delete             = "EXCLUSAO_LOGICA";
    public const string View               = "CONSULTA";
    public const string Download           = "DOWNLOAD";
    public const string Approve            = "APROVACAO";
    public const string Reject             = "REJEICAO";
    public const string Sign               = "ASSINATURA";
    public const string Export             = "EXPORTACAO";
    public const string PermissionChange   = "ALTERACAO_PERMISSAO";
    public const string SensitiveView      = "CONSULTA_SENSIVEL";
}