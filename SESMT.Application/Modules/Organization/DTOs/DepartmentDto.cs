using System.ComponentModel.DataAnnotations;

namespace SESMT.Application.Modules.Organization.DTOs;

public class DepartmentCreateRequest
{
    [Required(ErrorMessage = "UnitId é obrigatório")]
    public Guid UnitId { get; set; }

    [Required(ErrorMessage = "Nome é obrigatório")]
    [StringLength(150)]
    public string Name { get; set; } = null!;

    [StringLength(500)]
    public string? Description { get; set; }

    public Guid? ResponsibleId { get; set; }
}

public class DepartmentUpdateRequest
{
    [Required(ErrorMessage = "Nome é obrigatório")]
    [StringLength(150)]
    public string Name { get; set; } = null!;

    [StringLength(500)]
    public string? Description { get; set; }

    public Guid? ResponsibleId { get; set; }
}

public class DepartmentResponse
{
    public Guid Id { get; set; }
    public Guid UnitId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? ResponsibleId { get; set; }
    public bool Active { get; set; }
}

public class DepartmentSummaryResponse
{
    public Guid Id { get; set; }
    public Guid UnitId { get; set; }
    public string Name { get; set; } = null!;
    public bool Active { get; set; }
}
