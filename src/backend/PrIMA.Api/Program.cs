using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;
using PrIMA.Api.Features.Auth;
using PrIMA.Api.Features.Ingestion;
using PrIMA.Api.Features.Issues.GetDashboardFeed;
using PrIMA.Api.Infrastructure.Persistence;
using PrIMA.Api.Migrations;
using PrIMA.Api.Seeding;

var builder = WebApplication.CreateBuilder(args);

// Configuration values
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"] 
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

// Service Registrations
builder.Services.AddProblemDetails();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PrIMA API",
        Version = "v1",
        Description = "Proactive Inspection Monitoring App (PrIMA) — Operational Telemetry & Exception Workbench API"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer {token}'.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-Detector-API-Key",
        Description = "Detector Ingestion API Key for telemetry submission.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IDetectorRepository, DetectorRepository>();
builder.Services.AddScoped<IIssueRepository, IssueRepository>();
builder.Services.AddScoped<ApiKeyEndpointFilter>();
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<IngestTelemetryHandler>();
builder.Services.AddScoped<GetDashboardFeedHandler>();
builder.Services.AddScoped<DevelopmentDemoDataSeeder>();

var app = builder.Build();

// Middleware Pipeline
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PrIMA API v1");
    });
}

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

// Base Health Check Endpoints
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestampUtc = DateTime.UtcNow }))
   .AllowAnonymous()
   .WithName("GetHealth")
   .WithTags("System");

app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy", timestampUtc = DateTime.UtcNow }))
   .AllowAnonymous()
   .WithName("GetApiHealth")
   .WithTags("System");

// Feature Endpoints
app.MapAuthEndpoints();
app.MapIngestTelemetryEndpoint();
app.MapGetDashboardFeedEndpoint();

// Execute Database Migrations
var connectionString = app.Configuration.GetConnectionString("PrIMADb")
    ?? throw new InvalidOperationException("ConnectionStrings:PrIMADb is not configured.");

MigrationRunner.Run(connectionString);

// Seed Development Demo Data
if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("SeedDemoData"))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentDemoDataSeeder>();
    await seeder.SeedAsync();
}

app.Run();

public partial class Program { }
