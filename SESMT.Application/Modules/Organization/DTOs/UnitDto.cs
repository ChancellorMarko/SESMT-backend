using System.ComponentModel.DataAnnotations;

namespace SESMT.Application.Modules.Organization.DTOs;

public class UnitCreateRequest
{
    [Required(ErrorMessage = "CompanyId é obrigatório")]
    public Guid CompanyId { get; set; }

    [Required(ErrorMessage = "Código é obrigatório")]
    [StringLength(50)]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Nome é obrigatório")]
    [StringLength(150)]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Tipo é obrigatório")]
    [StringLength(30)]
    public string Type { get; set; } = null!;

    [StringLength(8)]
    public string? Cep { get; set; }

    [StringLength(200)]
    public string? Address { get; set; }

    [StringLength(20)]
    public string? Number { get; set; }

    [StringLength(100)]
    public string? Complement { get; set; }

    [StringLength(100)]
    public string? Neighborhood { get; set; }

    [StringLength(120)]
    public string? City { get; set; }

    [StringLength(2)]
    public string? State { get; set; }

    public Guid? ResponsibleId { get; set; }
}

public class UnitUpdateRequest
{
    [Required(ErrorMessage = "Código é obrigatório")]
    [StringLength(50)]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Nome é obrigatório")]
    [StringLength(150)]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Tipo é obrigatório")]
    [StringLength(30)]
    public string Type { get; set; } = null!;

    [StringLength(8)]
    public string? Cep { get; set; }

    [StringLength(200)]
    public string? Address { get; set; }

    [StringLength(20)]
    public string? Number { get; set; }

    [StringLength(100)]
    public string? Complement { get; set; }

    [StringLength(100)]
    public string? Neighborhood { get; set; }

    [StringLength(120)]
    public string? City { get; set; }

    [StringLength(2)]
    public string? State { get; set; }
}

public class UnitResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string? Cep { get; set; }
    public string? Address { get; set; }
    public string? Number { get; set; }
    public string? Complement { get; set; }
    public string? Neighborhood { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public Guid? ResponsibleId { get; set; }
    public bool Active { get; set; }
}

public class UnitSummaryResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public bool Active { get; set; }
}