using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PrIMA.Detectors.Probes;
using PrIMA.Detectors.Sdk;

namespace PrIMA.Detectors.Services;

/// <summary>
/// Dependency injection registration helpers for the PrIMA Detector SDK.
/// </summary>
public static class DetectorSdkExtensions
{
    /// <summary>
    /// Registers the PrIMA Detector SDK core services, HTTP clients, handshake service, CLI runner, and daemon worker.
    /// </summary>
    public static IServiceCollection AddDetectorSdk(
        this IServiceCollection services,
        Action<DetectorSdkOptions>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        services.AddHttpClient<TelemetryIngestionClient>((sp, client) =>
        {
            var options = sp.GetService<IOptions<DetectorSdkOptions>>()?.Value;
            if (!string.IsNullOrWhiteSpace(options?.ServerUrl))
            {
                client.BaseAddress = new Uri(options.ServerUrl.TrimEnd('/') + "/");
            }
        });

        services.AddHttpClient<IBootstrapHandshakeService, BootstrapHandshakeService>((sp, client) =>
        {
            var options = sp.GetService<IOptions<DetectorSdkOptions>>()?.Value;
            if (!string.IsNullOrWhiteSpace(options?.ServerUrl))
            {
                client.BaseAddress = new Uri(options.ServerUrl.TrimEnd('/') + "/");
            }
        });

        services.TryAddSingleton<IReadOnlySimrsConnectionFactory, ReadOnlySimrsConnectionFactory>();
        services.AddSingleton<IProbeExecutor, ProbeExecutor>();
        services.AddScoped<IDetectorRunner, DetectorRunner>();
        services.AddScoped<IDetectorCliRunner, DetectorCliRunner>();
        services.AddHostedService<DetectorDaemonWorker>();

        return services;
    }

    /// <summary>
    /// Registers the pilot SIMRS financial and process integrity probes (DEC-007).
    /// </summary>
    public static IServiceCollection AddPilotFinancialProbes(this IServiceCollection services)
    {
        services.AddDetectorProbe<JournalBalanceDetector>();
        services.AddDetectorProbe<DuplicateReceivableDetector>();
        services.AddDetectorProbe<UnprocessedBillingPipelineDetector>();
        return services;
    }

    /// <summary>
    /// Registers the pilot SIMRS sync and infrastructure detector probes (EMR, HFIS, Print Queue) (DEC-007).
    /// </summary>
    public static IServiceCollection AddPilotSyncAndInfraProbes(this IServiceCollection services)
    {
        services.AddDetectorProbe<EmrSyncDetector>();
        services.AddDetectorProbe<HfisScheduleSyncDetector>();
        services.AddDetectorProbe<PrintQueueSpoolerDetector>();
        return services;
    }

    /// <summary>
    /// Registers all 6 pilot SIMRS detector probes (DEC-007).
    /// </summary>
    public static IServiceCollection AddAllPilotSimrsProbes(this IServiceCollection services)
    {
        services.AddPilotFinancialProbes();
        services.AddPilotSyncAndInfraProbes();
        return services;
    }

    /// <summary>
    /// Registers a detector probe implementation with the dependency injection container.
    /// </summary>
    public static IServiceCollection AddDetectorProbe<TProbe>(this IServiceCollection services)
        where TProbe : class, IDetectorProbe
    {
        services.AddSingleton<IDetectorProbe, TProbe>();
        return services;
    }
}
