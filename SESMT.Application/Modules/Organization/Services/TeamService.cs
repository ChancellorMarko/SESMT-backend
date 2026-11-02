using SESMT.Application.Common;
using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Application.Modules.Organization.Interfaces;
using SESMT.Application.Modules.Organization.Mappers;
using SESMT.Domain.Common.Result;
using SESMT.Domain.Modules.Organization.Enums;
using SESMT.Domain.Modules.Organization.Repositories;
using SESMT.Domain.Modules.Workforce.Repositories;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Exceptions;

namespace SESMT.Application.Modules.Organization.Services;

public sealed class TeamService : ITeamService
{
    private readonly ITeamRepository _teamRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IWorkerRepository _workerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TeamService(
        ITeamRepository teamRepository,
        ICompanyRepository companyRepository,
        IWorkerRepository workerRepository,
        IUnitOfWork unitOfWork)
    {
        _teamRepository = teamRepository;
        _companyRepository = companyRepository;
        _workerRepository = workerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TeamResponse>> CreateAsync(
        TeamCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        // ─── RN-04: equipe pertence à organização autorizada ─────────────
        if (request.CompanyId is null)
            return Result<TeamResponse>.Failure(
                "É obrigatório informar a empresa da equipe.");

        var company = await _companyRepository.GetByIdAsync(
            request.CompanyId.Value, cancellationToken);

        if (company is null)
            return Result<TeamResponse>.Failure("Empresa não encontrada.");

        // ─── RN-04: empresa precisa pertencer à organização ──────────────
        var organizationId = Team.OrganizationFor(company.Id, company.OrganizationId);
        if (request.OrganizationId != Guid.Empty && request.OrganizationId != organizationId)
        {
            return Result<TeamResponse>.Failure(
                "A empresa não pertence à organização informada.");
        }
        request.OrganizationId = organizationId;

        // ─── RN-04: chefe precisa ter vínculo compatível ────────────────
        if (request.ChiefId is null || request.ChiefId == Guid.Empty)
            return Result<TeamResponse>.Failure("É obrigatório informar o chefe da equipe.");

        var chiefIsCompatible = await _workerRepository.ExistsInCompanyAsync(
            request.ChiefId.Value, company.Id, cancellationToken);

        if (!chiefIsCompatible)
            return Result<TeamResponse>.Failure(
                "O chefe informado não possui vínculo ativo com a empresa.");
        var chief = await _workerRepository.GetByIdAsync(request.ChiefId.Value, cancellationToken);
        if (chief?.DismissalDate is not null)
            return Result<TeamResponse>.Failure("O chefe informado possui vínculo encerrado.");

        var participantError = await ValidarParticipantesAsync(company.Id, request.ParticipantIds, cancellationToken);
        if (participantError is not null) return Result<TeamResponse>.Failure(participantError);

        // ─── RN-04: prestadoras cruzadas exigem concessão explícita ─────
        // No MVP: rejeitamos. Concessão fica para Fase 2.
        if (company.TipoEmpresa == CompanyType.PRESTADORA)
        {
            var conflito = await ValidarParticipantesPrestadoraAsync(
                company, request.ParticipantIds, cancellationToken);

            if (conflito is not null)
                return Result<TeamResponse>.Failure(conflito);
        }

        // ─── Criar entidade ─────────────────────────────────────────────
        try
        {
            var team = TeamMapper.ToEntity(request);

            await _teamRepository.AddAsync(team, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<TeamResponse>.Success(TeamMapper.ToResponse(team));
        }
        catch (ArgumentException ex)
        {
            return Result<TeamResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<TeamResponse>> GetByIdAsync(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var team = await _teamRepository.GetByIdAsync(teamId, cancellationToken);

        return team is null
            ? Result<TeamResponse>.Failure("Equipe não encontrada.")
            : Result<TeamResponse>.Success(TeamMapper.ToResponse(team));
    }

    public async Task<Result<IEnumerable<TeamSummaryResponse>>> ListByLocationAsync(
        Guid locationId,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        if (pageSize <= 0) pageSize = 10;
        if (pageNumber <= 0) pageNumber = 1;

        var skip = (pageNumber - 1) * pageSize;

        var teams = await _teamRepository.ListByLocationAsync(
            locationId, skip, pageSize, cancellationToken);

        var dtos = teams.Select(TeamMapper.ToSummary).ToList();

        return Result<IEnumerable<TeamSummaryResponse>>.Success(dtos);
    }

    public async Task<Result<TeamResponse>> UpdateAsync(
        Guid teamId,
        TeamPatchRequest patchData,
        CancellationToken cancellationToken = default)
    {
        var team = await _teamRepository.GetByIdAsync(teamId, cancellationToken);
        if (team is null)
            return Result<TeamResponse>.Failure("Equipe não encontrada.");
        if (patchData.ExpectedVersion < 1) throw new ArgumentException("Informe a revisão esperada da equipe.");
        if (team.Version != patchData.ExpectedVersion) throw new DomainException("A equipe foi alterada. Atualize a revisão antes de salvar.");
        if (patchData.ClearDescription && patchData.Description is not null) throw new ArgumentException("Informe uma descrição ou solicite a remoção, sem combinar as duas opções.");

        // Revalidate the effective chief even when only other fields are edited.
        var effectiveChief = patchData.ChiefId ?? team.ChiefId;
        if (effectiveChief.HasValue && team.CompanyId.HasValue)
        {
            var isCompatible = await _workerRepository.ExistsInCompanyAsync(
                effectiveChief.Value, team.CompanyId.Value, cancellationToken);

            if (!isCompatible)
                return Result<TeamResponse>.Failure(
                    "O novo chefe não possui vínculo ativo com a empresa da equipe.");
            var chief = await _workerRepository.GetByIdAsync(effectiveChief.Value, cancellationToken);
            if (chief?.DismissalDate is not null)
                return Result<TeamResponse>.Failure("O chefe informado possui vínculo encerrado.");
        }

        // Validação de participantes — todos devem pertencer à mesma empresa
        if (team.CompanyId.HasValue)
        {
            var participantesInvalidos = await ValidarParticipantesAsync(
                team.CompanyId.Value, patchData.ParticipantIds ?? team.ParticipantIds, cancellationToken);

            if (participantesInvalidos is not null)
                return Result<TeamResponse>.Failure(participantesInvalidos);
        }

        try
        {
            team.Update(
                name: patchData.Name ?? team.Name,
                localId: team.LocalId,
                description: patchData.ClearDescription ? null : patchData.Description ?? team.Description,
                chiefId: patchData.ChiefId ?? team.ChiefId,
                participantIds: patchData.ParticipantIds ?? team.ParticipantIds);

            _teamRepository.Update(team);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<TeamResponse>.Success(TeamMapper.ToResponse(team));
        }
        catch (ArgumentException ex)
        {
            return Result<TeamResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<bool>> DeactivateAsync(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var team = await _teamRepository.GetByIdAsync(teamId, cancellationToken);
        if (team is null)
            return Result<bool>.Failure("Equipe não encontrada.");

        team.Deactivate();   // soft delete — preserva histórico (regra do card)

        _teamRepository.Update(team);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }

    // ─── Helpers privados ────────────────────────────────────────────────

    /// <summary>
    /// Valida se todos os participantes pertencem à empresa da equipe.
    /// Retorna mensagem de erro ou null se tudo OK.
    /// </summary>
    private async Task<string?> ValidarParticipantesAsync(
        Guid companyId,
        IEnumerable<Guid> participantIds,
        CancellationToken ct)
    {
        var ids = participantIds.Distinct().ToList();
        if (ids.Count == 0)
            return null;

        var workers = await _workerRepository.GetByIdsAsync(ids, ct);
        var workersEncontrados = workers.Select(w => w.Id).ToHashSet();

        var idsFaltando = ids.Where(id => !workersEncontrados.Contains(id)).ToList();
        if (idsFaltando.Count > 0)
            return $"Participante(s) não encontrado(s): {string.Join(", ", idsFaltando)}.";

        var foraDaEmpresa = workers.Where(w => w.CompanyId != companyId).ToList();
        if (foraDaEmpresa.Count > 0)
            return "Todos os participantes precisam estar vinculados à mesma empresa da equipe.";

        if (workers.Any(w => w.DismissalDate.HasValue))
            return "Participantes com vínculo encerrado precisam ser removidos da equipe.";

        return null;
    }

    /// <summary>
    /// Regra específica para prestadoras: chefe/participantes de prestadoras
    /// diferentes da contratante exigem concessão explícita (Fase 2).
    /// No MVP, rejeitamos.
    /// </summary>
    private async Task<string?> ValidarParticipantesPrestadoraAsync(
        Domain.Modules.Organization.Entities.Company prestadora,
        IEnumerable<Guid> participantIds,
        CancellationToken ct)
    {
        var ids = participantIds.Distinct().ToList();
        if (ids.Count == 0)
            return null;

        var workers = await _workerRepository.GetByIdsAsync(ids, ct);

        // Se algum participante é de outra empresa (outra prestadora/contratante),
        // rejeitamos — o MVP exige concessão explícita para isso.
        var deOutraEmpresa = workers
            .Where(w => w.CompanyId != prestadora.Id)
            .ToList();

        if (deOutraEmpresa.Count > 0)
            return "Associações entre prestadoras diferentes exigem concessão explícita " +
                   "(não suportado nesta versão).";

        return null;
    }
}
