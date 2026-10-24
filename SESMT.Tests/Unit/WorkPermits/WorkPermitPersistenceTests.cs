using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.Tests.Unit.WorkPermits;

public class WorkPermitPersistenceTests
{
    [Fact]
    public void AppointmentUniquenessIndex_ShouldIncludeSoftDeletedPermits()
    {
        var options = new DbContextOptionsBuilder<SesmtDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=sesmt;Username=sesmt;Password=sesmt123")
            .Options;
        using var context = new SesmtDbContext(options);
        var index = context.Model.FindEntityType(typeof(WorkPermit))!.GetIndexes()
            .Single(candidate => candidate.GetDatabaseName() == "ux_permissao_trabalho_agendamento");

        Assert.True(index.IsUnique);
        Assert.Null(index.GetFilter());
    }

    [Fact]
    public void SaveChanges_ShouldRejectModificationOfTransitionHistory()
    {
        var options = new DbContextOptionsBuilder<SesmtDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=sesmt;Username=sesmt;Password=sesmt123")
            .Options;
        using var context = new SesmtDbContext(options);
        var permit = new WorkPermit(Guid.NewGuid(), Guid.NewGuid(), "INSPECAO", "Inspeção", Guid.NewGuid());
        var transition = permit.Transitions.Single();
        context.WorkPermitTransitions.Attach(transition);
        context.Entry(transition).Property(nameof(WorkPermitTransition.Action)).CurrentValue = "ALTERADA";

        Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
    }
}
