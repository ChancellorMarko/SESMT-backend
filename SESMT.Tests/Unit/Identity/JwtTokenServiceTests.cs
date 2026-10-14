using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Domain.Modules.IdentityAccess.Constants;
using SESMT.Infrastructure.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SESMT.Tests.Unit.Identity;

public class JwtTokenServiceTests
{
    private const string SigningKey =
        "test-signing-key-with-at-least-32-bytes";

    private static JwtSettings CreateSettings() => new()
    {
        Issuer = "SESMT.API",
        Audience = "SESMT.Client",
        SigningKey = SigningKey,
        AccessTokenExpirationMinutes = 15
    };

    private static JwtTokenService CreateService() =>
        new(Options.Create(CreateSettings()));

    [Fact]
    public void GenerateAccessToken_ShouldGenerateValidJwt()
    {
        var userId = Guid.NewGuid();
        var role = "SESMT_ADMIN";
        var scopeId = Guid.NewGuid();

        var scopes = new[]
        {
        new UserScopeClaim(
            ScopeType.UNIDADE,
            scopeId)
    };

        var service = CreateService();

        var tokenResult = service.GenerateAccessToken(
            userId,
            [role],
            scopes);

        Assert.False(string.IsNullOrWhiteSpace(tokenResult.Token));
        Assert.True(tokenResult.ExpiresAtUtc > DateTime.UtcNow);

        var handler = new JwtSecurityTokenHandler();

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "SESMT.API",
            ValidateAudience = true,
            ValidAudience = "SESMT.Client",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var principal = handler.ValidateToken(
            tokenResult.Token,
            validationParameters,
            out var validatedToken);

        Assert.IsType<JwtSecurityToken>(validatedToken);

        Assert.Equal(
            userId.ToString(),
            principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        Assert.Equal("SESMT.API", validatedToken.Issuer);

        Assert.Contains(
            "SESMT.Client",
            ((JwtSecurityToken)validatedToken).Audiences);

        Assert.Contains(
            principal.Claims,
            claim => claim.Type == ClaimTypes.Role
                && claim.Value == role);

        Assert.Contains(
            principal.Claims,
            claim => claim.Type == "scope_type"
                && claim.Value == ScopeType.UNIDADE);

        Assert.Contains(
            principal.Claims,
            claim => claim.Type == "scope_reference_id"
                && claim.Value == scopeId.ToString());

        Assert.Equal(
            DateTime.SpecifyKind(
                ((JwtSecurityToken)validatedToken).ValidTo,
                DateTimeKind.Utc),
            tokenResult.ExpiresAtUtc);
    }

    [Fact]
    public void Constructor_ShouldRejectSigningKeyShorterThan32Bytes()
    {
        var settings = new JwtSettings
        {
            Issuer = "SESMT.API",
            Audience = "SESMT.Client",
            SigningKey = "short",
            AccessTokenExpirationMinutes = 15
        };

        Assert.Throws<InvalidOperationException>(() =>
            new JwtTokenService(Options.Create(settings)));
    }

    [Fact]
    public void Constructor_ShouldRejectNonPositiveExpiration()
    {
        var settings = new JwtSettings
        {
            Issuer = "SESMT.API",
            Audience = "SESMT.Client",
            SigningKey = SigningKey,
            AccessTokenExpirationMinutes = 0
        };

        Assert.Throws<InvalidOperationException>(() =>
            new JwtTokenService(Options.Create(settings)));
    }
}