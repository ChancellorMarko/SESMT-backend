using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Domain.Modules.IdentityAccess.Constants;


namespace SESMT.Infrastructure.Identity;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly SigningCredentials _signingCredentials;

    public JwtTokenService(IOptions<JwtSettings> options)
    {
        _settings = options.Value;

        if (string.IsNullOrWhiteSpace(_settings.SigningKey)
            || Encoding.UTF8.GetByteCount(_settings.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "A chave de assinatura JWT deve possuir pelo menos 32 bytes.");
        }

        if (string.IsNullOrWhiteSpace(_settings.Issuer)
            || string.IsNullOrWhiteSpace(_settings.Audience))
        {
            throw new InvalidOperationException(
                "O emissor e a audiência JWT devem ser configurados.");
        }

        if (_settings.AccessTokenExpirationMinutes <= 0)
        {
            throw new InvalidOperationException(
                "A duração do access token deve ser maior que zero.");
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_settings.SigningKey));

        _signingCredentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);
    }

    public AccessTokenResult GenerateAccessToken(
        Guid userId,
        IEnumerable<string> roles,
        IEnumerable<UserScopeClaim> scopes)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "O ID do usuário é obrigatório.",
                nameof(userId));
        }

        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(scopes);

        var now = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(
                JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        claims.AddRange(
            roles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.Ordinal)
                .Select(role => new Claim(ClaimTypes.Role, role)));

        foreach (var scope in scopes)
        {
            if (scope is null)
            {
                throw new ArgumentException(
                    "A coleção de escopos não pode conter valores nulos.",
                    nameof(scopes));
            }

            if (!IsValidScopeType(scope.ScopeType))
            {
                throw new ArgumentException(
                    $"Tipo de escopo inválido: {scope.ScopeType}.",
                    nameof(scopes));
            }

            claims.Add(new Claim("scope_type", scope.ScopeType));

            if (scope.ReferenceId.HasValue)
            {
                if (scope.ReferenceId.Value == Guid.Empty)
                {
                    throw new ArgumentException(
                        "A referência do escopo não pode ser um GUID vazio.",
                        nameof(scopes));
                }

                claims.Add(new Claim(
                    "scope_reference_id",
                    scope.ReferenceId.Value.ToString()));
            }
        }

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(
                _settings.AccessTokenExpirationMinutes),
            signingCredentials: _signingCredentials);

        var serializedToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new AccessTokenResult(
            serializedToken,
            token.ValidTo);
    }

    private static bool IsValidScopeType(string scopeType) =>
        scopeType is
            ScopeType.EMPRESA
            or ScopeType.UNIDADE
            or ScopeType.SETOR;
}