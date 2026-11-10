using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.WorkPermits.Constants;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Tests.Integration.API;

/// <summary>The final review and the replaced closings must survive a round trip through PostgreSQL.</summary>
[Collection(PostgresCollection.Name)]
public sealed class WorkPermitReviewPostgresTests(PostgresDatabaseFixture database)
{
    [PostgresFact]
    public async Task ReturnedClosingIsKeptAfterResubmissionAndTheReviewIsPersisted()
    {
        var starts = DateTimeOffset.UtcNow.AddDays(6);
        var appointment = WorkPermitAppointment.CreateRequest(database.CompanyA, "Revisão final fictícia", starts, starts.AddHours(1),
            database.ActorA, database.UnitA, database.TeamA, ["ALTURA"], [database.WorkerA]);
        var permit = new WorkPermit(database.CompanyA, appointment.Id, "ALTURA", "Revisão final fictícia", database.ActorA);
        var closing = new WorkPermitChecklistAnswer(Guid.NewGuid(), "true");
        permit.Release(database.ActorA, true, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(2), "Condições fictícias", true);
        permit.Start(database.ActorA, true);
        permit.Complete(database.ActorA, "Primeiro encerramento", DateTimeOffset.UtcNow, [closing], true);
        await using (var db = database.CreateContext()) { db.Add(appointment); db.Add(permit); await db.SaveChangesAsync(); }

        await using (var db = database.CreateContext())
        {
            var stored = await db.WorkPermits.Include(p => p.Transitions).SingleAsync(p => p.Id == permit.Id);
            stored.Review(database.ActorB, WorkPermit.Returned, "Falta a liberação da área");
            await db.SaveChangesAsync();
        }
        await using (var db = database.CreateContext())
        {
            var stored = await db.WorkPermits.Include(p => p.Transitions).SingleAsync(p => p.Id == permit.Id);
            Assert.Equal(WorkPermit.Returned, stored.ReviewStatus); Assert.Equal(database.ActorB, stored.ReviewedBy);
            Assert.Equal("Falta a liberação da área", stored.ReviewNote); Assert.NotNull(stored.ReviewedAt);
            stored.ResubmitClosing(database.ActorA, "Encerramento corrigido", DateTimeOffset.UtcNow, [closing], true);
            await db.SaveChangesAsync();
        }
        await using (var db = database.CreateContext())
        {
            var stored = await db.WorkPermits.Include(p => p.Transitions).SingleAsync(p => p.Id == permit.Id);
            Assert.Equal(WorkPermit.AwaitingReview, stored.ReviewStatus); Assert.Null(stored.ReviewedBy); Assert.Null(stored.ReviewNote);
            Assert.Equal(WorkPermitStatus.Completed, stored.Status); Assert.Equal("Encerramento corrigido", stored.CompletionSummary);
            var previous = Assert.Single(stored.ClosingHistory);
            Assert.Equal("Primeiro encerramento", previous.Summary); Assert.Equal(database.ActorA, previous.SubmittedBy);
            Assert.Equal(database.ActorB, previous.ReviewedBy); Assert.Equal("Falta a liberação da área", previous.ReviewNote);
            Assert.Equal(closing.QuestionId, Assert.Single(previous.Answers).QuestionId);
            Assert.Contains(stored.Transitions, t => t.Action == "REVISAO_DEVOLVIDA" && t.Reason == "Falta a liberação da área");
            Assert.Contains(stored.Transitions, t => t.Action == "ENCERRAMENTO_REENVIADO");
        }
    }
}
