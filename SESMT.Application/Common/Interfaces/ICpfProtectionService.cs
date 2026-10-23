namespace SESMT.Application.Common.Interfaces;

public interface ICpfProtectionService
{
    /// <summary>
    /// Criptografa o CPF para armazenamento e gera o hash para busca/unicidade.
    /// </summary>
    (byte[] Encrypted, string Hash) Protect(string rawCpf);

    /// <summary>
    /// Descriptografa um CPF armazenado. Uso restrito a perfis autorizados.
    /// </summary>
    string Unprotect(byte[] encrypted);

    /// <summary>
    /// Gera apenas o hash (SHA-256) — para consultas por CPF.
    /// </summary>
    string Hash(string rawCpf);
}
