using System.ComponentModel.DataAnnotations;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Application.Modules.Appointments;

public sealed record RequestAppointment(Guid CompanyId, Guid LocationId, Guid TeamId,
    [Required, StringLength(200)] string Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    [Required] Guid[] ServiceIds, [Required] Guid[] ParticipantIds);
public sealed record InviteAppointmentRequest([Required, StringLength(200)] string Title,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, [Required] Guid[] ServiceIds, [Required] Guid[] ParticipantIds);
public sealed record ConfirmAppointment([Range(1, int.MaxValue)] int ExpectedVersion, Guid ResponsibleId);
public sealed record RescheduleAppointment([Range(1, int.MaxValue)] int ExpectedVersion,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, [Required, StringLength(1000)] string Reason);
public sealed record CancelAppointment([Range(1, int.MaxValue)] int ExpectedVersion, [Required, StringLength(1000)] string Reason);
public sealed record ConfigureAppointment([Range(1, int.MaxValue)] int ExpectedVersion, Guid TeamId, [Required] Guid[] ServiceIds, [Required] Guid[] ParticipantIds);
public sealed record CreateAppointmentInvite(Guid CompanyId, Guid TeamId, Guid LocationId, DateTimeOffset ExpiresAt);
public sealed record RevokeAppointmentInvite([Range(1, int.MaxValue)] int ExpectedVersion);
public sealed record AppointmentResponse(Guid Id, Guid CompanyId, Guid? LocationId, Guid? TeamId,
    string Title, string Status, int Version, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    Guid? ResponsibleId, string[] ServiceCodes, Guid[] ParticipantIds, Guid? RequestedBy,
    Guid? RequestedViaInviteId, IReadOnlyList<AppointmentEvent> History)
{
    public static AppointmentResponse From(WorkPermitAppointment value) => new(value.Id, value.CompanyId,
        value.LocationId, value.TeamId, value.Title, value.Status, value.Version, value.StartsAt, value.EndsAt,
        value.ResponsibleId, value.ServiceCodes, value.ParticipantIds, value.RequestedBy, value.RequestedViaInviteId, value.ReadHistory());
}
public sealed record AppointmentPage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed record AppointmentTeam(Guid Id, Guid CompanyId, Guid? LocationId, string Name, Guid[] MemberIds);
public sealed record AppointmentInviteCreated(Guid Id, string InviteUrl, DateTimeOffset ExpiresAt, Guid TeamId, int Version);
public sealed record AppointmentInviteContext(Guid CompanyId, Guid TeamId, Guid LocationId,
    string TeamName, Guid[] ParticipantIds, DateTimeOffset ExpiresAt, string[] AllowedActions);
public sealed class AppointmentOptions
{
    public int LocationCapacity { get; set; } = 1;
    public int ResponsibleCapacity { get; set; } = 1;
    public int MaximumInviteHours { get; set; } = 168;
    public int MaximumPendingRequestsPerInvite { get; set; } = 20;
}
public sealed class InvalidAppointmentInviteException : Exception
{
    public InvalidAppointmentInviteException() : base("Convite inválido, expirado ou revogado.") { }
}
