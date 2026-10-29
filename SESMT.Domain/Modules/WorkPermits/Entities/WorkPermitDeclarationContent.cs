using System.Text.Json;

namespace SESMT.Domain.Modules.WorkPermits.Entities;

/// <summary>Preserves the actual statement and scheduling context; it is not an electronic signature.</summary>
public static class WorkPermitDeclarationContent
{
    public static string Create(WorkPermitAppointment appointment, IEnumerable<WorkPermitTemplateVersion> templates)
    {
        var context = JsonSerializer.Serialize(new
        {
            appointment.Id, appointment.Version, appointment.LocationId, appointment.TeamId, appointment.ResponsibleId,
            From = appointment.StartsAt.ToUniversalTime(), Until = appointment.EndsAt.ToUniversalTime(),
            Services = appointment.ServiceCodes.Order(StringComparer.Ordinal).ToArray(),
            Participants = appointment.ParticipantIds.Order().ToArray()
        });
        return context + "\n\n" + string.Join("\n\n", templates.OrderBy(item => item.Id)
            .Select(item => $"{item.TemplateId} / {item.Version}: {item.Declaration}"));
    }
}
