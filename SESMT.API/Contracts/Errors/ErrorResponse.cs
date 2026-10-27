namespace SESMT.API.Contracts.Errors;

/// <summary>
/// Contrato de erro da API. Alinhado ao DTO do frontend (client.ts).
/// Todos os endpoints devem retornar exatamente este shape.
/// </summary>
public sealed record ErrorResponse(
    string Code,
    string Message,
    string RequestId,
    IReadOnlyList<FieldError>? FieldErrors = null);

/// <summary>
/// Erro por campo — usado em validação (400/422).
/// </summary>
public sealed record FieldError(
    string Field,
    string Code,
    string Message);