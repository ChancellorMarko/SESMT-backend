using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Tests.Unit.Workforce;

public class WorkerMovementTests
{
    [Fact]
    public void End_ShouldSetEndDate_WhenDateIsValid()
    {
        var startDate = new DateTime(2026, 1, 1);
        var endDate = new DateTime(2026, 3, 1);

        var movement = new WorkerMovement(
            Guid.NewGuid(),
            Guid.NewGuid(),
            startDate);

        movement.End(endDate);

        Assert.Equal(endDate, movement.EndDate);
    }

    [Fact]
    public void End_ShouldThrow_WhenEndDateIsBeforeStartDate()
    {
        var startDate = new DateTime(2026, 3, 1);
        var endDate = new DateTime(2026, 1, 1);

        var movement = new WorkerMovement(
            Guid.NewGuid(),
            Guid.NewGuid(),
            startDate);

        Assert.Throws<ArgumentException>(
            () => movement.End(endDate));
    }
}