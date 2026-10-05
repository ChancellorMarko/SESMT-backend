using SESMT.Domain.Common;

namespace SESMT.Domain.Events;

/// <summary>
/// Representa a mudança de função de um trabalhador.
/// Esse evento registra um fato já ocorrido no domínio e permite que,
/// posteriormente, outras partes do sistema reajam à alteração sem
/// acoplar essa regra diretamente ao agregado.
/// </summary>
public sealed record WorkerFunctionChangedEvent(
    Guid WorkerId,
    Guid PreviousFunctionId,
    Guid NewFunctionId,
    DateTime OccurredOn) : IDomainEvent;