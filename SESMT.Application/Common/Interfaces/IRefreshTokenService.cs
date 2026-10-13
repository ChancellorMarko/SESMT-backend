namespace SESMT.Application.Common.Interfaces;

/// <summary>
/// Define operações para geração e hash de refresh tokens.
/// </summary>
public interface IRefreshTokenService
{
    string GenerateToken();

    string HashToken(string refreshToken);
}
