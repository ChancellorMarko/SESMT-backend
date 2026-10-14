using System.Security.Cryptography;
using System.Text;
using SESMT.Application.Common.Interfaces;

namespace SESMT.Infrastructure.Identity;

/// <summary>
/// Gera refresh tokens opacos e calcula seus hashes para persistência.
/// </summary>
public sealed class RefreshTokenService : IRefreshTokenService
{
    private const int TokenSizeInBytes = 64;

    public string GenerateToken()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(TokenSizeInBytes);

        return Convert.ToBase64String(tokenBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public string HashToken(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        var tokenBytes = Encoding.UTF8.GetBytes(refreshToken);
        var hashBytes = SHA256.HashData(tokenBytes);

        return Convert.ToBase64String(hashBytes);
    }
}
