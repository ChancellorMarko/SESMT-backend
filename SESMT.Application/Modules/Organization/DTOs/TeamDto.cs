using System.ComponentModel.DataAnnotations;

namespace SESMT.Application.Modules.Organization.DTOs;

public class TeamCreateRequest
{
    [Required(ErrorMessage = "OrganizationId é obrigatório")]
    public Guid OrganizationId { get; set; }

    public Guid? CompanyId { get; set; }

    public Guid? LocalId { get; set; }

    [Required(ErrorMessage = "Nome é obrigatório")]
    [StringLength(150)]
    public string Name { get; set; } = null!;

    [StringLength(1000)]
    public string? Description { get; set; }

    public Guid? ChiefId { get; set; }

    public List<Guid> ParticipantIds { get; set; } = new();
}

public class TeamPatchRequest
{
    [StringLength(150)]
    public string? Name { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    public Guid? ChiefId { get; set; }

    public List<Guid>? ParticipantIds { get; set; }
}

public class TeamResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? LocalId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? ChiefId { get; set; }
    public IReadOnlyCollection<Guid> ParticipantIds { get; set; } = Array.Empty<Guid>();
    public bool Active { get; set; }
}

public class TeamSummaryResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = null!;
    public Guid? ChiefId { get; set; }
    public bool Active { get; set; }
}