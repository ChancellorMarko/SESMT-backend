using System.ComponentModel.DataAnnotations;

namespace SESMT.Application.Modules.Workforce.DTOs;

public class WorkerCreateRequest
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required(ErrorMessage = "Matrícula é obrigatória")]
    [StringLength(30)]
    public string Enrollment { get; set; } = null!;

    [Required(ErrorMessage = "CPF é obrigatório")]
    [StringLength(14)]
    public string Cpf { get; set; } = null!;

    [Required(ErrorMessage = "Nome é obrigatório")]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    [Required]
    public DateOnly DateOfBirth { get; set; }

    [Required]
    public DateOnly AdmissionDate { get; set; }

    public DateOnly? DismissalDate { get; set; }

    public Guid? PositionId { get; set; }
    public Guid? UnitId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? UserId { get; set; }
}

public class WorkerUpdateRequest
{
    [Required(ErrorMessage = "Matrícula é obrigatória")]
    [StringLength(30)]
    public string Enrollment { get; set; } = null!;

    [Required(ErrorMessage = "Nome é obrigatório")]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    [Required]
    public DateOnly DateOfBirth { get; set; }

    [Required]
    public DateOnly AdmissionDate { get; set; }

    public DateOnly? DismissalDate { get; set; }
    public Guid? PositionId { get; set; }
    public Guid? UnitId { get; set; }
    public Guid? DepartmentId { get; set; }

    /// <summary>Motivo da mudança (usado se cargo/setor mudar).</summary>
    [StringLength(200)]
    public string? ChangeReason { get; set; }
}

public class WorkerResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Enrollment { get; set; } = null!;
    public string CpfMasked { get; set; } = null!;   // "***.***.***-XX"
    public string Name { get; set; } = null!;
    public DateOnly DateOfBirth { get; set; }
    public DateOnly AdmissionDate { get; set; }
    public DateOnly? DismissalDate { get; set; }
    public Guid? PositionId { get; set; }
    public Guid? UnitId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? UserId { get; set; }
    public bool Active { get; set; }
}

public class WorkerSummaryResponse
{
    public Guid Id { get; set; }
    public string Enrollment { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Guid? PositionId { get; set; }
    public Guid? DepartmentId { get; set; }
    public bool Active { get; set; }
}
