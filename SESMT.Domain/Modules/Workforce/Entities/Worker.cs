using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.Workforce.Entities;

public class Worker : AggregateRootBase
{
    public Guid CompanyId { get; private set; }
    public string Enrollment { get; private set; }
    public string CpfEncryption { get; private set; }
    public string CpfHash { get; private set; }
    public string Name { get; private set; }
    public DateTime DateOfBirth { get; private set; }
    public DateTime AdmissionDate { get; private set; }
    public DateTime? DismissalDate { get; private set; }
    public Guid? PositionId { get; private set; }
    public Guid? RoleId { get; private set; }
    public Guid? UnitId { get; private set; }
    public Guid? UserId { get; private set; }

    protected Worker() { }

    public Worker(Guid companyId, string enrollment, string cpfEncryption, string cpfHash, string name, DateTime dateOfBirth, DateTime admissionDate, DateTime? dismissalDate, Guid? positionId, Guid? roleId, Guid? unitId, Guid? userId)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("ID da empresa é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(enrollment))
        {
            throw new ArgumentException("Matrícula do trabalhador é obrigatória.");
        }
        if (string.IsNullOrWhiteSpace(cpfEncryption))
        {
            throw new ArgumentException("CPF do trabalhador é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(cpfHash))
        {
            throw new ArgumentException("Hash do CPF do trabalhador é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nome do trabalhador é obrigatório.");
        }

        CompanyId = companyId;
        Enrollment = enrollment.Trim();
        CpfEncryption = cpfEncryption.Trim();
        CpfHash = cpfHash.Trim();
        Name = name.Trim();
        DateOfBirth = dateOfBirth;
        AdmissionDate = admissionDate;
        DismissalDate = dismissalDate;
        PositionId = positionId;
        RoleId = roleId;
        UnitId = unitId;
        UserId = userId;
    }

    public void Update(string enrollment, string name, DateTime dateOfBirth, DateTime admissionDate, DateTime? dismissalDate, Guid? positionId, Guid? roleId, Guid? unitId)
    {
        if (string.IsNullOrWhiteSpace(enrollment))
        {
            throw new ArgumentException("Matrícula do trabalhador é obrigatória.");
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nome do trabalhador é obrigatório.");
        }

        Enrollment = enrollment.Trim();
        Name = name.Trim();
        DateOfBirth = dateOfBirth;
        AdmissionDate = admissionDate;
        DismissalDate = dismissalDate;
        PositionId = positionId;
        RoleId = roleId;
        UnitId = unitId;
        MarkAsUpdated();
    }
}