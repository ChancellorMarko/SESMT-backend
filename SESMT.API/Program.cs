using System.Text;
using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using SESMT.API.Middleware;
using SESMT.API.Services;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using PasswordResetEmailSettings = SESMT.API.Configuration.PasswordResetEmailSettings;
using SESMT.Infrastructure.Persistence;
using SESMT.Infrastructure.Identity;
using SESMT.Application;
using SESMT.Infrastructure;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Infrastructure.Persistence.Seeding;
using SESMT.Application.Common.Interfaces;

const string BearerSecurityScheme = "Bearer";

var builder = WebApplication.CreateBuilder(args);

// Banco de dados
var connectionString =
    builder.Configuration.GetConnectionString("Sesmt");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "A connection string 'Sesmt' não foi configurada.");
}

// Audit
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Configuração JWT
var jwtSettings = builder.Configuration
    .GetSection(JwtSettings.SectionName)
    .Get<JwtSettings>()
    ?? throw new InvalidOperationException(
        "A configuração JWT não foi encontrada.");

var signingKey = builder.Configuration["Jwt:SigningKey"];

if (string.IsNullOrWhiteSpace(signingKey)
    || Encoding.UTF8.GetByteCount(signingKey) < 32)
{
    throw new InvalidOperationException(
        "A chave JWT deve possuir pelo menos 32 bytes.");
}

if (string.IsNullOrWhiteSpace(jwtSettings.Issuer)
    || string.IsNullOrWhiteSpace(jwtSettings.Audience))
{
    throw new InvalidOperationException(
        "O emissor e a audiência JWT devem ser configurados.");
}

if (jwtSettings.RefreshTokenExpirationDays <= 0)
{
    throw new InvalidOperationException(
        "A duração do refresh token deve ser maior que zero.");
}

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(JwtSettings.SectionName));

builder.Services.AddSingleton(
    new SessionPolicy(jwtSettings.RefreshTokenExpirationDays));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,

                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(signingKey)),

                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ClockSkew = TimeSpan.Zero
            };
    });

builder.Services.AddAuthorization();

// Configuração do Seeder
builder.Services.Configure<SeedSettings>(builder.Configuration.GetSection(SeedSettings.SectionName));

// API e documentação
builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer(
        (document, context, cancellationToken) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??=
                new Dictionary<string, IOpenApiSecurityScheme>();

            document.Components.SecuritySchemes[BearerSecurityScheme] =
                new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description =
                        "Informe o access token JWT retornado pelo login."
                };

            return Task.CompletedTask;
        });

    options.AddOperationTransformer(
        (operation, context, cancellationToken) =>
        {
            var endpointMetadata = context
                .Description
                .ActionDescriptor
                .EndpointMetadata;

            var hasAuthorize = endpointMetadata
                .OfType<IAuthorizeData>()
                .Any();

            var hasAllowAnonymous = endpointMetadata
                .OfType<IAllowAnonymous>()
                .Any();

            if (!hasAuthorize || hasAllowAnonymous)
                return Task.CompletedTask;

            operation.Security ??= [];
            operation.Security.Add(
                new OpenApiSecurityRequirement
                {
                    [
                        new OpenApiSecuritySchemeReference(
                            BearerSecurityScheme,
                            context.Document,
                            null)
                    ] = []
                });

            return Task.CompletedTask;
        });
});

builder.Services.AddApplication();
builder.Services.AddScoped<SESMT.Application.Modules.WorkPermits.Services.OperationalDesignationService>();
builder.Services.AddInfrastructure(builder.Configuration);
var passwordResetEmailOptions = builder.Services.AddOptions<PasswordResetEmailSettings>()
    .Bind(builder.Configuration.GetSection(PasswordResetEmailSettings.SectionName));
if (builder.Environment.IsProduction())
{
    passwordResetEmailOptions
        .Validate(settings =>
            !string.IsNullOrWhiteSpace(settings.Host)
            && settings.Port is > 0 and <= 65535
            && settings.EnableSsl
            && MailAddress.TryCreate(settings.From, out _)
            && !string.IsNullOrWhiteSpace(settings.Username)
            && !string.IsNullOrWhiteSpace(settings.Password)
            && Uri.TryCreate(settings.ResetUrl, UriKind.Absolute, out var resetUri)
            && resetUri.Scheme == Uri.UriSchemeHttps,
            "A configuração SMTP de produção deve informar host, porta válida, TLS, remetente, credenciais e URL HTTPS de redefinição.")
        .ValidateOnStart();
}
builder.Services.AddScoped<IPasswordResetNotificationService, SmtpPasswordResetNotificationService>();

var app = builder.Build();

// Scalar e OpenAPI somente em desenvolvimento
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .AddPreferredSecuritySchemes([BearerSecurityScheme])
            .EnablePersistentAuthentication();
    });
}

using (var scope = app.Services.CreateScope())
{
    try
    {
        await DatabaseSeeder.SeedAsync(app.Services);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup");
        logger.LogError(ex, "Falha ao rodar o seed inicial.");
        throw;
    }
}

// Pipeline HTTP
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Health check
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy"
}));

app.MapControllers();

app.Run();

public partial class Program { }
