using System.Text;
using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using SESMT.API.Middleware;
using SESMT.API.Services;
using SESMT.Application;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Application.Common.Interfaces;
using SESMT.Infrastructure;
using SESMT.Infrastructure.Identity;
using SESMT.Infrastructure.Persistence.Seeding;
using PasswordResetEmailSettings = SESMT.API.Configuration.PasswordResetEmailSettings;

const string BearerSecurityScheme = "Bearer";

var builder = WebApplication.CreateBuilder(args);

<<<<<<< HEAD
// Banco de dados
var connectionString =
    builder.Configuration.GetConnectionString("Sesmt");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "A connection string 'Sesmt' não foi configurada.");
}

// Audit
=======
// ─── Auditoria (usa HttpContext) ───────────────────────────────────────
>>>>>>> main
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ─── JWT ───────────────────────────────────────────────────────────────
var jwtSettings = builder.Configuration
    .GetSection(JwtSettings.SectionName)
    .Get<JwtSettings>()
    ?? throw new InvalidOperationException("A configuração JWT não foi encontrada.");

var signingKey = builder.Configuration["Jwt:SigningKey"];

if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
    throw new InvalidOperationException("A chave JWT deve possuir pelo menos 32 bytes.");

if (string.IsNullOrWhiteSpace(jwtSettings.Issuer)
    || string.IsNullOrWhiteSpace(jwtSettings.Audience))
    throw new InvalidOperationException("O emissor e a audiência JWT devem ser configurados.");

if (jwtSettings.RefreshTokenExpirationDays <= 0)
    throw new InvalidOperationException("A duração do refresh token deve ser maior que zero.");

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(JwtSettings.SectionName));

builder.Services.AddSingleton(
    new SessionPolicy(jwtSettings.RefreshTokenExpirationDays));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization();

// ─── CORS (⚠️ obrigatório para o front) ────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://localhost:4173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ─── Seeder ────────────────────────────────────────────────────────────
builder.Services.Configure<SeedSettings>(
    builder.Configuration.GetSection(SeedSettings.SectionName));

// ─── API e documentação ────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[BearerSecurityScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Informe o access token JWT retornado pelo login.",
        };
        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var hasAuthorize = metadata.OfType<IAuthorizeData>().Any();
        var hasAllowAnonymous = metadata.OfType<IAllowAnonymous>().Any();

        if (!hasAuthorize || hasAllowAnonymous)
            return Task.CompletedTask;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [
                new OpenApiSecuritySchemeReference(BearerSecurityScheme, context.Document, null)
            ] = []
        });
        return Task.CompletedTask;
    });
});

// ─── DI de Application e Infrastructure ────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
<<<<<<< HEAD
var passwordResetEmailOptions = builder.Services.AddOptions<PasswordResetEmailSettings>()
    .Bind(builder.Configuration.GetSection(PasswordResetEmailSettings.SectionName));
=======

// ─── E-mail ────────────────────────────────────────────────────────────
var passwordResetEmailOptions = builder.Services
    .AddOptions<PasswordResetEmailSettings>()
    .Bind(builder.Configuration.GetSection(PasswordResetEmailSettings.SectionName));

>>>>>>> main
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
<<<<<<< HEAD
=======

>>>>>>> main
builder.Services.AddScoped<IPasswordResetNotificationService, SmtpPasswordResetNotificationService>();

// ─── Build ─────────────────────────────────────────────────────────────
var app = builder.Build();

<<<<<<< HEAD
// Scalar e OpenAPI somente em desenvolvimento
=======
// ─── Health check (antes do UsePathBase, para ficar em /health) ───────
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
   .AllowAnonymous();

// ─── Versionamento ─────────────────────────────────────────────────────
app.UsePathBase("/api/v1");

// ─── Pipeline ──────────────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

// ─── Scalar/OpenAPI (dev) ──────────────────────────────────────────────
>>>>>>> main
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithOpenApiRoutePattern("/api/v1/openapi/{documentName}.json")
            .AddPreferredSecuritySchemes([BearerSecurityScheme])
            .EnablePersistentAuthentication();
    });
}

// ─── Seeder (uma única vez, com try/catch) ─────────────────────────────
await using (var scope = app.Services.CreateAsyncScope())
{
    try
    {
        await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
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

app.MapControllers();

app.Run();

public partial class Program { }
