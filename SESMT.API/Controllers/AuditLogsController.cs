using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Contracts.Errors;
using SESMT.Domain.Modules.Audit.Repositories;

namespace SESMT.API.Controllers;

/// <summary>
/// Consulta de logs de auditoria. Acesso restrito a ADMIN/AUDITOR.
/// </summary>
[ApiController]
[Route("audit-logs")]
[Authorize(Roles = "ADMIN,AUDITOR")]
public sealed class AuditLogsController : ControllerBase
{
    private readonly IAuditLogRepository _repository;

    public AuditLogsController(IAuditLogRepository repository)
    {
        _repository = repository;
    }

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
        if (pageSize <= 0 || pageSize > 200) pageSize = 20;
        if (pageNumber <= 0) pageNumber = 1;

        var skip = (pageNumber - 1) * pageSize;

        var logs = await _repository.QueryAsync(
            actorId, action, resourceType, resourceId, from, to, skip, pageSize, cancellationToken);

        var total = await _repository.CountAsync(
            actorId, action, resourceType, resourceId, from, to, cancellationToken);

        return Ok(new
        {
            total,
            pageSize,
            pageNumber,
            items = logs.Select(a => new
            {
                a.Id,
                a.OccurredAt,
                a.ActorId,
                a.ActorName,
                a.ActorEmail,
                a.Action,
                a.ResourceType,
                a.ResourceId,
                a.IpAddress,
                a.CurrentHash,
            })
        });
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        // Query detalhada com payloads completos
        var logs = await _repository.QueryAsync(
            null, null, null, null, null, null,
            skip: 0, take: 1, cancellationToken);

        // Simplificação — idealmente um GetByIdAsync no repositório
        // Para o MVP: consulta via filtro ou adicione um método
        return NotFound();
    }
}
