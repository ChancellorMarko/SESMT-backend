using SESMT.Application.Modules.Dashboard;
using SESMT.Application.Modules.WorkPermits.Services;

namespace SESMT.Tests.Unit.Dashboard;

public sealed class DashboardActivityEvolutionTests
{
    [Fact]
    public async Task Evolution_ReturnsChronologicalMonthsWithRealCountsAndZeroGaps()
    {
        var repository = new FakeDashboardRepository(
        [
            new(new DateTimeOffset(2026, 5, 12, 9, 0, 0, TimeSpan.Zero), "EM_VERIFICACAO", null, [], []),
            new(new DateTimeOffset(2026, 5, 13, 9, 0, 0, TimeSpan.Zero), "LIBERADA", null, [], []),
            new(new DateTimeOffset(2026, 7, 4, 9, 0, 0, TimeSpan.Zero), "EM_EXECUCAO", null, [], []),
            new(new DateTimeOffset(2026, 8, 4, 9, 0, 0, TimeSpan.Zero), "CONCLUIDA", null, [], []),
            new(new DateTimeOffset(2026, 8, 5, 9, 0, 0, TimeSpan.Zero), "CANCELADA", null, [], [])
        ]);
        var service = new DashboardService(repository);

        var result = await service.GetActivityEvolutionAsync(Guid.NewGuid(), new(
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 31, 23, 59, 59, TimeSpan.Zero),
            null, null, null, null, null), CancellationToken.None);

        Assert.Equal("2026-05", result.Periodo.From);
        Assert.Equal("2026-08", result.Periodo.To);
        Assert.Equal(["2026-05", "2026-06", "2026-07", "2026-08"], result.Series.Select(x => x.Mes));
        Assert.Equal((0, 0, 2), (result.Series[0].Concluidas, result.Series[0].EmAndamento, result.Series[0].Pendentes));
        Assert.Equal((0, 0, 0), (result.Series[1].Concluidas, result.Series[1].EmAndamento, result.Series[1].Pendentes));
        Assert.Equal((0, 1, 0), (result.Series[2].Concluidas, result.Series[2].EmAndamento, result.Series[2].Pendentes));
        Assert.Equal((1, 0, 0), (result.Series[3].Concluidas, result.Series[3].EmAndamento, result.Series[3].Pendentes));
    }

    [Fact]
    public async Task Evolution_AppliesLocationTeamAndServiceFilters()
    {
        var localId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var repository = new FakeDashboardRepository(
        [
            new(DateTimeOffset.Parse("2026-05-12T09:00:00Z"), "CONCLUIDA", localId, [serviceId], [teamId]),
            new(DateTimeOffset.Parse("2026-05-13T09:00:00Z"), "CONCLUIDA", Guid.NewGuid(), [serviceId], [teamId]),
            new(DateTimeOffset.Parse("2026-05-14T09:00:00Z"), "CONCLUIDA", localId, [Guid.NewGuid()], [teamId]),
            new(DateTimeOffset.Parse("2026-05-15T09:00:00Z"), "CONCLUIDA", localId, [serviceId], [Guid.NewGuid()])
        ], new HashSet<Guid> { localId }, new HashSet<Guid> { teamId }, new HashSet<Guid> { serviceId });
        var service = new DashboardService(repository);

        var result = await service.GetActivityEvolutionAsync(Guid.NewGuid(), new(
            DateTimeOffset.Parse("2026-05-01T00:00:00Z"), DateTimeOffset.Parse("2026-05-31T23:59:59Z"),
            localId, teamId, serviceId, null, null), CancellationToken.None);

        Assert.Equal(1, result.Series.Single().Concluidas);
    }

    [Fact]
    public async Task Evolution_RejectsPeriodsLongerThanThreeYears()
    {
        var service = new DashboardService(new FakeDashboardRepository([]));

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetActivityEvolutionAsync(Guid.NewGuid(), new(
            DateTimeOffset.Parse("2020-01-01T00:00:00Z"), DateTimeOffset.Parse("2023-01-01T00:00:00Z"),
            null, null, null, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Evolution_RejectsFiltersOutsideAuthorizedScope()
    {
        var service = new DashboardService(new FakeDashboardRepository([]));

        await Assert.ThrowsAsync<WorkPermitForbiddenException>(() => service.GetActivityEvolutionAsync(Guid.NewGuid(), new(
            DateTimeOffset.Parse("2026-05-01T00:00:00Z"), DateTimeOffset.Parse("2026-05-31T23:59:59Z"),
            Guid.NewGuid(), null, null, null, null), CancellationToken.None));
    }

    private sealed class FakeDashboardRepository(
        IReadOnlyList<DashboardActivityTransition> transitions,
        IReadOnlySet<Guid>? localIds = null,
        IReadOnlySet<Guid>? teamIds = null,
        IReadOnlySet<Guid>? serviceIds = null) : IDashboardRepository
    {
        public Task<DashboardData> GetDataAsync(Guid actorId, CancellationToken cancellationToken, DashboardFilter? activityFilter = null) =>
            Task.FromResult(new DashboardData([], [], [], localIds ?? new HashSet<Guid>(),
                teamIds ?? new HashSet<Guid>(), serviceIds ?? new HashSet<Guid>(), transitions));
    }
}
