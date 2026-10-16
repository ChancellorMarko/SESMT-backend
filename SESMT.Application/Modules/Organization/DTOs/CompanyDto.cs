using System.ComponentModel.DataAnnotations;
using SESMT.Domain.Modules.Organization.Enums;

namespace SESMT.Application.Modules.Organization.DTOs;

public class CompanyCreateRequest
{
    [Required(ErrorMessage = "Razão social é obrigatória")]
    [StringLength(255)]
    public string CorporateName { get; set; } = null!;

    [Required(ErrorMessage = "Nome fantasia é obrigatório")]
    [StringLength(255)]
    public string FantasyName { get; set; } = null!;

    [Required(ErrorMessage = "CNPJ é obrigatório")]
    [StringLength(14, MinimumLength = 14)]
    public string Cnpj { get; set; } = null!;

    [EmailAddress(ErrorMessage = "Email inválido")]
    [StringLength(254)]
    public string? Email { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Tipo de empresa é obrigatório")]
    public CompanyType TipoEmpresa { get; set; }

    public Guid? OrganizationId { get; set; }
}

public class CompanyResponse
{
    public Guid Id { get; set; }
    public string CorporateName { get; set; } = null!;
    public string FantasyName { get; set; } = null!;
    public string Cnpj { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public Guid? OrganizationId { get; set; }
    public string TipoEmpresa { get; set; } = null!;
    public bool Active { get; set; }
}

public class CompanySummaryResponse
{
    public Guid Id { get; set; }
    public string CorporateName { get; set; } = null!;
    public string FantasyName { get; set; } = null!;
    public string TipoEmpresa { get; set; } = null!;
    public bool Active { get; set; }
}