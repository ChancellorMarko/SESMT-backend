namespace SESMT.Application.Exceptions;

/// <summary>An Idempotency-Key already used by the same person for a different request.</summary>
public sealed class IdempotencyConflictException : Exception
{
    public IdempotencyConflictException()
        : base("Esta tentativa já foi utilizada com outros dados. Confira o resultado antes de continuar.")
    {
    }
}
