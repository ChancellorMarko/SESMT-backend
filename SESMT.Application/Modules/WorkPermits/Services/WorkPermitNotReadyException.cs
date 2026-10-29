namespace SESMT.Application.Modules.WorkPermits.Services;

public sealed class WorkPermitNotReadyException : Exception
{
    public IReadOnlyList<Domain.Modules.WorkPermits.Entities.WorkPermitReadinessIssue> Issues { get; }
    public WorkPermitNotReadyException()
        : base("A permissão possui pendências que impedem esta ação.")
    {
        Issues = [];
    }

    public WorkPermitNotReadyException(Domain.Modules.WorkPermits.Entities.WorkPermitReadinessAssessment assessment)
        : base("A permissão possui pendências: " + string.Join(", ", assessment.Issues.Select(issue => issue.Code).Distinct()) + ".")
    {
        Issues = assessment.Issues;
    }
}
