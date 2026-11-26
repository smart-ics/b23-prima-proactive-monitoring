using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;

namespace PrIMA.Api.Features.Detectors.Enrollment.DetectorHandshake;

public sealed class DetectorHandshakeHandler
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<DetectorHandshakeHandler> _logger;

    private const string SelectTokenWithLockSql = """
        SELECT
            Id,
            TokenPrefix,
            TokenHash,
            Label,
            Status,
            ExpiresAtUtc,
            CreatedByOfficer,
            CreatedAtUtc,
            ConsumedAtUtc,
            ConsumedByDetectorId
        FROM dbo.DetectorEnrollmentTokens WITH (UPDLOCK, ROWLOCK)
        WHERE TokenHash = @TokenHash;
        """;

    private const string SelectDetectorWithLockSql = """
        SELECT
            Id,
            Name,
            Description,
            Scope,
            TargetModule,
            ExpectedCadenceSeconds,
            WebhookUrl,
            AdminStatus,
            HealthStatus,
            LastRunAtUtc,
            LastHealthyHeartbeatUtc,
            CreatedAtUtc,
            ApiKeyHash,
            Version
        FROM dbo.Detectors WITH (UPDLOCK, ROWLOCK)
        WHERE Name = @Name;
        """;

    private const string UpdateExistingDetectorSql = """
        UPDATE dbo.Detectors
        SET Description = @Description,
            Scope = @Scope,
            TargetModule = @TargetModule,
            ExpectedCadenceSeconds = @ExpectedCadenceSeconds,
            WebhookUrl = @WebhookUrl,
            ApiKeyHash = @ApiKeyHash,
            Version = @Version,
            AdminStatus = 'Active'
        WHERE Id = @Id;

        IF OBJECT_ID('dbo.DetectorActivityLogs', 'U') IS NOT NULL
        BEGIN
            INSERT INTO dbo.DetectorActivityLogs (Id, DetectorId, ActivityType, Description, CreatedAtUtc)
            VALUES (NEWID(), @Id, 'ReEnrollment', 'Detector re-enrolled via handshake manifest sync', SYSUTCDATETIME());
        END
        """;

    private const string InsertNewDetectorSql = """
        INSERT INTO dbo.Detectors (
            Id,
            Name,
            Description,
            Scope,
            TargetModule,
            ExpectedCadenceSeconds,
            WebhookUrl,
            AdminStatus,
            HealthStatus,
            ApiKeyHash,
            Version,
            CreatedAtUtc
        )
        VALUES (
            @Id,
            @Name,
            @Description,
            @Scope,
            @TargetModule,
            @ExpectedCadenceSeconds,
            @WebhookUrl,
            'Active',
            'Unknown',
            @ApiKeyHash,
            @Version,
            @CreatedAtUtc
        );
        """;

    private const string ConsumeEnrollmentTokenSql = """
        UPDATE dbo.DetectorEnrollmentTokens
        SET Status = 'Consumed',
            ConsumedAtUtc = SYSUTCDATETIME(),
            ConsumedByDetectorId = @DetectorId
        WHERE Id = @TokenId;
        """;

    public DetectorHandshakeHandler(
        ISqlConnectionFactory connectionFactory,
        ILogger<DetectorHandshakeHandler> logger)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<DetectorHandshakeResponse>> HandleAsync(
        DetectorHandshakeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.EnrollmentToken))
        {
            return Result.Failure<DetectorHandshakeResponse>(EnrollmentTokenErrors.InvalidToken);
        }

        // 1. Cadence bounds and canonical scope validation
        if (request.ExpectedCadenceSeconds < 30 || request.ExpectedCadenceSeconds > 86400)
        {
            return Result.Failure<DetectorHandshakeResponse>(
                DomainError.Validation("Detectors.InvalidCadence", "Expected cadence must be between 30 and 86400 seconds."));
        }

        if (!Enum.TryParse<MonitoringScope>(request.Scope, ignoreCase: true, out var scope) ||
            scope is not (MonitoringScope.DataIntegrity or MonitoringScope.ProcessIntegrity or MonitoringScope.DataSync or MonitoringScope.Infrastructure))
        {
            return Result.Failure<DetectorHandshakeResponse>(
                DomainError.Validation("Detectors.InvalidScope", "Monitoring scope must be one of: DataIntegrity, ProcessIntegrity, DataSync, Infrastructure."));
        }

        var tokenHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(request.EnrollmentToken.Trim()));
        var tokenHash = Convert.ToHexString(tokenHashBytes).ToLowerInvariant();

        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
        {
            if (connection is DbConnection dbConn)
            {
                await dbConn.OpenAsync(cancellationToken);
            }
            else
            {
                connection.Open();
            }
        }

        using var tx = connection.BeginTransaction();

        // 2. Query token with exclusive update lock
        var tokenRecord = await connection.QuerySingleOrDefaultAsync<EnrollmentTokenRecord>(
            new CommandDefinition(
                SelectTokenWithLockSql,
                new { TokenHash = tokenHash },
                tx,
                cancellationToken: cancellationToken));

        if (tokenRecord is null)
        {
            tx.Rollback();
            return Result.Failure<DetectorHandshakeResponse>(EnrollmentTokenErrors.InvalidToken);
        }

        if (!string.Equals(tokenRecord.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            tx.Rollback();
            return Result.Failure<DetectorHandshakeResponse>(
                DomainError.Unauthorized("EnrollmentToken.InvalidToken", "Enrollment token is not pending or has already been used."));
        }

        if (tokenRecord.ExpiresAtUtc <= DateTime.UtcNow)
        {
            tx.Rollback();
            return Result.Failure<DetectorHandshakeResponse>(EnrollmentTokenErrors.Expired);
        }

        // 3. Generate per-detector API key
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var base64Url = WebEncoders.Base64UrlEncode(randomBytes);
        var apiKey = $"prm_key_{base64Url}";
        var apiKeyHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        var apiKeyHash = Convert.ToHexString(apiKeyHashBytes).ToLowerInvariant();

        // 4. Query detector by Name with exclusive update lock
        var existingDetector = await connection.QuerySingleOrDefaultAsync<DetectorRecord>(
            new CommandDefinition(
                SelectDetectorWithLockSql,
                new { Name = request.Name.Trim() },
                tx,
                cancellationToken: cancellationToken));

        Guid detectorId;
        var description = string.IsNullOrWhiteSpace(request.Description) ? request.Name.Trim() : request.Description.Trim();
        var targetModule = request.TargetModule.Trim();
        var webhookUrl = string.IsNullOrWhiteSpace(request.WebhookUrl) ? null : request.WebhookUrl.Trim();
        var version = string.IsNullOrWhiteSpace(request.Version) ? null : request.Version.Trim();

        if (existingDetector is not null)
        {
            if (string.Equals(existingDetector.AdminStatus, "Retired", StringComparison.OrdinalIgnoreCase))
            {
                tx.Rollback();
                return Result.Failure<DetectorHandshakeResponse>(DetectorErrors.CannotModifyRetiredDetector);
            }

            detectorId = existingDetector.Id;

            await connection.ExecuteAsync(new CommandDefinition(
                UpdateExistingDetectorSql,
                new
                {
                    Id = detectorId,
                    Description = description,
                    Scope = scope.ToString(),
                    TargetModule = targetModule,
                    ExpectedCadenceSeconds = request.ExpectedCadenceSeconds,
                    WebhookUrl = webhookUrl,
                    ApiKeyHash = apiKeyHash,
                    Version = version
                },
                tx,
                cancellationToken: cancellationToken));

            _logger.LogInformation("Detector re-enrolled via handshake manifest sync for detector {DetectorId} ({DetectorName})", detectorId, request.Name);
        }
        else
        {
            detectorId = Guid.NewGuid();
            var utcNow = DateTime.UtcNow;

            await connection.ExecuteAsync(new CommandDefinition(
                InsertNewDetectorSql,
                new
                {
                    Id = detectorId,
                    Name = request.Name.Trim(),
                    Description = description,
                    Scope = scope.ToString(),
                    TargetModule = targetModule,
                    ExpectedCadenceSeconds = request.ExpectedCadenceSeconds,
                    WebhookUrl = webhookUrl,
                    ApiKeyHash = apiKeyHash,
                    Version = version,
                    CreatedAtUtc = utcNow
                },
                tx,
                cancellationToken: cancellationToken));

            _logger.LogInformation("New detector enrolled via handshake: {DetectorId} ({DetectorName})", detectorId, request.Name);
        }

        // 5. Consume token atomically
        await connection.ExecuteAsync(new CommandDefinition(
            ConsumeEnrollmentTokenSql,
            new { DetectorId = detectorId, TokenId = tokenRecord.Id },
            tx,
            cancellationToken: cancellationToken));

        tx.Commit();

        return Result.Success(new DetectorHandshakeResponse(
            DetectorId: detectorId,
            ApiKey: apiKey,
            Status: "Active",
            Name: request.Name.Trim(),
            Version: version));
    }

    private sealed record EnrollmentTokenRecord(
        Guid Id,
        string TokenPrefix,
        string TokenHash,
        string? Label,
        string Status,
        DateTime ExpiresAtUtc,
        string CreatedByOfficer,
        DateTime CreatedAtUtc,
        DateTime? ConsumedAtUtc,
        Guid? ConsumedByDetectorId);

    private sealed record DetectorRecord(
        Guid Id,
        string Name,
        string Description,
        string Scope,
        string TargetModule,
        int ExpectedCadenceSeconds,
        string? WebhookUrl,
        string AdminStatus,
        string HealthStatus,
        DateTime? LastRunAtUtc,
        DateTime? LastHealthyHeartbeatUtc,
        DateTime CreatedAtUtc,
        string? ApiKeyHash,
        string? Version);
}
