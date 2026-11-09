using System.Text;
using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
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
using SESMT.API.Contracts.Browser;
using Microsoft.AspNetCore.Mvc;

const string BearerSecurityScheme = "Bearer";

var builder = WebApplication.CreateBuilder(args);

// ─── Auditoria (usa HttpContext) ───────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IBrowserIdentityReader, BrowserIdentityReader>();
builder.Services.AddScoped<IBrowserWorkspaceAccessReader, BrowserWorkspaceAccessReader>();
builder.Services.AddScoped<IBrowserWorkersReader, BrowserWorkersReader>();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-Token";
    options.Cookie.Name = "ampara.csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.Cookie.Path = "/api/v1/browser";
});

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
        // UsePathBase removes /api/v1 before authentication. Keep native responses intact.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                if (!context.Request.Path.StartsWithSegments("/browser")) return;
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.CacheControl = "no-store";
                var expired = context.AuthenticateFailure is SecurityTokenExpiredException;
                await context.Response.WriteAsJsonAsync(new BrowserErrorResponse(
                    expired ? "TOKEN_EXPIRED" : "UNAUTHENTICATED",
                    expired ? "Seu acesso expirou. Renove a sessão." : "Entre para continuar.",
                    context.HttpContext.TraceIdentifier));
            },
            OnForbidden = async context =>
            {
                if (!context.Request.Path.StartsWithSegments("/browser")) return;
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.Headers.CacheControl = "no-store";
                await context.Response.WriteAsJsonAsync(new BrowserErrorResponse(
                    "FORBIDDEN", "Você não possui acesso a esta operação.", context.HttpContext.TraceIdentifier));
            }
        };
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
            .WithExposedHeaders("X-Total-Count")
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ─── Seeder ────────────────────────────────────────────────────────────
builder.Services.AddOptions<SeedSettings>()
    .Bind(builder.Configuration.GetSection(SeedSettings.SectionName))
    .Validate(settings => !builder.Environment.IsProduction() || settings.HasSafeProductionBootstrap(),
        "Configure credenciais seguras para o bootstrap de produção ou desative Seed:BootstrapAdminEnabled.")
    .ValidateOnStart();

// ─── API e documentação ────────────────────────────────────────────────
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    var nativeValidation = options.InvalidModelStateResponseFactory;
    options.InvalidModelStateResponseFactory = context =>
        !context.HttpContext.Request.Path.StartsWithSegments("/browser")
            ? nativeValidation(context)
            : new BadRequestObjectResult(new BrowserErrorResponse(
                "VALIDATION_FAILED", "Confira os dados informados.", context.HttpContext.TraceIdentifier));
});
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
builder.Services.AddNativeResourceAuthorization();

// ─── Data Protection (dados protegidos do registro de ASO) ─────────────
// Sem as chaves, os dados protegidos gravados ficam ilegíveis. Fora de desenvolvimento e
// testes, o caminho persistente é obrigatório; veja docs/aso-data-protection-keys.md.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("SESMT.API");
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
else if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
    throw new InvalidOperationException(
        "Configure DataProtection__KeysPath com um diretório persistente e com backup antes de iniciar a API.");

// ─── E-mail ────────────────────────────────────────────────────────────
var passwordResetEmailOptions = builder.Services
    .AddOptions<PasswordResetEmailSettings>()
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

// ─── Build ─────────────────────────────────────────────────────────────
var app = builder.Build();

if (Environment.GetEnvironmentVariable("SESMT_SEED_EXAMPLES") == "1")
{
    LocalExampleSeeder.RequireLocalTarget(app.Environment.EnvironmentName, builder.Configuration.GetConnectionString("Sesmt") ?? "");
    await using var seedScope = app.Services.CreateAsyncScope();
    await DatabaseSeeder.SeedAsync(seedScope.ServiceProvider);
    var examples = await LocalExampleSeeder.SeedAsync(seedScope.ServiceProvider.GetRequiredService<SESMT.Infrastructure.Persistence.SesmtDbContext>(),
        seedScope.ServiceProvider.GetRequiredService<IPasswordHashService>(), seedScope.ServiceProvider.GetRequiredService<ICpfProtectionService>(),
        builder.Configuration["Seed:AdminEmail"] ?? "", Environment.GetEnvironmentVariable("SESMT_EXAMPLE_PASSWORD") ?? "", DateTimeOffset.UtcNow);
    Console.WriteLine("EXAMPLE_SEED_RESULT:" + System.Text.Json.JsonSerializer.Serialize(examples));
    return;
}

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
app.MapFallback("/browser/{**path}", (HttpContext context) => Results.Json(
    new BrowserErrorResponse("INTEGRATION_PENDING", "Este recurso ainda está em integração com a API.", context.TraceIdentifier),
    statusCode: StatusCodes.Status501NotImplemented));

app.Run();

public partial class Program { }
