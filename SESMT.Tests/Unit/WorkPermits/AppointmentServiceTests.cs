using SESMT.Application.Exceptions;
using SESMT.Application.Modules.Appointments;
using SESMT.Application.Modules.WorkPermits.Services;
using SESMT.Domain.Exceptions;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Tests.Unit.WorkPermits;

public sealed class AppointmentServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid[] Services = WorkPermitServiceCatalogEntry.All.Take(2).Select(s => s.Id).ToArray();

    [Fact]
    public async Task RequestsPreserveCombinedServicesAndNeverConfirmExecution()
    {
        var repository = new FakeRepository(); var service = Create(repository);
        var result = await service.CreateAsync(repository.Request(), repository.Actor);
        Assert.Equal(WorkPermitAppointment.Requested, result.Status);
        Assert.Equal(new[] {"ALTURA", "QUENTE"}, result.ServiceCodes.Order());
        Assert.Null(result.ResponsibleId); Assert.Single(result.History); Assert.Equal(repository.Actor, result.RequestedBy);
    }

    [Fact]
    public async Task CapacityConflictPreservesPendingStateVersionAndHistory()
    {
        var repository = new FakeRepository(); var service = Create(repository);
        var first = await service.CreateAsync(repository.Request(), repository.Actor);
        await service.ConfirmAsync(first.Id, new(first.Version, repository.Actor), repository.Actor);
        var second = await service.CreateAsync(repository.Request(), repository.Actor);
        await Assert.ThrowsAsync<DomainException>(() => service.ConfirmAsync(second.Id, new(second.Version, repository.Actor), repository.Actor));
        var stored = repository.Appointments.Single(a => a.Id == second.Id);
        Assert.Equal(WorkPermitAppointment.Requested, stored.Status); Assert.Equal(1, stored.Version);
        Assert.Single(stored.ReadHistory()); Assert.Null(stored.ResponsibleId);
    }

    [Fact]
    public async Task ConfirmationRepeatDoesNotDuplicateTheDecision()
    {
        var repository = new FakeRepository(); var service = Create(repository);
        var value = await service.CreateAsync(repository.Request(), repository.Actor);
        var command = new ConfirmAppointment(value.Version, repository.Actor);
        var first = await service.ConfirmAsync(value.Id, command, repository.Actor);
        var repeated = await service.ConfirmAsync(value.Id, command, repository.Actor);
        Assert.Equal(first.Version, repeated.Version); Assert.Equal(2, repeated.History.Count);
        Assert.Equal(WorkPermitAppointment.Confirmed, repeated.Status);
    }

    [Fact]
    public async Task MissingDesignationPreventsConfirmationEvenForAConfigurator()
    {
        var repository = new FakeRepository { Designated = false }; var service = Create(repository);
        var value = await service.CreateAsync(repository.Request(), repository.Actor);
        await Assert.ThrowsAsync<WorkPermitForbiddenException>(() => service.ConfirmAsync(value.Id, new(value.Version, repository.Actor), repository.Actor));
        Assert.Equal(WorkPermitAppointment.Requested, repository.Appointments.Single().Status);
    }

    [Fact]
    public async Task RescheduleKeepsBeforeAfterAndRejectsChangesWhenAPermitExists()
    {
        var repository = new FakeRepository(); var service = Create(repository);
        var value = await service.CreateAsync(repository.Request(), repository.Actor);
        var changed = await service.RescheduleAsync(value.Id, new(value.Version, Now.AddHours(3), Now.AddHours(4), "Mudança solicitada"), repository.Actor);
        var history = changed.History.Last();
        Assert.Equal(Now.AddHours(1), history.Before!.StartsAt); Assert.Equal(Now.AddHours(3), history.After.StartsAt);
        repository.HasPermit = true;
        await Assert.ThrowsAsync<DomainException>(() => service.RescheduleAsync(value.Id, new(changed.Version, Now.AddHours(5), Now.AddHours(6), "Nova mudança"), repository.Actor));
        Assert.Equal(changed.Version, repository.Appointments.Single().Version);
    }

    [Fact]
    public async Task AnotherCompanyCannotBeQueriedAndATeamCannotBeForged()
    {
        var repository = new FakeRepository(); var service = Create(repository);
        await Assert.ThrowsAsync<NotFoundException>(() => service.ListAsync(repository.Actor, Guid.NewGuid(), null, null, null, null, 1, 20));
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(repository.Request() with { TeamId = Guid.NewGuid() }, repository.Actor));
        Assert.Equal(0, repository.ListCalls); Assert.Empty(repository.Appointments);
    }

    [Fact]
    public async Task LinkIsHashedAndCreatesOnlyAnAnonymousRequestForItsOwnTeam()
    {
        var repository = new FakeRepository(); var service = Create(repository);
        var invite = await service.CreateInviteAsync(new(repository.Company, repository.Team, repository.Location, Now.AddHours(5)), repository.Actor);
        var token = invite.InviteUrl.Split('/').Last();
        Assert.Equal(43, token.Length); Assert.DoesNotContain(token, repository.Invites.Single().TokenHash);
        Assert.Equal(64, repository.Invites.Single().TokenHash.Length);
        var request = new InviteAppointmentRequest("Pedido por link", Now.AddHours(1), Now.AddHours(2), Services, [repository.Person]);
        var created = await service.CreateFromInviteAsync(token, request);
        Assert.Equal(repository.Team, created.TeamId); Assert.Equal(repository.Company, created.CompanyId);
        Assert.Equal(invite.Id, created.RequestedViaInviteId); Assert.Null(created.RequestedBy);
        Assert.Null(created.History.Single().ActorId); Assert.Null(repository.Appointments.Single().CreatedBy);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateFromInviteAsync(token, request with {ParticipantIds = [Guid.NewGuid()]}));
        Assert.Single(repository.Appointments);
    }

    [Fact]
    public async Task RevocationAndExpiryImmediatelyBlockReadsAndNewRequests()
    {
        var repository = new FakeRepository(); var clock = new TestClock(Now); var service = new AppointmentService(repository, new(), clock);
        var invite = await service.CreateInviteAsync(new(repository.Company, repository.Team, repository.Location, Now.AddHours(5)), repository.Actor);
        var token = invite.InviteUrl.Split('/').Last();
        await service.RevokeInviteAsync(invite.Id, new(invite.Version), repository.Actor);
        await Assert.ThrowsAsync<InvalidAppointmentInviteException>(() => service.InviteContextAsync(token));
        await Assert.ThrowsAsync<InvalidAppointmentInviteException>(() => service.CreateFromInviteAsync(token, new("Pedido", Now.AddHours(1), Now.AddHours(2), Services, [repository.Person])));
        var other = await service.CreateInviteAsync(new(repository.Company, repository.Team, repository.Location, Now.AddHours(1)), repository.Actor);
        clock.Now = Now.AddHours(1);
        await Assert.ThrowsAsync<InvalidAppointmentInviteException>(() => service.InviteContextAsync(other.InviteUrl.Split('/').Last()));
        Assert.Empty(repository.Appointments);
    }

    [Fact]
    public void CapacityCountsSimultaneousOccupancyAndAllowsTouchingIntervals()
    {
        var intervals = new[] {(Now, Now.AddHours(1)), (Now.AddHours(1), Now.AddHours(2))};
        Assert.Equal(1, AppointmentCapacity.Peak(intervals, Now, Now.AddHours(2)));
        Assert.Equal(2, AppointmentCapacity.Peak(intervals.Append((Now.AddMinutes(30), Now.AddMinutes(90))), Now, Now.AddHours(2)));
        Assert.Equal(0, AppointmentCapacity.Peak(intervals, Now.AddHours(2), Now.AddHours(3)));
    }

    private static AppointmentService Create(FakeRepository repository) => new(repository, new(), new TestClock(Now));
    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class FakeRepository : IAppointmentRepository
    {
        public Guid Company { get; } = Guid.NewGuid(); public Guid Team { get; } = Guid.NewGuid();
        public Guid Location { get; } = Guid.NewGuid(); public Guid Actor { get; } = Guid.NewGuid(); public Guid Person { get; } = Guid.NewGuid();
        public List<WorkPermitAppointment> Appointments { get; } = []; public List<AppointmentInvite> Invites { get; } = [];
        public bool Designated { get; set; } = true; public bool HasPermit { get; set; }
        public int ListCalls { get; private set; }
        public RequestAppointment Request() => new(Company, Location, Team, "Atendimento de teste", Now.AddHours(1), Now.AddHours(2), Services, [Person]);
        public Task<IReadOnlyList<Guid>> AccessibleCompaniesAsync(Guid actor, CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>(actor == Actor ? [Company] : []);
        public Task<bool> HasRoleAsync(Guid actor, Guid company, string role, CancellationToken ct) => Task.FromResult(actor == Actor && company == Company);
        public Task<bool> ActiveLocationAsync(Guid location, Guid company, CancellationToken ct) => Task.FromResult(location == Location && company == Company);
        public Task<bool> ActiveUserAsync(Guid user, Guid company, CancellationToken ct) => Task.FromResult(user == Actor && company == Company);
        public Task<bool> CanAttendAsync(Guid user, Guid company, Guid location, string[] services, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) => Task.FromResult(Designated);
        public Task<AppointmentTeam?> GetTeamAsync(Guid team, Guid company, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
            Task.FromResult<AppointmentTeam?>(team == Team && company == Company ? new(Team, Company, Location, "Equipe de teste", [Person]) : null);
        public Task<WorkPermitAppointment?> GetAsync(Guid id, IReadOnlyCollection<Guid> companies, CancellationToken ct) =>
            Task.FromResult(Appointments.FirstOrDefault(a => a.Id == id && companies.Contains(a.CompanyId)));
        public Task<AppointmentPage<WorkPermitAppointment>> ListAsync(IReadOnlyCollection<Guid> companies, DateTimeOffset? from, DateTimeOffset? to, string? status, string? search, int page, int pageSize, CancellationToken ct)
        { ListCalls++; return Task.FromResult(new AppointmentPage<WorkPermitAppointment>([], 0, page, pageSize)); }
        public Task<bool> HasPermitAsync(Guid appointment, CancellationToken ct) => Task.FromResult(HasPermit);
        public Task<(int Location, int Responsible)> OverlapsAsync(Guid except, Guid location, Guid responsible, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        {
            var occupied = Appointments.Where(a => a.Id != except && a.Status == WorkPermitAppointment.Confirmed).ToArray();
            return Task.FromResult((AppointmentCapacity.Peak(occupied.Where(a => a.LocationId == location).Select(a => (a.StartsAt, a.EndsAt)), from, to),
                AppointmentCapacity.Peak(occupied.Where(a => a.ResponsibleId == responsible).Select(a => (a.StartsAt, a.EndsAt)), from, to)));
        }
        public Task AddAsync(WorkPermitAppointment appointment, CancellationToken ct) { Appointments.Add(appointment); return Task.CompletedTask; }
        public Task<AppointmentInvite?> FindInviteAsync(string hash, CancellationToken ct) => Task.FromResult(Invites.FirstOrDefault(i => i.TokenHash == hash));
        public Task<AppointmentInvite?> GetInviteAsync(Guid id, IReadOnlyCollection<Guid> companies, CancellationToken ct) => Task.FromResult(Invites.FirstOrDefault(i => i.Id == id && companies.Contains(i.CompanyId)));
        public Task AddInviteAsync(AppointmentInvite invite, CancellationToken ct) { Invites.Add(invite); return Task.CompletedTask; }
        public Task<int> PendingInviteRequestsAsync(Guid invite, CancellationToken ct) => Task.FromResult(Appointments.Count(a => a.RequestedViaInviteId == invite && a.Status == WorkPermitAppointment.Requested));
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<T> SerializedAsync<T>(Func<Task<T>> action, CancellationToken ct) => action();
    }
}
