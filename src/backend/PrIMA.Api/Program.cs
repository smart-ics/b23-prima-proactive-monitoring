using System.Data.Common;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PrIMA.Api.BackgroundServices;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;
using PrIMA.Api.Features.Auth;
using PrIMA.Api.Features.Detectors;
using PrIMA.Api.Features.Detectors.Enrollment;
using PrIMA.Api.Features.Detectors.Enrollment.GenerateEnrollmentToken;
using PrIMA.Api.Features.Detectors.Enrollment.GetPendingEnrollmentTokens;
using PrIMA.Api.Features.Detectors.Enrollment.RevokeEnrollmentToken;
using PrIMA.Api.Features.Detectors.Enrollment.DetectorHandshake;
using PrIMA.Api.Features.Detectors.GetDetectorById;
using PrIMA.Api.Features.Detectors.GetDetectorList;
using PrIMA.Api.Features.Detectors.GetDetectorSummary;
using PrIMA.Api.Features.Detectors.GetInspectionRuns;
using PrIMA.Api.Features.Detectors.RegisterDetector;
using PrIMA.Api.Features.Detectors.ToggleDetectorStatus;
using PrIMA.Api.Features.Detectors.TriggerManualRun;
using PrIMA.Api.Features.Detectors.UpdateDetector;
using PrIMA.Api.Features.History;
using PrIMA.Api.Features.History.GetHistoricalIssues;
using PrIMA.Api.Features.History.GetReliabilityKpi;
using PrIMA.Api.Features.Ingestion;
using PrIMA.Api.Features.Issues;
using PrIMA.Api.Features.Issues.AcknowledgeIssue;
using PrIMA.Api.Features.Issues.AddNote;
using PrIMA.Api.Features.Issues.AssignIssue;
using PrIMA.Api.Features.Issues.DismissIssue;
using PrIMA.Api.Features.Issues.GetDashboardFeed;
using PrIMA.Api.Features.Issues.GetIssueById;
using PrIMA.Api.Features.Issues.RecordManualVerification;
using PrIMA.Api.Features.Issues.RecordRemediation;
using PrIMA.Api.Features.Issues.ResolveIssue;
using PrIMA.Api.Features.Issues.StartInvestigation;
using PrIMA.Api.Features.Issues.TakeIt;
using PrIMA.Api.Features.Issues.TriggerVerificationRun;
using PrIMA.Api.Features.Officers;
using PrIMA.Api.Infrastructure.Persistence;
using PrIMA.Api.Infrastructure.Webhooks;
using PrIMA.Api.Migrations;
using PrIMA.Api.Seeding;

var builder = WebApplication.CreateBuilder(args);

// Configuration values
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"] 
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

// Service Registrations
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BadHttpRequestExceptionHandler>();

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
    options.SupportNonNullableReferenceTypes();
    options.SchemaFilter<RequireNonNullablePropertiesSchemaFilter>();

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
builder.Services.AddHttpClient<IDetectorWebhookClient, DetectorWebhookClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PrIMA-Supervisor/1.0");
});
builder.Services.AddScoped<ApiKeyEndpointFilter>();
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<IngestTelemetryHandler>();
builder.Services.AddScoped<GetDashboardFeedHandler>();
builder.Services.AddScoped<GetOfficersHandler>();
builder.Services.AddScoped<GetIssueByIdHandler>();
builder.Services.AddScoped<TakeItHandler>();
builder.Services.AddScoped<AssignIssueHandler>();
builder.Services.AddScoped<AcknowledgeIssueHandler>();
builder.Services.AddScoped<StartInvestigationHandler>();
builder.Services.AddScoped<AddNoteHandler>();
builder.Services.AddScoped<RecordRemediationHandler>();
builder.Services.AddScoped<RecordManualVerificationHandler>();
builder.Services.AddScoped<ResolveIssueHandler>();
builder.Services.AddScoped<DismissIssueHandler>();
builder.Services.AddScoped<GetDetectorListHandler>();
builder.Services.AddScoped<GetDetectorSummaryHandler>();
builder.Services.AddScoped<GetDetectorByIdHandler>();
builder.Services.AddScoped<GetInspectionRunsHandler>();
builder.Services.AddScoped<RegisterDetectorHandler>();
builder.Services.AddScoped<UpdateDetectorHandler>();
builder.Services.AddScoped<ToggleDetectorStatusHandler>();
builder.Services.AddScoped<TriggerManualRunHandler>();
builder.Services.AddScoped<TriggerVerificationRunHandler>();
builder.Services.AddScoped<GenerateEnrollmentTokenHandler>();
builder.Services.AddScoped<GetPendingEnrollmentTokensHandler>();
builder.Services.AddScoped<RevokeEnrollmentTokenHandler>();
builder.Services.AddScoped<DetectorHandshakeHandler>();
builder.Services.AddScoped<GetReliabilityKpiHandler>();
builder.Services.AddScoped<GetHistoricalIssuesHandler>();
builder.Services.AddScoped<DevelopmentDemoDataSeeder>();
builder.Services.AddHostedService<MonitoringSupervisorWorker>();

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
app.MapGet("/health", (ISqlConnectionFactory sqlConnectionFactory, CancellationToken cancellationToken) =>
    ExecuteHealthCheckAsync(sqlConnectionFactory, cancellationToken))
   .AllowAnonymous()
   .WithName("GetHealth")
   .WithTags("System");

app.MapGet("/api/health", (ISqlConnectionFactory sqlConnectionFactory, CancellationToken cancellationToken) =>
    ExecuteHealthCheckAsync(sqlConnectionFactory, cancellationToken))
   .AllowAnonymous()
   .WithName("GetApiHealth")
   .WithTags("System");

// Feature Endpoints
app.MapAuthEndpoints();
app.MapIngestTelemetryEndpoint();
app.MapGetDashboardFeedEndpoint();
app.MapOfficerEndpoints();
app.MapIssueLifecycleEndpoints();
app.MapDetectorEndpoints();
app.MapEnrollmentEndpoints();
app.MapHistoryEndpoints();

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

static async Task<IResult> ExecuteHealthCheckAsync(ISqlConnectionFactory sqlConnectionFactory, CancellationToken cancellationToken)
{
    try
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        if (connection is DbConnection dbConn)
        {
            await dbConn.OpenAsync(cancellationToken);
            using var cmd = dbConn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(cancellationToken);
        }
        else
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.ExecuteScalar();
        }

        return Results.Ok(new
        {
            status = "Healthy",
            database = "Connected",
            timestampUtc = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new
            {
                status = "Unhealthy",
                database = "Disconnected",
                error = ex.Message
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

public partial class Program { }

public sealed class RequireNonNullablePropertiesSchemaFilter : Swashbuckle.AspNetCore.SwaggerGen.ISchemaFilter
{
    public void Apply(OpenApiSchema schema, Swashbuckle.AspNetCore.SwaggerGen.SchemaFilterContext context)
    {
        if (schema.Properties == null) return;

        foreach (var property in schema.Properties)
        {
            if (!property.Value.Nullable && !schema.Required.Contains(property.Key))
            {
                schema.Required.Add(property.Key);
            }
        }
    }
}

public sealed class BadHttpRequestExceptionHandler : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badHttpRequestException)
        {
            return false;
        }

        var problemDetails = badHttpRequestException.ToProblemDetails(httpContext);
        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }
}
