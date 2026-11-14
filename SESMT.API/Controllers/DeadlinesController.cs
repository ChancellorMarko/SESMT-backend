using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SESMT.API.Services;
using SESMT.Application.Exceptions;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Controllers;

public sealed record DeadlineItem(string Kind, Guid Id, Guid CompanyId, Guid UnitId, string UnitName, Guid WorkerId,
    string WorkerName, string Label, DateOnly DueOn, string Status);

/// <summary>Consolidated view of administrative due dates (training renewal, next ASO evaluation and PPE
/// replacement) across every unit the actor may read. Each source keeps the exact read rule of its own
/// module; nothing here infers aptitude, qualification or clinical data. Only the current record of each
/// worker/subject is considered, so an already renewed item never alerts.</summary>
[ApiController, Authorize, Route("deadlines")]
public sealed class DeadlinesController(SesmtDbContext db, NativeResourceAccess access) : ApiControllerBase
{
    public const int MaxItems = 200;
    private static readonly string[] Kinds = ["TRAINING", "ASO", "EPI"];
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Sao_Paulo").DateTime);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, int days = 30, Guid? unitId = null, Guid? workerId = null, string? kind = null)
    {
        Response.Headers.CacheControl = "no-store";
        if (days is < 1 or > 365 || unitId == Guid.Empty || workerId == Guid.Empty || kind is not null && !Kinds.Contains(kind))
            return Failure(400, "VALIDATION_FAILED", "Prazo, unidade, trabalhador ou tipo inválido.");

        var scope = await access.ReadAsync(ct);
        if (unitId.HasValue && !scope.HasUnit(unitId.Value)) throw new ResourceForbiddenException();
        var today = Today; var limit = today.AddDays(days);
        var sources = new (string Kind, string Permission)[] { ("TRAINING", "treinamento:ler"), ("ASO", "aso:acompanhar"), ("EPI", "epi:ler") };
        var readable = sources.Where(s => kind is null || s.Kind == kind)
            .Select(s => (s.Kind, Companies: NativeResourceAccess.Companies(scope, s.Permission))).Where(s => s.Companies.Length > 0).ToArray();
        if (readable.Length == 0) throw new ResourceForbiddenException();

        var units = await db.Units.AsNoTracking().Where(u => u.Active && u.DeletedAt == null && scope.UnitIds.Contains(u.Id)
                && (!unitId.HasValue || u.Id == unitId)).Select(u => new { u.Id, u.CompanyId, u.Name }).ToListAsync(ct);
        var unitNames = units.ToDictionary(u => u.Id, u => u.Name);
        var fullUnits = scope.FullUnitIds.ToArray(); var sectors = scope.SectorIds.ToArray();
        var ownOnly = scope.OwnOnly; var ownWorker = scope.WorkerId;
        var workers = db.Workers.AsNoTracking().Where(w => w.Active && w.DeletedAt == null
            && (w.DismissalDate == null || w.DismissalDate > today)
            && (!ownOnly || w.Id == ownWorker) && (!workerId.HasValue || w.Id == workerId)
            && w.UnitId.HasValue && (fullUnits.Contains(w.UnitId.Value) || w.DepartmentId.HasValue && sectors.Contains(w.DepartmentId.Value)));

        var rows = new List<(string Kind, Guid Id, Guid Company, Guid Unit, Guid Worker, string WorkerName, string Label, DateOnly Due)>();
        foreach (var (sourceKind, companies) in readable)
        {
            var allowedUnits = units.Where(u => companies.Contains(u.CompanyId)).Select(u => u.Id).ToArray();
            if (allowedUnits.Length == 0) continue;
            if (sourceKind == "TRAINING")
            {
                var training = from t in db.TrainingRecords.AsNoTracking() join w in workers on t.WorkerId equals w.Id
                    where t.Active && t.DeletedAt == null && t.RevokedAt == null && t.CompanyId == w.CompanyId
                        && allowedUnits.Contains(t.UnitId) && t.RenewalOn != null && t.RenewalOn <= limit
                        && !db.TrainingRecords.Any(n => n.WorkerId == t.WorkerId && n.Id != t.Id && n.Active && n.DeletedAt == null
                            && n.RevokedAt == null && n.Title.ToLower() == t.Title.ToLower()
                            && (n.CompletedOn > t.CompletedOn || n.CompletedOn == t.CompletedOn && n.CreatedAt > t.CreatedAt))
                    select new { t.Id, t.CompanyId, t.UnitId, t.WorkerId, WorkerName = w.Name.Value, Label = t.Title, Due = t.RenewalOn!.Value };
                rows.AddRange((await training.ToListAsync(ct)).Select(x => ("TRAINING", x.Id, x.CompanyId, x.UnitId, x.WorkerId, x.WorkerName, x.Label, x.Due)));
            }
            else if (sourceKind == "ASO")
            {
                // Only the next-evaluation date of the current ASO. Exam kind, result and clinical details stay out.
                var aso = from a in db.OccupationalCertificates.AsNoTracking() join w in workers on a.WorkerId equals w.Id
                    where a.Active && a.DeletedAt == null && a.RevokedAt == null && a.CompanyId == w.CompanyId && w.UnitId == a.UnitId
                        && allowedUnits.Contains(a.UnitId) && a.NextEvaluationOn != null && a.NextEvaluationOn <= limit
                        && !db.OccupationalCertificates.Any(n => n.WorkerId == a.WorkerId && n.Id != a.Id && n.Active && n.DeletedAt == null
                            && n.RevokedAt == null && (n.ExamOn > a.ExamOn || n.ExamOn == a.ExamOn && (n.IssuedOn > a.IssuedOn
                                || n.IssuedOn == a.IssuedOn && n.CreatedAt > a.CreatedAt)))
                    select new { a.Id, a.CompanyId, a.UnitId, a.WorkerId, WorkerName = w.Name.Value, Due = a.NextEvaluationOn!.Value };
                rows.AddRange((await aso.ToListAsync(ct)).Select(x => ("ASO", x.Id, x.CompanyId, x.UnitId, x.WorkerId, x.WorkerName, "Próxima avaliação do ASO", x.Due)));
            }
            else
            {
                var epi = from d in db.EquipmentDeliveries.AsNoTracking() join w in workers on d.WorkerId equals w.Id
                    where d.Active && d.DeletedAt == null && d.ReturnedOn == null && d.CompanyId == w.CompanyId
                        && allowedUnits.Contains(d.UnitId) && d.ReplacementOn != null && d.ReplacementOn <= limit
                        && !db.EquipmentDeliveries.Any(n => n.WorkerId == d.WorkerId && n.EquipmentId == d.EquipmentId && n.Id != d.Id
                            && n.Active && n.DeletedAt == null && (n.DeliveredOn > d.DeliveredOn || n.DeliveredOn == d.DeliveredOn && n.CreatedAt > d.CreatedAt))
                    select new { d.Id, d.CompanyId, d.UnitId, d.WorkerId, WorkerName = w.Name.Value, Label = d.EquipmentName, Due = d.ReplacementOn!.Value };
                rows.AddRange((await epi.ToListAsync(ct)).Select(x => ("EPI", x.Id, x.CompanyId, x.UnitId, x.WorkerId, x.WorkerName, x.Label, x.Due)));
            }
        }

        var ordered = rows.OrderBy(r => r.Due).ThenBy(r => r.WorkerName, StringComparer.Ordinal).ThenBy(r => r.Id).ToArray();
        var items = ordered.Take(MaxItems).Select(r => new DeadlineItem(r.Kind, r.Id, r.Company, r.Unit, unitNames[r.Unit], r.Worker,
            r.WorkerName, r.Label, r.Due, r.Due < today ? "OVERDUE" : "DUE_SOON")).ToArray();
        return Ok(new
        {
            referenceDate = today, days, kinds = readable.Select(r => r.Kind).ToArray(),
            summary = new { overdue = ordered.Count(r => r.Due < today), dueSoon = ordered.Count(r => r.Due >= today) },
            total = ordered.Length, truncated = ordered.Length > MaxItems, items,
        });
    }
}
