namespace SESMT.Application.Modules.Appointments;

public static class AppointmentCapacity
{
    public static int Peak(IEnumerable<(DateTimeOffset StartsAt, DateTimeOffset EndsAt)> intervals, DateTimeOffset from, DateTimeOffset to)
    {
        var events = intervals.Where(i => i.StartsAt < to && i.EndsAt > from)
            .SelectMany(i => new[] {(Time: i.StartsAt < from ? from : i.StartsAt, Delta: 1), (Time: i.EndsAt > to ? to : i.EndsAt, Delta: -1)})
            .OrderBy(e => e.Time).ThenBy(e => e.Delta);
        var current = 0; var peak = 0;
        foreach (var point in events) { current += point.Delta; peak = Math.Max(peak, current); }
        return peak;
    }
}
