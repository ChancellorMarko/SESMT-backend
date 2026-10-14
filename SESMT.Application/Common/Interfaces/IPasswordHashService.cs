namespace SESMT.Application.Common.Interfaces;

/// <summary>
/// Define operações para gerar e verificar hashes de senha.
/// </summary>
public interface IPasswordHashService
{
    string HashPassword(string password);

    bool VerifyPassword(string passwordHash, string providedPassword);
}