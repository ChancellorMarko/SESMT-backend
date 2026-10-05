using SESMT.Domain.Events;
using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Tests.Unit.Workforce;

public class WorkerTests
{
    /// <summary>
    /// Garante que uma mudança de função encerre a movimentação anterior
    /// e registre uma nova movimentação no histórico do trabalhador.
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

        // O histórico deve preservar a movimentação inicial
        // e registrar a nova função como movimentação vigente.
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
    /// Garante que solicitar a mesma função atual não altere
    /// o histórico nem gere uma nova movimentação.
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

        Assert.Single(worker.Movements);

        var movement = worker.Movements.Single();

        Assert.Equal(functionId, movement.FunctionId);
        Assert.Equal(startDate, movement.StartDate);
        Assert.Null(movement.EndDate);
    }

    /// <summary>
    /// Garante que uma mudança de função registre um evento de domínio
    /// contendo a função anterior e a nova função.
    /// </summary>
    [Fact]
    public void MoveToFunction_ShouldRaiseFunctionChangedEvent()
    {
        var firstFunctionId = Guid.NewGuid();
        var secondFunctionId = Guid.NewGuid();

        var movementDate = new DateTime(2026, 3, 1);

        var worker = new Worker(
            firstFunctionId,
            "João da Silva",
            new DateTime(2026, 1, 1));

        worker.MoveToFunction(
            secondFunctionId,
            movementDate);

        var domainEvent = Assert.Single(worker.DomainEvents);

        var functionChangedEvent =
            Assert.IsType<WorkerFunctionChangedEvent>(domainEvent);

        Assert.Equal(worker.Id, functionChangedEvent.WorkerId);
        Assert.Equal(
            firstFunctionId,
            functionChangedEvent.PreviousFunctionId);
        Assert.Equal(
            secondFunctionId,
            functionChangedEvent.NewFunctionId);
        Assert.Equal(
            movementDate,
            functionChangedEvent.OccurredOn);
    }

    /// <summary>
    /// Garante que permanecer na mesma função não produza
    /// um evento de alteração de função.
    /// </summary>
    [Fact]
    public void MoveToFunction_ShouldNotRaiseEvent_WhenFunctionIsTheSame()
    {
        var functionId = Guid.NewGuid();

        var worker = new Worker(
            functionId,
            "João da Silva",
            new DateTime(2026, 1, 1));

        worker.MoveToFunction(
            functionId,
            new DateTime(2026, 3, 1));

        Assert.Empty(worker.DomainEvents);
    }
}