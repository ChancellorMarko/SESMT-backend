using System.Security.Cryptography;
using System.Text;
using SESMT.Application.Exceptions;
using SESMT.Application.Modules.WorkPermits.Constants;
using SESMT.Application.Modules.WorkPermits.Services;
using SESMT.Domain.Exceptions;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Application.Modules.Appointments;

public sealed class AppointmentService(IAppointmentRepository repository, AppointmentOptions options, TimeProvider clock)
{
    private static readonly string[] ReadRoles = [WorkPermitRoleCodes.Requester, WorkPermitRoleCodes.Operator,
        WorkPermitRoleCodes.Releaser, WorkPermitRoleCodes.Configurator];

    public async Task<AppointmentResponse> CreateAsync(RequestAppointment request, Guid actor, CancellationToken ct = default)
    {
        if (request.CompanyId == Guid.Empty || request.LocationId == Guid.Empty || request.TeamId == Guid.Empty || request.ServiceIds is null)
            throw new ArgumentException("Empresa, local, equipe e serviços são obrigatórios.");
        await AuthorizeAsync(actor, request.CompanyId, false, ct);
        ValidateFuturePeriod(request.StartsAt, request.EndsAt);
        await ValidateTeamAsync(request.CompanyId, request.LocationId, request.TeamId, request.ParticipantIds, request.StartsAt, request.EndsAt, ct);
        var appointment = WorkPermitAppointment.CreateRequest(request.CompanyId, request.Title, request.StartsAt, request.EndsAt,
            actor, request.LocationId, request.TeamId, WorkPermitServiceCatalogEntry.ResolveServiceCodes(request.ServiceIds).ToArray(), request.ParticipantIds);
        await repository.AddAsync(appointment, ct); await repository.SaveAsync(ct);
        return AppointmentResponse.From(appointment);
    }

