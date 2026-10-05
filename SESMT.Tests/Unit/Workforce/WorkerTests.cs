using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Tests.Unit.Workforce;

public class WorkerTests
{
    /// <summary>
    /// Garante que uma movimentação para uma nova função
    /// encerra a movimentação anterior e registra a nova.
    /// </summary>
    /// <summary>
    /// Garante que uma movimentação para uma nova função
    /// encerra a movimentação anterior e registra a nova.
    /// </summary>
    [Fact]
    public void MoveToFunction_ShouldClosePreviousMovementAndCreateNewMovement()
    {
        var firstFunctionId = Guid.NewGuid();
        var secondFunctionId = Guid.NewGuid();

        var firstMovementDate = new DateTime(2026, 1, 1);
        var secondMovementDate = new DateTime(2026, 3, 1);

        var worker = new Worker(
            firstFunctionId,
            "João da Silva",
            firstMovementDate);

        worker.MoveToFunction(
            secondFunctionId,
            secondMovementDate);

        Assert.Equal(secondFunctionId, worker.FunctionId);

        // O trabalhador deve possuir a movimentação inicial
        // e a nova movimentação registrada.
        Assert.Equal(2, worker.Movements.Count);

        var previousMovement = worker.Movements.First();
        var currentMovement = worker.Movements.Last();

        Assert.Equal(firstFunctionId, previousMovement.FunctionId);
        Assert.Equal(firstMovementDate, previousMovement.StartDate);
        Assert.Equal(secondMovementDate, previousMovement.EndDate);

        Assert.Equal(secondFunctionId, currentMovement.FunctionId);
        Assert.Equal(secondMovementDate, currentMovement.StartDate);
        Assert.Null(currentMovement.EndDate);
    }

    /// <summary>
    /// Garante que solicitar a mesma função atual
    /// não cria uma nova movimentação.
    /// </summary>
    [Fact]
    public void MoveToFunction_ShouldDoNothing_WhenFunctionIsTheSame()
    {
        var functionId = Guid.NewGuid();
        var startDate = new DateTime(2026, 1, 1);

        var worker = new Worker(
            functionId,
            "João da Silva",
            startDate);

        worker.MoveToFunction(
            functionId,
            new DateTime(2026, 3, 1));

        Assert.Equal(functionId, worker.FunctionId);

        // A movimentação inicial deve continuar sendo a única movimentação.
        Assert.Single(worker.Movements);

        var movement = worker.Movements.Single();

        Assert.Equal(functionId, movement.FunctionId);
        Assert.Equal(startDate, movement.StartDate);
        Assert.Null(movement.EndDate);
    }
}