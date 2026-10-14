using Microsoft.AspNetCore.Identity;
using SESMT.Application.Common.Interfaces;

namespace SESMT.Infrastructure.Identity;

/// <summary>
/// Implementa a geração e a verificação de hashes de senha.
/// </summary>
public sealed class PasswordHashService : IPasswordHashService
{
    private readonly PasswordHasher<object> _passwordHasher = new();

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return _passwordHasher.HashPassword(new object(), password);
    }

    public bool VerifyPassword(string passwordHash, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)
            || string.IsNullOrWhiteSpace(providedPassword))
        {
            return false;
        }

        var result = _passwordHasher.VerifyHashedPassword(
            new object(),
            passwordHash,
            providedPassword);

        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}