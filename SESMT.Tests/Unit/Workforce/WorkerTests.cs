using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Tests.Unit.Workforce;

public class WorkerTests
{
    private static Worker CreateWorker()
    {
        return new Worker(
            Guid.NewGuid(),
            "MAT-001",
            [1, 2, 3],
            "cpf-hash",
            "João da Silva",
            new DateOnly(2000, 5, 10),
            new DateOnly(2025, 1, 6),
            null,
            null,
            null,
            null);
    }

    [Fact]
    public void Constructor_ShouldCreateWorker_WhenDataIsValid()
    {
        var companyId = Guid.NewGuid();

        var worker = new Worker(
            companyId,
            " MAT-001 ",
            [1, 2, 3],
            " cpf-hash ",
            " João da Silva ",
            new DateOnly(2000, 5, 10),
            new DateOnly(2025, 1, 6),
            null,
            null,
            null,
            null);

        Assert.Equal(companyId, worker.CompanyId);
        Assert.Equal("MAT-001", worker.Enrollment);
        Assert.Equal("cpf-hash", worker.CpfHash);
        Assert.Equal("João da Silva", worker.Name);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCompanyIdIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            new Worker(
                Guid.Empty,
                "MAT-001",
                [1, 2, 3],
                "cpf-hash",
                "João da Silva",
                new DateOnly(2000, 5, 10),
                new DateOnly(2025, 1, 6),
                null,
                null,
                null,
                null));
    }

    [Fact]
    public void Update_ShouldUpdateWorkerData_WhenDataIsValid()
    {
        var worker = CreateWorker();

        worker.Update(
            "MAT-002",
            "Maria da Silva",
            new DateOnly(1998, 8, 15),
            new DateOnly(2024, 3, 1),
            null,
            null,
            null);

        Assert.Equal("MAT-002", worker.Enrollment);
        Assert.Equal("Maria da Silva", worker.Name);
        Assert.Equal(new DateOnly(1998, 8, 15), worker.DateOfBirth);
        Assert.Equal(new DateOnly(2024, 3, 1), worker.AdmissionDate);
    }

    [Fact]
    public void Update_ShouldThrow_WhenNameIsEmpty()
    {
        var worker = CreateWorker();

        Assert.Throws<ArgumentException>(() =>
            worker.Update(
                "MAT-002",
                " ",
                new DateOnly(2000, 5, 10),
                new DateOnly(2025, 1, 6),
                null,
                null,
                null));
    }
}