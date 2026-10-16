using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.WorkPermits.Entities;

public sealed class WorkPermitAppointment : AggregateRootBase
{
    public Guid CompanyId { get; private set; }
    public string Title { get; private set; } = null!;
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public Guid RequestedBy { get; private set; }

    private WorkPermitAppointment() { }

    public WorkPermitAppointment(Guid companyId, string title, DateTimeOffset startsAt, DateTimeOffset endsAt, Guid requestedBy)
    {
        if (companyId == Guid.Empty || requestedBy == Guid.Empty)
            throw new ArgumentException("Empresa e solicitante do agendamento são obrigatórios.");
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título do agendamento é obrigatório.");
        if (endsAt <= startsAt)
            throw new ArgumentException("O término deve ser posterior ao início do agendamento.");

        CompanyId = companyId;
        Title = title.Trim();
        StartsAt = startsAt;
        EndsAt = endsAt;
        RequestedBy = requestedBy;
        CreatedBy = requestedBy;
    }
}
