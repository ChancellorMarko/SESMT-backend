using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.API.Contracts.Users;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.API.Services;

public sealed partial class UserManagementService
{
    /// <summary>Audit action for a reset started by an administrator. The token itself is never stored or logged in clear.</summary>
    public const string PasswordResetIssued = "PASSWORD_RESET_ISSUED";

    /// <summary>Longer than the self-service link (30 min) because the administrator hands it over in person or by another channel.</summary>
    public static readonly TimeSpan AdminResetLifetime = TimeSpan.FromHours(24);

    /// <summary>
    /// Locks the account out of its old password and sessions and issues a single-use reset token.
    /// The administrator never chooses or learns the new password: only the holder of the token defines it,
    /// so signatures and acknowledgements made with the account keep identifying its owner.
    /// </summary>
    public async Task<object?> ResetPasswordAsync(Guid id, AdminPasswordResetRequest request, CancellationToken ct)
    {
        if (!TryGetActor(out var actorId))
            throw new ServiceRefusal(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário autenticado.");
        var reason = request?.Reason?.Trim();
        if (request is null || !request.Confirmation || reason is null || reason.Length is < 10 or > 500)
            throw new ServiceRefusal(400, "VALIDATION_FAILED", "Confirme a redefinição e informe o motivo (de 10 a 500 caracteres).");
        if (id == actorId)
            throw new ServiceRefusal(409, "OWN_ACCOUNT", "Para a sua própria conta, use \"Esqueci minha senha\" na tela de login.");

        await using var transaction = await LockManagedUserAsync(id, ct);
        var user = await FindManagedUserAsync(id, actorId, "editar", ct);
        if (user is null)
            throw new ServiceRefusal(404, "NOT_FOUND", "Conta não encontrada ou fora do seu escopo.");
        if (!user.Active)
            throw new ServiceRefusal(409, "INACTIVE_ACCOUNT", "Esta conta está desativada. Reative-a antes de redefinir a senha.");

        var now = _clock.GetUtcNow();
        // Earlier links stop working, so only the token issued now is valid.
        var pending = await _db.PasswordResetTokens
            .Where(t => t.UserId == id && t.UsedAt == null && t.Active && t.DeletedAt == null)
            .ToListAsync(ct);
        foreach (var previous in pending) previous.MarkUsed(now);

        // The old password stops working immediately (lost device, shared or leaked password).
        user.UpdatePassword(_passwordHasher.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))));
        var sessions = await _db.UserSessions
            .Where(s => s.UserId == id && s.Active && s.RevokedAt == null && s.DeletedAt == null)
            .ToListAsync(ct);
        foreach (var session in sessions) session.Revoke();

        var token = _tokens.GenerateToken();
        var expiresAt = now.Add(AdminResetLifetime);
        _db.PasswordResetTokens.Add(new PasswordResetToken(id, _tokens.HashToken(token), expiresAt));

        var details = JsonSerializer.Serialize(new { reason, expiresAt, sessionsEnded = sessions.Count, previousLinksCancelled = pending.Count });
        await AddAuditAsync(PasswordResetIssued, id, actorId, null, details, await CompanyIdsForUserAsync(id, ct), ct);

        await _db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new AdminPasswordResetResponse(token, expiresAt, sessions.Count);
    }
}
