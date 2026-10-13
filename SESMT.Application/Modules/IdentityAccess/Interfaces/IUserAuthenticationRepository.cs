using SESMT.Application.Modules.IdentityAccess.DTOs;

namespace SESMT.Application.Modules.IdentityAccess.Interfaces;

/// <summary>
/// Define as operações de persistência necessárias para autenticação.
/// </summary>
public interface IUserAuthenticationRepository
{
    /// <summary>
    /// Busca um usuário pelo e-mail e recupera seus perfis e escopos.
    /// Usuários excluídos logicamente não devem ser retornados.
    /// </summary>
    Task<UserAuthenticationData?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca um usuário pelo ID e recupera seus perfis e escopos.
    /// Usuários excluídos logicamente não devem ser retornados.
    /// </summary>
    Task<UserAuthenticationData?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste as alterações realizadas durante a autenticação.
    /// </summary>
    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
