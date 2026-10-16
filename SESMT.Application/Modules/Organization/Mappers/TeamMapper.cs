using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Domain.Modules.Organization.Entities;

namespace SESMT.Application.Modules.Organization.Mappers;

public static class TeamMapper
{
    /// <summary>
    /// Cria a entidade Team a partir do DTO de criação.
    /// Usa o construtor do domínio, que já valida as regras.
    /// </summary>
    public static Team ToEntity(TeamCreateRequest request) =>
        new(
            organizationId: request.OrganizationId,
            companyId: request.CompanyId,
            localId: request.LocalId,
            name: request.Name,
            description: request.Description,
            chiefId: request.ChiefId,
            participantIds: request.ParticipantIds);

    public static TeamResponse ToResponse(Team team) =>
        new()
        {
            Id = team.Id,
            OrganizationId = team.OrganizationId,
            CompanyId = team.CompanyId,
            LocalId = team.LocalId,
            Name = team.Name,
            Description = team.Description,
            ChiefId = team.ChiefId,
            ParticipantIds = team.ParticipantIds.AsReadOnly(),
            Active = team.Active && team.DeletedAt is null
        };

    public static TeamSummaryResponse ToSummary(Team team) =>
        new()
        {
            Id = team.Id,
            Name = team.Name,
            OrganizationId = team.OrganizationId,
            ChiefId = team.ChiefId,
            Active = team.Active && team.DeletedAt is null
        };
}