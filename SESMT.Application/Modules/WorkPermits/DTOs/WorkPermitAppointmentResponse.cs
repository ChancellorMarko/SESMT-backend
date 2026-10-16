using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record WorkPermitAppointmentResponse(Guid Id, Guid CompanyId, string Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt)
{
    public static WorkPermitAppointmentResponse From(WorkPermitAppointment appointment) =>
        new(appointment.Id, appointment.CompanyId, appointment.Title, appointment.StartsAt, appointment.EndsAt);
}
