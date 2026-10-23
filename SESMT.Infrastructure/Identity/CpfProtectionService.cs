using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using SESMT.Application.Common.Interfaces;
using SESMT.Domain.ValueObjects;

namespace SESMT.Infrastructure.Identity;

public sealed class CpfProtectionService : ICpfProtectionService
{
    private readonly IDataProtector _protector;

    public CpfProtectionService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("SESMT.CpfProtection.v1");
    }

    public (byte[] Encrypted, string Hash) Protect(string rawCpf)
    {
        var normalized = Cpf.Normalize(rawCpf);
        var encrypted = _protector.Protect(normalized);
        var hash = Hash(normalized);

        return (Encoding.UTF8.GetBytes(encrypted), hash);
    }

    public string Unprotect(byte[] encrypted)
    {
        var encryptedString = Encoding.UTF8.GetString(encrypted);
        return _protector.Unprotect(encryptedString);
    }

    public string Hash(string rawCpf)
    {
        var normalized = Cpf.Normalize(rawCpf);
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
