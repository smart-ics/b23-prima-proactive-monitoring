using PrIMA.Detectors.Services;

var builder = WebApplication.CreateBuilder(args);

var isRunOnce = args.Contains("--run-once", StringComparer.OrdinalIgnoreCase);

// Register Detector SDK core components
builder.Services.AddDetectorSdk(options =>
{
    var section = builder.Configuration.GetSection("DetectorSdk");
    if (section.Exists())
    {
        section.Bind(options);
    }
});

// Register Pilot SIMRS Financial & Process Probes (DEC-007)
builder.Services.AddPilotFinancialProbes();

// Configure Kestrel port for Webhook listener if not already configured via ASPNETCORE_URLS
var webhookPort = builder.Configuration.GetValue<int?>("DetectorSdk:WebhookPort")
    ?? (int.TryParse(Environment.GetEnvironmentVariable("PRIMA_WEBHOOK_PORT"), out var port) ? port : 5090);

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{webhookPort}");
}

var app = builder.Build();

// Map Embedded Kestrel Webhook Listener & Status endpoints
app.MapDetectorWebhooks();

app.MapGet("/", () => Results.Ok(new
{
    Service = "PrIMA Reference Detector Runner",
    Status = "Running",
    TimestampUtc = DateTime.UtcNow
}));

app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    TimestampUtc = DateTime.UtcNow
}));

if (isRunOnce)
{
    using var scope = app.Services.CreateScope();
    var cliRunner = scope.ServiceProvider.GetRequiredService<IDetectorCliRunner>();
    var exitCode = await cliRunner.RunOnceAsync();
    Environment.ExitCode = exitCode;
    return exitCode;
}

await app.RunAsync();
return 0;

namespace PrIMA.Detectors
{
    public partial class Program { }
}
