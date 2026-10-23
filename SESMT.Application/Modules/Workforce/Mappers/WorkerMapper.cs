using SESMT.Application.Modules.Workforce.DTOs;
using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Application.Modules.Workforce.Mappers;

public static class WorkerMapper
{
    public static Worker ToEntity(
        WorkerCreateRequest request,
        byte[] cpfEncrypted,
        string cpfHash) =>
        new(
            companyId: request.CompanyId,
            enrollment: request.Enrollment,
            cpfEncryption: cpfEncrypted,
            cpfHash: cpfHash,
            name: request.Name,
            dateOfBirth: request.DateOfBirth,
            admissionDate: request.AdmissionDate,
            dismissalDate: request.DismissalDate,
            positionId: request.PositionId,
            unitId: request.UnitId,
            departmentId: request.DepartmentId,
            userId: request.UserId);

    public static WorkerResponse ToResponse(Worker worker, string cpfMasked) =>
        new()
        {
            Id = worker.Id,
            CompanyId = worker.CompanyId,
            Enrollment = worker.Enrollment,
            CpfMasked = cpfMasked,
            Name = worker.Name.Value,
            DateOfBirth = worker.DateOfBirth,
            AdmissionDate = worker.AdmissionDate,
            DismissalDate = worker.DismissalDate,
            PositionId = worker.PositionId,
            UnitId = worker.UnitId,
            DepartmentId = worker.DepartmentId,
            UserId = worker.UserId,
            Active = worker.Active && worker.DeletedAt is null
        };

    public static WorkerSummaryResponse ToSummary(Worker worker) =>
        new()
        {
            Id = worker.Id,
            Enrollment = worker.Enrollment,
            Name = worker.Name.Value,
            PositionId = worker.PositionId,
            DepartmentId = worker.DepartmentId,
            Active = worker.Active && worker.DeletedAt is null
        };
}
