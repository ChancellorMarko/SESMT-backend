using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Tests.Unit.Workforce;

public class WorkerTests
{
    // ─── Helpers ────────────────────────────────────────────────────────────

    private static Worker CreateWorker() =>
        new(
            companyId: Guid.NewGuid(),
            enrollment: "MAT-001",
            cpfEncryption: [1, 2, 3],
            cpfHash: "cpf-hash",
            name: "João da Silva",
            dateOfBirth: new DateOnly(2000, 5, 10),
            admissionDate: new DateOnly(2025, 1, 6),
            dismissalDate: null,
            positionId: null,
            unitId: null,
            departmentId: null,
            userId: null);

    // ─── Construtor ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ShouldCreateWorker_WhenDataIsValid()
    {
        var companyId = Guid.NewGuid();

        var worker = new Worker(
            companyId: companyId,
            enrollment: " MAT-001 ",
            cpfEncryption: [1, 2, 3],
            cpfHash: " cpf-hash ",
            name: " João da Silva ",
            dateOfBirth: new DateOnly(2000, 5, 10),
            admissionDate: new DateOnly(2025, 1, 6),
            dismissalDate: null,
            positionId: null,
            unitId: null,
            departmentId: null,
            userId: null);

        Assert.Equal(companyId, worker.CompanyId);
        Assert.Equal("MAT-001", worker.Enrollment);
        Assert.Equal("cpf-hash", worker.CpfHash);
        Assert.Equal("João da Silva", worker.Name.Value);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCompanyIdIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            new Worker(
                companyId: Guid.Empty,
                enrollment: "MAT-001",
                cpfEncryption: [1, 2, 3],
                cpfHash: "cpf-hash",
                name: "João da Silva",
                dateOfBirth: new DateOnly(2000, 5, 10),
                admissionDate: new DateOnly(2025, 1, 6),
                dismissalDate: null,
                positionId: null,
                unitId: null,
                departmentId: null,
                userId: null));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenEnrollmentIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            new Worker(
                companyId: Guid.NewGuid(),
                enrollment: " ",
                cpfEncryption: [1, 2, 3],
                cpfHash: "cpf-hash",
                name: "João da Silva",
                dateOfBirth: new DateOnly(2000, 5, 10),
                admissionDate: new DateOnly(2025, 1, 6),
                dismissalDate: null,
                positionId: null,
                unitId: null,
                departmentId: null,
                userId: null));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCpfEncryptionIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            new Worker(
                companyId: Guid.NewGuid(),
                enrollment: "MAT-001",
                cpfEncryption: [],
                cpfHash: "cpf-hash",
                name: "João da Silva",
                dateOfBirth: new DateOnly(2000, 5, 10),
                admissionDate: new DateOnly(2025, 1, 6),
                dismissalDate: null,
                positionId: null,
                unitId: null,
                departmentId: null,
                userId: null));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDismissalDateIsBeforeAdmission()
    {
        Assert.Throws<ArgumentException>(() =>
            new Worker(
                companyId: Guid.NewGuid(),
                enrollment: "MAT-001",
                cpfEncryption: [1, 2, 3],
                cpfHash: "cpf-hash",
                name: "João da Silva",
                dateOfBirth: new DateOnly(2000, 5, 10),
                admissionDate: new DateOnly(2025, 1, 6),
                dismissalDate: new DateOnly(2024, 1, 1),
                positionId: null,
                unitId: null,
                departmentId: null,
                userId: null));
    }

    // ─── Update ─────────────────────────────────────────────────────────────

    [Fact]
    public void Update_ShouldUpdateWorkerData_WhenDataIsValid()
    {
        var worker = CreateWorker();

        worker.Update(
            enrollment: "MAT-002",
            name: "Maria da Silva",
            dateOfBirth: new DateOnly(1998, 8, 15),
            admissionDate: new DateOnly(2024, 3, 1),
            dismissalDate: null,
            positionId: null,
            unitId: null,
            departmentId: null);

        Assert.Equal("MAT-002", worker.Enrollment);
        Assert.Equal("Maria da Silva", worker.Name.Value);
        Assert.Equal(new DateOnly(1998, 8, 15), worker.DateOfBirth);
        Assert.Equal(new DateOnly(2024, 3, 1), worker.AdmissionDate);
    }

    [Fact]
    public void Update_ShouldThrow_WhenNameIsEmpty()
    {
        var worker = CreateWorker();

        Assert.Throws<ArgumentException>(() =>
            worker.Update(
                enrollment: "MAT-002",
                name: " ",
                dateOfBirth: new DateOnly(2000, 5, 10),
                admissionDate: new DateOnly(2025, 1, 6),
                dismissalDate: null,
                positionId: null,
                unitId: null,
                departmentId: null));
    }

    [Fact]
    public void Update_ShouldThrow_WhenDismissalDateIsBeforeAdmission()
    {
        var worker = CreateWorker();

        Assert.Throws<ArgumentException>(() =>
            worker.Update(
                enrollment: "MAT-002",
                name: "Maria da Silva",
                dateOfBirth: new DateOnly(1998, 8, 15),
                admissionDate: new DateOnly(2024, 3, 1),
                dismissalDate: new DateOnly(2023, 1, 1),
                positionId: null,
                unitId: null,
                departmentId: null));
    }

    // ─── Terminate ──────────────────────────────────────────────────────────

    [Fact]
    public void Terminate_ShouldSetDismissalDate()
    {
        var worker = CreateWorker();
        var dismissal = new DateOnly(2025, 12, 31);

        worker.Terminate(dismissal);

        Assert.Equal(dismissal, worker.DismissalDate);
    }

    [Fact]
    public void Terminate_ShouldThrow_WhenAlreadyTerminated()
    {
        var worker = CreateWorker();
        worker.Terminate(new DateOnly(2025, 12, 31));

        Assert.Throws<InvalidOperationException>(() =>
            worker.Terminate(new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Terminate_ShouldThrow_WhenDateIsBeforeAdmission()
    {
        var worker = CreateWorker();

        Assert.Throws<ArgumentException>(() =>
            worker.Terminate(new DateOnly(2024, 1, 1)));
    }

    // ─── ChangeAssignment ───────────────────────────────────────────────────

    [Fact]
    public void ChangeAssignment_ShouldUpdateFields()
    {
        var worker = CreateWorker();
        var positionId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();

        worker.ChangeAssignment(positionId, unitId, departmentId);

        Assert.Equal(positionId, worker.PositionId);
        Assert.Equal(unitId, worker.UnitId);
        Assert.Equal(departmentId, worker.DepartmentId);
    }

    // ─── LinkUser ───────────────────────────────────────────────────────────

    [Fact]
    public void LinkUser_ShouldSetUserId()
    {
        var worker = CreateWorker();
        var userId = Guid.NewGuid();

        worker.LinkUser(userId);

        Assert.Equal(userId, worker.UserId);
    }

    [Fact]
    public void LinkUser_ShouldThrow_WhenUserIdIsEmpty()
    {
        var worker = CreateWorker();

        Assert.Throws<ArgumentException>(() =>
            worker.LinkUser(Guid.Empty));
    }
}
