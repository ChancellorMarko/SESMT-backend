using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Audit.Repositories;

namespace SESMT.API.Controllers;

/// <summary>Consulta append-only da trilha de auditoria nos escopos do usuário.</summary>
[ApiController]
[Route("audit-logs")]
[Authorize(Roles = "ADMIN,AUDITOR")]
public sealed class AuditLogsController : ApiControllerBase
{
    private readonly IAuditLogRepository _repository;

    public AuditLogsController(IAuditLogRepository repository) => _repository = repository;

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Query(
        [FromQuery] Guid? actorId,
        [FromQuery] string? action,
        [FromQuery] string? resourceType,
        [FromQuery] Guid? resourceId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int pageSize = 20,
        [FromQuery] int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActorId(out var userId)) return Unauthorized();
        if (from.HasValue && to.HasValue && from > to)
            return Failure(400, "INVALID_RANGE", "O início do período deve ser anterior ao fim.");

        pageSize = Math.Clamp(pageSize, 1, 200);
        pageNumber = Math.Max(pageNumber, 1);
        var companyIds = await _repository.GetAccessibleCompanyIdsAsync(userId, cancellationToken);
        var skip = (int)Math.Min((long)(pageNumber - 1) * pageSize, int.MaxValue);

        var logs = await _repository.QueryAsync(
            actorId, action, resourceType, resourceId, from, to, companyIds,
            skip, pageSize, cancellationToken);
        var total = await _repository.CountAsync(
            actorId, action, resourceType, resourceId, from, to, companyIds, cancellationToken);

        return Ok(new
        {
            total,
            page = pageNumber,
            pageSize,
            items = logs.Select(ToItem),
        });
    }

    [HttpGet("{eventId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid eventId, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var userId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        var companyIds = await _repository.GetAccessibleCompanyIdsAsync(userId, cancellationToken);
        var log = await _repository.GetByEventIdAsync(eventId, companyIds, cancellationToken);
        return log is null 
            ? Failure(404, "AUDIT_EVENT_NOT_FOUND", "Log não encontrado.")
            : Ok(ToItem(log));
    }

    private bool TryGetActorId(out Guid actorId)
    {
        var value = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out actorId);
    }

    private static object ToItem(AuditLog log) => new
    {
        id = log.EventId,
        atorId = log.ActorId,
        declaranteId = log.DeclarantId,
        empresaId = log.CompanyId,
        acao = log.Action,
        recursoTipo = log.ResourceType,
        recursoId = log.ResourceId,
        versao = log.ResourceVersion,
        momento = log.OccurredAt,
        resultado = log.Result,
        traceId = log.TraceId,
    };
}
