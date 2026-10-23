using SESMT.Domain.Common.AggregateRoot;
using SESMT.Domain.ValueObjects;

namespace SESMT.Domain.Modules.Workforce.Entities;

public class Worker : AggregateRootBase
{
    public Guid CompanyId { get; private set; }
    public string Enrollment { get; private set; } = null!;
    public byte[] CpfEncryption { get; private set; } = null!;
    public string CpfHash { get; private set; } = null!;
    public WorkerName Name { get; private set; } = null!;
    public DateOnly DateOfBirth { get; private set; }
    public DateOnly AdmissionDate { get; private set; }
    public DateOnly? DismissalDate { get; private set; }
    public Guid? PositionId { get; private set; }
    public Guid? UnitId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? UserId { get; private set; }

    protected Worker() { }

    public Worker(
        Guid companyId,
        string enrollment,
        byte[] cpfEncryption,
        string cpfHash,
        string name,
        DateOnly dateOfBirth,
        DateOnly admissionDate,
        DateOnly? dismissalDate,
        Guid? positionId,
        Guid? unitId,
        Guid? departmentId,
        Guid? userId)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("ID da empresa é obrigatório.");

        if (string.IsNullOrWhiteSpace(enrollment))
            throw new ArgumentException("Matrícula do trabalhador é obrigatória.");

        if (cpfEncryption is null || cpfEncryption.Length == 0)
            throw new ArgumentException("CPF do trabalhador é obrigatório.");

        if (string.IsNullOrWhiteSpace(cpfHash))
            throw new ArgumentException("Hash do CPF do trabalhador é obrigatório.");

        if (dismissalDate.HasValue && dismissalDate.Value < admissionDate)
            throw new ArgumentException("Data de demissão não pode ser anterior à admissão.");

        CompanyId = companyId;
        Enrollment = enrollment.Trim();
        CpfEncryption = cpfEncryption;
        CpfHash = cpfHash.Trim();
        Name = new WorkerName(name);
        DateOfBirth = dateOfBirth;
        AdmissionDate = admissionDate;
        DismissalDate = dismissalDate;
        PositionId = positionId;
        UnitId = unitId;
        DepartmentId = departmentId;
        UserId = userId;
    }

    public void Update(
        string enrollment,
        string name,
        DateOnly dateOfBirth,
        DateOnly admissionDate,
        DateOnly? dismissalDate,
        Guid? positionId,
        Guid? unitId,
        Guid? departmentId)
    {
        if (string.IsNullOrWhiteSpace(enrollment))
            throw new ArgumentException("Matrícula do trabalhador é obrigatória.");

        if (dismissalDate.HasValue && dismissalDate.Value < admissionDate)
            throw new ArgumentException("Data de demissão não pode ser anterior à admissão.");

        Enrollment = enrollment.Trim();
        Name = new WorkerName(name);
        DateOfBirth = dateOfBirth;
        AdmissionDate = admissionDate;
        DismissalDate = dismissalDate;
        PositionId = positionId;
        UnitId = unitId;
        DepartmentId = departmentId;
        MarkAsUpdated();
    }

    public void ChangeAssignment(Guid? positionId, Guid? unitId, Guid? departmentId)
    {
        PositionId = positionId;
        UnitId = unitId;
        DepartmentId = departmentId;
        MarkAsUpdated();
    }

    public void Terminate(DateOnly dismissalDate)
    {
        if (dismissalDate < AdmissionDate)
            throw new ArgumentException("Data de demissão não pode ser anterior à admissão.");

        if (DismissalDate.HasValue)
            throw new InvalidOperationException("Trabalhador já possui demissão registrada.");

        DismissalDate = dismissalDate;
        MarkAsUpdated();
    }

    public void LinkUser(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("ID do usuário é obrigatório.");

        UserId = userId;
        MarkAsUpdated();
    }

    public void Deactivate() => MarkAsDeleted();
}