    public async Task<AppointmentPage<AppointmentResponse>> ListAsync(Guid actor, Guid? company, DateTimeOffset? from,
        DateTimeOffset? to, string? status, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize || search?.Length > 200
            || from?.Offset != TimeSpan.Zero && from.HasValue || to?.Offset != TimeSpan.Zero && to.HasValue
            || from.HasValue && to.HasValue && to <= from || status is not (null or "" or WorkPermitAppointment.Requested or WorkPermitAppointment.Confirmed or WorkPermitAppointment.Cancelled))
            throw new ArgumentException("Filtros ou paginação inválidos.");
        var companies = await ReadCompaniesAsync(actor, company, ct);
        var result = await repository.ListAsync(companies, from, to, status, search?.Trim(), page, pageSize, ct);
        return new(result.Items.Select(AppointmentResponse.From).ToArray(), result.Total, page, pageSize);
    }

    public async Task<AppointmentResponse> GetAsync(Guid id, Guid actor, CancellationToken ct = default) =>
        AppointmentResponse.From(await GetAuthorizedAsync(id, actor, false, ct));

    public Task<AppointmentResponse> ConfirmAsync(Guid id, ConfirmAppointment request, Guid actor, CancellationToken ct = default) =>
        repository.SerializedAsync(async () =>
        {
            var appointment = await GetAuthorizedAsync(id, actor, true, ct);
            if (appointment.Status == WorkPermitAppointment.Confirmed && appointment.ResponsibleId == request.ResponsibleId
                && appointment.Version == request.ExpectedVersion + 1) return AppointmentResponse.From(appointment);
            appointment.EnsureVersion(request.ExpectedVersion);
            ValidateFuturePeriod(appointment.StartsAt, appointment.EndsAt);
            await ValidateCurrentContextAsync(appointment, ct);
            if (!await repository.ActiveUserAsync(request.ResponsibleId, appointment.CompanyId, ct)) throw new NotFoundException("Responsável não encontrado.");
            if (!await repository.CanAttendAsync(request.ResponsibleId, appointment.CompanyId, appointment.LocationId ?? Guid.Empty,
                appointment.ServiceCodes, appointment.StartsAt, appointment.EndsAt, ct)) throw new WorkPermitForbiddenException();
            await CheckCapacityAsync(appointment, request.ResponsibleId, appointment.StartsAt, appointment.EndsAt, ct);
            appointment.Confirm(actor, request.ResponsibleId, request.ExpectedVersion);
            await repository.SaveAsync(ct); return AppointmentResponse.From(appointment);
        }, ct);

    public Task<AppointmentResponse> RescheduleAsync(Guid id, RescheduleAppointment request, Guid actor, CancellationToken ct = default) =>
        repository.SerializedAsync(async () =>
        {
            var appointment = await GetAuthorizedAsync(id, actor, true, ct);
            appointment.EnsureVersion(request.ExpectedVersion);
            await EnsureNoPermitAsync(id, ct); ValidateFuturePeriod(request.StartsAt, request.EndsAt);
            await ValidateTeamAsync(appointment.CompanyId, appointment.LocationId ?? Guid.Empty, appointment.TeamId ?? Guid.Empty,
                appointment.ParticipantIds, request.StartsAt, request.EndsAt, ct);
            if (appointment.Status == WorkPermitAppointment.Confirmed && appointment.ResponsibleId.HasValue)
            {
                if (!await repository.CanAttendAsync(appointment.ResponsibleId.Value, appointment.CompanyId, appointment.LocationId ?? Guid.Empty,
                    appointment.ServiceCodes, request.StartsAt, request.EndsAt, ct)) throw new WorkPermitForbiddenException();
                await CheckCapacityAsync(appointment, appointment.ResponsibleId.Value, request.StartsAt, request.EndsAt, ct);
            }
            appointment.Reschedule(request.StartsAt, request.EndsAt, request.Reason, actor, request.ExpectedVersion);
            await repository.SaveAsync(ct); return AppointmentResponse.From(appointment);
        }, ct);

    public Task<AppointmentResponse> CancelAsync(Guid id, CancelAppointment request, Guid actor, CancellationToken ct = default) =>
        repository.SerializedAsync(async () =>
        {
            var appointment = await GetAuthorizedAsync(id, actor, true, ct);
            await EnsureNoPermitAsync(id, ct);
            appointment.Cancel(request.Reason, actor, request.ExpectedVersion);
            await repository.SaveAsync(ct); return AppointmentResponse.From(appointment);
        }, ct);

    public Task<AppointmentResponse> ConfigureAsync(Guid id, ConfigureAppointment request, Guid actor, CancellationToken ct = default) =>
        repository.SerializedAsync(async () =>
        {
            var appointment = await GetAuthorizedAsync(id, actor, true, ct);
            await EnsureNoPermitAsync(id, ct);
            await ValidateTeamAsync(appointment.CompanyId, appointment.LocationId ?? Guid.Empty, request.TeamId, request.ParticipantIds, appointment.StartsAt, appointment.EndsAt, ct);
            appointment.ConfigureContext(request.TeamId, WorkPermitServiceCatalogEntry.ResolveServiceCodes(request.ServiceIds).ToArray(), request.ParticipantIds, actor, request.ExpectedVersion);
            await repository.SaveAsync(ct); return AppointmentResponse.From(appointment);
        }, ct);

    public async Task<AppointmentInviteCreated> CreateInviteAsync(CreateAppointmentInvite request, Guid actor, CancellationToken ct = default)
    {
        if (request.CompanyId == Guid.Empty || request.LocationId == Guid.Empty || request.TeamId == Guid.Empty)
            throw new ArgumentException("Empresa, local e equipe são obrigatórios.");
        await AuthorizeAsync(actor, request.CompanyId, true, ct);
        var now = clock.GetUtcNow();
        if (options.MaximumInviteHours < 1 || request.ExpiresAt.Offset != TimeSpan.Zero || request.ExpiresAt <= now || request.ExpiresAt > now.AddHours(options.MaximumInviteHours))
            throw new ArgumentException("Validade do convite fora do limite configurado.");
        var team = await repository.GetTeamAsync(request.TeamId, request.CompanyId, now, now, ct) ?? throw new NotFoundException("Equipe não encontrada.");
        if (!await repository.ActiveLocationAsync(request.LocationId, request.CompanyId, ct) || team.LocationId.HasValue && team.LocationId != request.LocationId)
            throw new NotFoundException("Local da equipe não encontrado.");
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var invite = new AppointmentInvite(request.CompanyId, request.TeamId, request.LocationId, Hash(token), request.ExpiresAt, actor, now);
        await repository.AddInviteAsync(invite, ct); await repository.SaveAsync(ct);
        return new(invite.Id, $"/convite/{token}", invite.ExpiresAt, invite.TeamId, invite.Version);
    }

    public Task RevokeInviteAsync(Guid id, RevokeAppointmentInvite request, Guid actor, CancellationToken ct = default) =>
        repository.SerializedAsync(async () =>
        {
            var companies = await repository.AccessibleCompaniesAsync(actor, ct);
            var invite = await repository.GetInviteAsync(id, companies, ct) ?? throw new NotFoundException("Convite não encontrado.");
            await AuthorizeAsync(actor, invite.CompanyId, true, ct);
            invite.Revoke(actor, request.ExpectedVersion, clock.GetUtcNow()); await repository.SaveAsync(ct); return true;
        }, ct);

    public async Task<AppointmentInviteContext> InviteContextAsync(string token, CancellationToken ct = default)
    {
        var invite = await ValidInviteAsync(token, ct);
        var now = clock.GetUtcNow();
        var team = await repository.GetTeamAsync(invite.TeamId, invite.CompanyId, now, now, ct) ?? throw new InvalidAppointmentInviteException();
        return new(invite.CompanyId, invite.TeamId, invite.LocationId, team.Name, team.MemberIds, invite.ExpiresAt, ["REQUEST_APPOINTMENT", "READ_OWN_REQUESTS"]);
    }

    public Task<AppointmentResponse> CreateFromInviteAsync(string token, InviteAppointmentRequest request, CancellationToken ct = default) =>
        repository.SerializedAsync(async () =>
        {
            var invite = await ValidInviteAsync(token, ct); ValidateFuturePeriod(request.StartsAt, request.EndsAt);
            if (options.MaximumPendingRequestsPerInvite < 1 || await repository.PendingInviteRequestsAsync(invite.Id, ct) >= options.MaximumPendingRequestsPerInvite)
                throw new DomainException("Limite de solicitações pendentes deste convite atingido.");
            await ValidateTeamAsync(invite.CompanyId, invite.LocationId, invite.TeamId, request.ParticipantIds, request.StartsAt, request.EndsAt, ct);
            var appointment = WorkPermitAppointment.CreateFromInvite(invite.CompanyId, request.Title, request.StartsAt, request.EndsAt,
                invite.Id, invite.LocationId, invite.TeamId, WorkPermitServiceCatalogEntry.ResolveServiceCodes(request.ServiceIds).ToArray(), request.ParticipantIds);
            await repository.AddAsync(appointment, ct); await repository.SaveAsync(ct); return AppointmentResponse.From(appointment);
        }, ct);

    public async Task<AppointmentResponse> GetInviteRequestAsync(string token, Guid id, CancellationToken ct = default)
    {
        var invite = await ValidInviteAsync(token, ct);
        var appointment = await repository.GetAsync(id, [invite.CompanyId], ct);
        if (appointment?.RequestedViaInviteId != invite.Id) throw new NotFoundException("Solicitação não encontrada.");
        return AppointmentResponse.From(appointment);
    }

    private async Task<AppointmentInvite> ValidInviteAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token) || token.Length != 43 || token.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_')))
            throw new InvalidAppointmentInviteException();
        var invite = await repository.FindInviteAsync(Hash(token), ct);
        if (invite is null || !invite.IsUsableAt(clock.GetUtcNow()) || !await repository.ActiveLocationAsync(invite.LocationId, invite.CompanyId, ct)
            || await repository.GetTeamAsync(invite.TeamId, invite.CompanyId, clock.GetUtcNow(), clock.GetUtcNow(), ct) is null)
            throw new InvalidAppointmentInviteException();
        return invite;
    }
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private async Task ValidateCurrentContextAsync(WorkPermitAppointment appointment, CancellationToken ct) =>
        _ = await ValidateTeamAsync(appointment.CompanyId, appointment.LocationId ?? Guid.Empty, appointment.TeamId ?? Guid.Empty,
            appointment.ParticipantIds, appointment.StartsAt, appointment.EndsAt, ct);
    private async Task<AppointmentTeam> ValidateTeamAsync(Guid company, Guid location, Guid teamId, Guid[] participants, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        if (company == Guid.Empty || location == Guid.Empty || teamId == Guid.Empty || participants is null)
            throw new ArgumentException("Informe empresa, local, equipe e participantes válidos.");
        var team = await repository.GetTeamAsync(teamId, company, from, to, ct) ?? throw new NotFoundException("Equipe não encontrada.");
        if (!await repository.ActiveLocationAsync(location, company, ct) || team.LocationId.HasValue && team.LocationId != location
            || participants.Length == 0 || participants.Distinct().Count() != participants.Length || participants.Any(id => !team.MemberIds.Contains(id)))
            throw new ArgumentException("Local ou participantes incompatíveis com a equipe e o período.");
        return team;
    }
    private void ValidateFuturePeriod(DateTimeOffset from, DateTimeOffset to)
    {
        if (from.Offset != TimeSpan.Zero || to.Offset != TimeSpan.Zero || from < clock.GetUtcNow() || to <= from)
            throw new ArgumentException("Informe um período futuro em UTC, com término posterior ao início.");
    }
    private async Task CheckCapacityAsync(WorkPermitAppointment appointment, Guid responsible, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var counts = await repository.OverlapsAsync(appointment.Id, appointment.LocationId ?? Guid.Empty, responsible, from, to, ct);
        if (options.LocationCapacity < 1 || options.ResponsibleCapacity < 1 || counts.Location >= options.LocationCapacity || counts.Responsible >= options.ResponsibleCapacity)
            throw new DomainException("Não há disponibilidade para este responsável ou local no período.");
    }
    private async Task EnsureNoPermitAsync(Guid id, CancellationToken ct)
    {
        if (await repository.HasPermitAsync(id, ct)) throw new DomainException("O agendamento possui uma PT; use o fluxo da PT para resolver seu contexto antes de alterar a agenda.");
    }
    private async Task AuthorizeAsync(Guid actor, Guid company, bool manage, CancellationToken ct)
    {
        if (!(await repository.AccessibleCompaniesAsync(actor, ct)).Contains(company)) throw new NotFoundException("Empresa não encontrada.");
        var roles = manage ? new[] {WorkPermitRoleCodes.Configurator} : new[] {WorkPermitRoleCodes.Requester, WorkPermitRoleCodes.Configurator};
        foreach (var role in roles) if (await repository.HasRoleAsync(actor, company, role, ct)) return;
        throw new WorkPermitForbiddenException();
    }
    private async Task<IReadOnlyList<Guid>> ReadCompaniesAsync(Guid actor, Guid? selected, CancellationToken ct)
    {
        var companies = await repository.AccessibleCompaniesAsync(actor, ct);
        if (selected.HasValue && !companies.Contains(selected.Value)) throw new NotFoundException("Empresa não encontrada.");
        var permitted = new List<Guid>();
        foreach (var company in companies.Where(id => !selected.HasValue || id == selected.Value))
            foreach (var role in ReadRoles)
                if (await repository.HasRoleAsync(actor, company, role, ct)) { permitted.Add(company); break; }
        if (permitted.Count == 0) throw new WorkPermitForbiddenException();
        return permitted;
    }
    private async Task<WorkPermitAppointment> GetAuthorizedAsync(Guid id, Guid actor, bool manage, CancellationToken ct)
    {
        var companies = await ReadCompaniesAsync(actor, null, ct);
        var appointment = await repository.GetAsync(id, companies, ct) ?? throw new NotFoundException("Agendamento não encontrado.");
        if (manage) await AuthorizeAsync(actor, appointment.CompanyId, true, ct);
        return appointment;
    }
}
