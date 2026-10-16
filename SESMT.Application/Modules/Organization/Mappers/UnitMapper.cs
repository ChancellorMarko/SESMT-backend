using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Domain.Modules.Organization.Entities;

namespace SESMT.Application.Modules.Organization.Mappers;

public static class UnitMapper
{
    /// <summary>
    /// Cria a entidade Unit a partir do DTO de criação.
    /// Usa o construtor do domínio — validações de negócio ficam na entidade.
    /// </summary>
    public static Unit ToEntity(UnitCreateRequest request) =>
        new(
            companyId: request.CompanyId,
            code: request.Code,
            name: request.Name,
            type: request.Type,
            cep: request.Cep,
            address: request.Address,
            number: request.Number,
            complement: request.Complement,
            neighborhood: request.Neighborhood,
            city: request.City,
            state: request.State);

    public static UnitResponse ToResponse(Unit unit) =>
        new()
        {
            Id = unit.Id,
            CompanyId = unit.CompanyId,
            Code = unit.Code,
            Name = unit.Name,
            Type = unit.Type,
            Cep = unit.CEP,
            Address = unit.Address,
            Number = unit.Number,
            Complement = unit.Complement,
            Neighborhood = unit.Neighborhood,
            City = unit.City,
            State = unit.State,
            Active = unit.Active && unit.DeletedAt is null
        };

    public static UnitSummaryResponse ToSummary(Unit unit) =>
        new()
        {
            Id = unit.Id,
            CompanyId = unit.CompanyId,
            Code = unit.Code,
            Name = unit.Name,
            Type = unit.Type,
            Active = unit.Active && unit.DeletedAt is null
        };
}