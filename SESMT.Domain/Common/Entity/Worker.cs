using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.Workforce.Entities;

public sealed class Worker : AggregateRootBase
{
    private readonly List<WorkerMovement> _movements = [];

    public Guid FunctionId { get; private set; }

    public string Name { get; private set; } = null!;

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
        Name = name;

        // Registra a função inicial para que o histórico do trabalhador
        // exista desde o início do seu vínculo.
        _movements.Add(
            new WorkerMovement(
                Id,
                functionId,
                startDate));
    }

    /// <summary>
    /// Move o trabalhador para uma nova função.
    /// A movimentação anterior é encerrada antes de registrar a nova,
    /// preservando o histórico do trabalhador.
    /// </summary>
    public void MoveToFunction(
        Guid newFunctionId,
        DateTime movementDate)
    {
        // Não há movimentação se o trabalhador já estiver na função informada.
        if (newFunctionId == FunctionId)
            return;

        // Encerra a movimentação atualmente vigente, caso exista.
        var currentMovement = _movements
            .FirstOrDefault(x => x.EndDate is null);

        currentMovement?.End(movementDate);

        // Atualiza a função atual do trabalhador.
        FunctionId = newFunctionId;

        // Registra a nova movimentação para manter o histórico.
        _movements.Add(
            new WorkerMovement(
                Id,
                newFunctionId,
                movementDate));
    }
}