using SESMT.Domain.Common.AggregateRoot;
using SESMT.Domain.ValueObjects;

namespace SESMT.Domain.Modules.IdentityAccess.Entities;

public class User : AggregateRootBase
{
    public string Name { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public bool MfaEnabled { get; private set; }
    public byte[]? MfaSecretEncrypted { get; private set; }
    public DateTimeOffset? LastLogin { get; private set; }
    public DateTimeOffset? BlockedUntil { get; private set; }
    public int LoginAttempts { get; private set; }

    protected User() { }

    public User(string name, string email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nome do usuário é obrigatório.");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Hash da senha é obrigatório.");

        Name = name.Trim();
        Email = new Email(email);   // valida e normaliza
        PasswordHash = passwordHash;
    }

    public void Update(string name, string email)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nome do usuário é obrigatório.");

        Name = name.Trim();
        Email = new Email(email);
        MarkAsUpdated();
    }

    public void UpdatePassword(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("A senha é obrigatória.");

        PasswordHash = passwordHash;
        MarkAsUpdated();
    }

    public void RegisterLogin()
    {
        LastLogin = DateTimeOffset.UtcNow;
        LoginAttempts = 0;
        BlockedUntil = null;
        MarkAsUpdated();
    }

    public void RegisterFailedLogin(int maxAttempts, TimeSpan blockDuration)
    {
        LoginAttempts++;
        if (LoginAttempts >= maxAttempts)
            BlockedUntil = DateTimeOffset.UtcNow.Add(blockDuration);
        MarkAsUpdated();
    }

    public void EnableMfa(byte[] encryptedSecret)
    {
        if (encryptedSecret is null || encryptedSecret.Length == 0)
            throw new ArgumentException("O segredo MFA é obrigatório.");

        MfaEnabled = true;
        MfaSecretEncrypted = encryptedSecret;
        MarkAsUpdated();
    }

    public void DisableMfa()
    {
        MfaEnabled = false;
        MfaSecretEncrypted = null;
        MarkAsUpdated();
    }
}
