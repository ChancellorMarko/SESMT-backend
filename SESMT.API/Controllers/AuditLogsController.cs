using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Audit.Repositories;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Controllers;

/// <summary>Consulta append-only da trilha de auditoria nos escopos do usuário.</summary>
[ApiController]
[Route("audit-logs")]
[Authorize]
public sealed class AuditLogsController : ApiControllerBase
{
    private readonly IAuditLogRepository _repository;
    private readonly SesmtDbContext _db;

    public AuditLogsController(IAuditLogRepository repository, SesmtDbContext db)
    {
        _repository = repository;
        _db = db;
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
        if (!TryGetActorId(out var userId)) return Unauthorized();
        if (!await CanReadAuditAsync(userId, cancellationToken))
            return Failure(403, "FORBIDDEN", "Você não possui permissão para consultar a auditoria.");
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
        if (!await CanReadAuditAsync(userId, cancellationToken))
            return Failure(403, "FORBIDDEN", "Você não possui permissão para consultar a auditoria.");
        var companyIds = await _repository.GetAccessibleCompanyIdsAsync(userId, cancellationToken);
        var log = await _repository.GetByEventIdAsync(eventId, companyIds, cancellationToken);
        return log is null
            ? Failure(404, "AUDIT_EVENT_NOT_FOUND", "Log não encontrado.")
            : Ok(ToItem(log));
    }

    private async Task<bool> CanReadAuditAsync(Guid userId, CancellationToken cancellationToken)
        => await (
            from userRole in _db.UserRoles.AsNoTracking()
            join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            join rolePermission in _db.RolePermissions.AsNoTracking() on role.Id equals rolePermission.RoleId
            join permission in _db.Permissions.AsNoTracking() on rolePermission.PermissionId equals permission.Id
            where userRole.UserId == userId
                && userRole.Active && userRole.DeletedAt == null
                && role.Active && role.DeletedAt == null
                && rolePermission.Active && rolePermission.DeletedAt == null
                && permission.Active && permission.DeletedAt == null
                && permission.Resource == "auditoria"
                && (permission.Action == "ler" || permission.Action == "exportar")
            select 1)
            .AnyAsync(cancellationToken);

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
