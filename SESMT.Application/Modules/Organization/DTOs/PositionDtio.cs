using System.ComponentModel.DataAnnotations;

namespace SESMT.Application.Modules.Organization.DTOs;

public class PositionCreateRequest
{
    [Required(ErrorMessage = "CompanyId é obrigatório")]
    public Guid CompanyId { get; set; }

    [Required(ErrorMessage = "Nome é obrigatório")]
    [StringLength(150)]
    public string Name { get; set; } = null!;

    [StringLength(10)]
    public string? Cbo { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }
}

public class PositionUpdateRequest
{
    [Required(ErrorMessage = "Nome é obrigatório")]
    [StringLength(150)]
    public string Name { get; set; } = null!;

    [StringLength(10)]
    public string? Cbo { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }
}

public class PositionResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public string? Cbo { get; set; }
    public string? Description { get; set; }
    public bool Active { get; set; }
}

public class PositionSummaryResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public string? Cbo { get; set; }
    public bool Active { get; set; }
}
