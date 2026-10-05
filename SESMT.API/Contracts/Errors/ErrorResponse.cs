namespace SESMT.API.Contracts.Errors;

public sealed record ErrorResponse(
    int Status,
    string Title,
    string Detail
);