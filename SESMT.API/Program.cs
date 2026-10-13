using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SESMT.API.Middleware;
using SESMT.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("Sesmt");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "A connection string 'Sesmt' não foi configurada.");
}

builder.Services.AddDbContext<SesmtDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        npgsql => npgsql.MigrationsAssembly(
            typeof(SesmtDbContext).Assembly.FullName)));

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy"
}));

app.MapControllers();

app.Run();