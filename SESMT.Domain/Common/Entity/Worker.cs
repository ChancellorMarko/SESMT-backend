using SESMT.Domain.Common.AggregateRoot;
using SESMT.Domain.Events;
using SESMT.Domain.ValueObjects;

namespace SESMT.Domain.Modules.Workforce.Entities;

public sealed class Worker : AggregateRootBase
{
    private readonly List<WorkerMovement> _movements = [];

    public Guid FunctionId { get; private set; }

    public WorkerName Name { get; private set; } = null!;

    public IReadOnlyCollection<WorkerMovement> Movements =>
        _movements.AsReadOnly();

    private Worker()
    {
    }

    public Worker(
        Guid functionId,
        string name,
        DateTime startDate)
    {
        FunctionId = functionId;
        Name = new WorkerName(name);

        // A função inicial do trabalhador também faz parte
        // do histórico para preservar sua trajetória desde a admissão.
        _movements.Add(
            new WorkerMovement(
                Id,
                functionId,
                startDate));
    }

    /// <summary>
    /// Move o trabalhador para uma nova função.
    /// A movimentação anterior é encerrada antes de registrar a nova,
    /// preservando o histórico e registrando a alteração como fato de domínio.
    /// </summary>
    public void MoveToFunction(
        Guid newFunctionId,
        DateTime movementDate)
    {
        // Não existe alteração de função quando o trabalhador
        // permanece na mesma função.
        if (newFunctionId == FunctionId)
            return;

        var previousFunctionId = FunctionId;

        // Encerra a movimentação atualmente vigente para manter
        // o histórico do trabalhador consistente.
        var currentMovement = _movements
            .FirstOrDefault(x => x.EndDate is null);

        currentMovement?.End(movementDate);

        FunctionId = newFunctionId;

        _movements.Add(
            new WorkerMovement(
                Id,
                newFunctionId,
                movementDate));

        // A mudança de função é um fato relevante do domínio.
        // Outros mecanismos poderão reagir a esse evento posteriormente.
        AddDomainEvent(
            new WorkerFunctionChangedEvent(
                Id,
                previousFunctionId,
                newFunctionId,
                movementDate));
    }
}