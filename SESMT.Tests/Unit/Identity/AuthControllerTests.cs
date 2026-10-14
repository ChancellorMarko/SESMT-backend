using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Controllers;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;

namespace SESMT.Tests.Unit.Identity;

public sealed class AuthControllerTests
{
    [Fact]
    public async Task ForgotPassword_ReturnsSameAcceptedResponseForEveryEmail()
    {
        var controller = new AuthController(
            null!,
            null!,
            null!,
            new FakePasswordResetService());

        var knownEmail = await controller.ForgotPassword(
            new ForgotPasswordRequest("user@example.com"), CancellationToken.None);
        var unknownEmail = await controller.ForgotPassword(
            new ForgotPasswordRequest("unknown@example.com"), CancellationToken.None);

        var knownResponse = Assert.IsType<AcceptedResult>(knownEmail);
        var unknownResponse = Assert.IsType<AcceptedResult>(unknownEmail);
        Assert.Equal(StatusCodes.Status202Accepted, knownResponse.StatusCode);
        Assert.Equal(StatusCodes.Status202Accepted, unknownResponse.StatusCode);
        Assert.Equal(
            JsonSerializer.Serialize(knownResponse.Value),
            JsonSerializer.Serialize(unknownResponse.Value));
    }

    private sealed class FakePasswordResetService : IPasswordResetService
    {
        public Task RequestAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<PasswordResetResult> ResetAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(PasswordResetResult.Failure("invalid_or_expired_token"));
    }
}
