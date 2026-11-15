namespace SESMT.API.Services;

/// <summary>
/// A refusal the client can act on, with the stable error code the endpoint has always answered (EQUIPMENT_CHANGED,
/// DUPLICATE_COMMAND, INVALID_WORKER...). Services throw it; <c>ApiControllerBase.Respond</c> turns it into the standard
/// error response, so moving rules out of controllers does not change any contract.
/// </summary>
public sealed class ServiceRefusal(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
