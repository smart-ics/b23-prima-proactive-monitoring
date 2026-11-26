using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrIMA.Detectors.Sdk;

namespace PrIMA.Detectors.Services;

/// <summary>
/// Service contract for handling detector bootstrap, local credential caching, and automated token handshake.
/// </summary>
public interface IBootstrapHandshakeService
{
    /// <summary>
    /// Resolves credentials for a detector probe from local cache (.prima-credentials.json)
    /// or executes automated handshake enrollment with PrIMA server via PRIMA_ENROLLMENT_TOKEN.
    /// </summary>
    Task<DetectorCredential?> EnsureCredentialsAsync(IDetectorProbe probe, CancellationToken ct = default);

    /// <summary>
    /// Resolves credentials for a collection of detector probes.
    /// </summary>
    Task<IReadOnlyDictionary<string, DetectorCredential>> EnsureAllCredentialsAsync(
        IEnumerable<IDetectorProbe> probes,
        CancellationToken ct = default);
}

/// <summary>
/// Implements detector bootstrap handshake and local credential caching (.prima-credentials.json) (DEC-005).
/// </summary>
public sealed class BootstrapHandshakeService : IBootstrapHandshakeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly HttpClient _httpClient;
    private readonly DetectorSdkOptions _options;
    private readonly ILogger<BootstrapHandshakeService> _logger;
    private readonly object _fileLock = new();

    public BootstrapHandshakeService(
        HttpClient httpClient,
        IOptions<DetectorSdkOptions>? options = null,
        ILogger<BootstrapHandshakeService>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? new DetectorSdkOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BootstrapHandshakeService>.Instance;
    }

    public async Task<DetectorCredential?> EnsureCredentialsAsync(IDetectorProbe probe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(probe);

        var credentialPath = ResolveCredentialFilePath();

        // 1. Check local credential cache file (.prima-credentials.json)
        var cachedFile = LoadCredentialsFile(credentialPath);
        if (cachedFile != null)
        {
            // Match in multi-probe map
            if (cachedFile.Probes.TryGetValue(probe.Manifest.Name, out var matchedCred) &&
                matchedCred.DetectorId != Guid.Empty &&
                !string.IsNullOrWhiteSpace(matchedCred.ApiKey))
            {
                _logger.LogDebug("Loaded credentials for detector '{ProbeName}' ({DetectorId}) from '{Path}'.",
                    probe.Manifest.Name, matchedCred.DetectorId, credentialPath);
                return matchedCred;
            }

            // Match in top-level single detector cache
            if (cachedFile.DetectorId.HasValue &&
                cachedFile.DetectorId.Value != Guid.Empty &&
                !string.IsNullOrWhiteSpace(cachedFile.ApiKey) &&
                (string.IsNullOrWhiteSpace(cachedFile.DetectorName) ||
                 string.Equals(cachedFile.DetectorName, probe.Manifest.Name, StringComparison.OrdinalIgnoreCase)))
            {
                var cred = new DetectorCredential
                {
                    DetectorId = cachedFile.DetectorId.Value,
                    ApiKey = cachedFile.ApiKey,
                    ServerUrl = cachedFile.ServerUrl ?? ResolveServerUrl(),
                    DetectorName = probe.Manifest.Name
                };
                return cred;
            }
        }

        // 2. Local credentials missing; check PRIMA_ENROLLMENT_TOKEN
        var enrollmentToken = _options.EnrollmentToken;
        if (string.IsNullOrWhiteSpace(enrollmentToken))
        {
            enrollmentToken = Environment.GetEnvironmentVariable("PRIMA_ENROLLMENT_TOKEN");
        }

        if (string.IsNullOrWhiteSpace(enrollmentToken))
        {
            _logger.LogError(
                "PrIMA credentials missing from '{CredentialPath}' and environment variable PRIMA_ENROLLMENT_TOKEN is not set. " +
                "To enroll this detector, generate an enrollment token in the PrIMA web UI (Quick Enroll) and configure PRIMA_ENROLLMENT_TOKEN, " +
                "or place a valid .prima-credentials.json file in the working directory.",
                credentialPath);
            return null;
        }

        // 3. Token present; execute automated handshake with PrIMA server
        var serverUrl = ResolveServerUrl();
        var webhookUrl = ResolveWebhookUrl(probe.Manifest);

        _logger.LogInformation("Enrolling detector '{ProbeName}' via handshake with PrIMA server at {ServerUrl}...",
            probe.Manifest.Name, serverUrl);

        var handshakeRequest = new DetectorHandshakeApiRequest(
            EnrollmentToken: enrollmentToken.Trim(),
            Name: probe.Manifest.Name,
            TargetModule: probe.Manifest.TargetModule,
            Scope: probe.Manifest.Scope,
            ExpectedCadenceSeconds: probe.Manifest.ExpectedCadenceSeconds,
            WebhookUrl: webhookUrl,
            Description: probe.Manifest.Description,
            Version: probe.Manifest.Version);

        var requestUri = $"{serverUrl.TrimEnd('/')}/api/detectors/handshake";
        using var response = await _httpClient.PostAsJsonAsync(requestUri, handshakeRequest, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Handshake registration failed for '{ProbeName}' with status HTTP {StatusCode}: {ErrorBody}",
                probe.Manifest.Name, (int)response.StatusCode, errorBody);
            throw new InvalidOperationException(
                $"Handshake registration failed for '{probe.Manifest.Name}' (HTTP {(int)response.StatusCode}): {errorBody}");
        }

        var handshakeResult = await response.Content.ReadFromJsonAsync<DetectorHandshakeApiResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException($"Invalid handshake response received from {requestUri}.");

        var newCredential = new DetectorCredential
        {
            DetectorId = handshakeResult.DetectorId,
            ApiKey = handshakeResult.ApiKey,
            ServerUrl = serverUrl,
            DetectorName = handshakeResult.Name
        };

        // 4. Persist to local cache (.prima-credentials.json)
        SaveCredentialsFile(credentialPath, probe.Manifest.Name, newCredential);

        _logger.LogInformation("Detector '{ProbeName}' successfully enrolled with ID {DetectorId}. Credentials saved to '{Path}'.",
            probe.Manifest.Name, newCredential.DetectorId, credentialPath);

        return newCredential;
    }

    public async Task<IReadOnlyDictionary<string, DetectorCredential>> EnsureAllCredentialsAsync(
        IEnumerable<IDetectorProbe> probes,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(probes);

        var results = new Dictionary<string, DetectorCredential>(StringComparer.OrdinalIgnoreCase);

        foreach (var probe in probes)
        {
            var cred = await EnsureCredentialsAsync(probe, ct);
            if (cred != null)
            {
                results[probe.Manifest.Name] = cred;
            }
        }

        return results;
    }

    private string ResolveCredentialFilePath()
    {
        var rawPath = !string.IsNullOrWhiteSpace(_options.CredentialFilePath)
            ? _options.CredentialFilePath
            : ".prima-credentials.json";

        return Path.IsPathRooted(rawPath)
            ? rawPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, rawPath));
    }

    private string ResolveServerUrl()
    {
        var envServer = Environment.GetEnvironmentVariable("PRIMA_SERVER_URL");
        if (!string.IsNullOrWhiteSpace(envServer))
        {
            return envServer.TrimEnd('/');
        }

        return !string.IsNullOrWhiteSpace(_options.ServerUrl)
            ? _options.ServerUrl.TrimEnd('/')
            : "http://localhost:5000";
    }

    private string ResolveWebhookUrl(DetectorManifest manifest)
    {
        var baseUrl = _options.WebhookBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = Environment.GetEnvironmentVariable("PRIMA_WEBHOOK_BASE_URL");
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            var port = _options.WebhookPort > 0 ? _options.WebhookPort : 5090;
            baseUrl = $"http://localhost:{port}";
        }

        var endpointKey = !string.IsNullOrWhiteSpace(manifest.WebhookEndpointKey)
            ? manifest.WebhookEndpointKey
            : manifest.Name.ToLowerInvariant().Replace(" ", "-");

        return $"{baseUrl.TrimEnd('/')}/webhooks/{endpointKey}";
    }

    private DetectorCredentialsFile? LoadCredentialsFile(string path)
    {
        lock (_fileLock)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<DetectorCredentialsFile>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read credentials cache file at '{Path}'.", path);
                return null;
            }
        }
    }

    private void SaveCredentialsFile(string path, string probeName, DetectorCredential credential)
    {
        lock (_fileLock)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var existing = LoadCredentialsFile(path) ?? new DetectorCredentialsFile();

                // Maintain top-level fields for single detector access
                existing.DetectorId = credential.DetectorId;
                existing.ApiKey = credential.ApiKey;
                existing.ServerUrl = credential.ServerUrl;
                existing.DetectorName = credential.DetectorName;

                // Maintain multi-detector map
                existing.Probes[probeName] = credential;

                var json = JsonSerializer.Serialize(existing, JsonOptions);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save credentials cache file at '{Path}'.", path);
                throw;
            }
        }
    }
}
