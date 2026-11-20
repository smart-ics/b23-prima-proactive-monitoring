using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.Extensions.Configuration;
using PrIMA.Api.Common;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Features.Issues.GetDashboardFeed;

public sealed class GetDashboardFeedHandler
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IConfiguration _configuration;

    private const string DashboardFeedSql = """
        SELECT
            COUNT(CASE WHEN Status NOT IN ('Resolved', 'Dismissed') AND CurrentSeverity = 'Critical' THEN 1 END) AS CriticalCount,
            COUNT(CASE WHEN Status NOT IN ('Resolved', 'Dismissed') AND CurrentSeverity = 'High' THEN 1 END) AS HighCount,
            COUNT(CASE WHEN Status = 'Open' THEN 1 END) AS OpenCount,
            COUNT(CASE WHEN Status = 'Reopened' THEN 1 END) AS ReopenedCount,
            COUNT(CASE WHEN Status = 'Resolved' AND ResolvedAtUtc >= @StartOfTodayWibUtc AND ResolvedAtUtc < @StartOfTomorrowWibUtc THEN 1 END) AS ResolvedTodayCount
        FROM dbo.Issues;

        SELECT
            Id,
            CorrelationKey,
            Title,
            DetectorId,
            DetectorName,
            Scope,
            CurrentSeverity,
            SeverityRank,
            Status,
            EscalationLevel,
            OccurrenceCount,
            CurrentAssignee,
            CreatedAtUtc,
            LastOccurrenceAtUtc
        FROM dbo.Issues
        WHERE Status NOT IN ('Resolved', 'Dismissed')
          AND (@Status IS NULL OR Status = @Status)
          AND (@Severity IS NULL OR CurrentSeverity = @Severity)
          AND (@Scope IS NULL OR Scope = @Scope)
          AND (@Search IS NULL OR (
               Title LIKE '%' + @Search + '%' OR
               DetectorName LIKE '%' + @Search + '%' OR
               CorrelationKey LIKE '%' + @Search + '%'
          ))
        ORDER BY SeverityRank DESC, LastOccurrenceAtUtc DESC;
        """;

    public GetDashboardFeedHandler(
        ISqlConnectionFactory connectionFactory,
        IConfiguration configuration)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<Result<GetDashboardFeedResponse>> HandleAsync(
        GetDashboardFeedQuery query,
        CancellationToken cancellationToken = default)
    {
        // Query validation: reject closed status filters with 422 Unprocessable Entity
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var trimmedStatus = query.Status.Trim();
            if (string.Equals(trimmedStatus, "Resolved", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmedStatus, "Dismissed", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<GetDashboardFeedResponse>(IssueErrors.ClosedStatusFeedFilter);
            }
        }

        var (startOfTodayWibUtc, startOfTomorrowWibUtc) = CalculateOperationalDayBoundariesUtc();

        string? statusParam = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var trimmed = query.Status.Trim();
            statusParam = Enum.TryParse<IssueStatus>(trimmed, ignoreCase: true, out var parsedStatus)
                ? parsedStatus.ToString()
                : trimmed;
        }

        string? severityParam = null;
        if (!string.IsNullOrWhiteSpace(query.Severity))
        {
            var trimmed = query.Severity.Trim();
            severityParam = Enum.TryParse<IssueSeverity>(trimmed, ignoreCase: true, out var parsedSeverity)
                ? parsedSeverity.ToString()
                : trimmed;
        }

        string? scopeParam = null;
        if (!string.IsNullOrWhiteSpace(query.Scope))
        {
            var trimmed = query.Scope.Trim();
            scopeParam = Enum.TryParse<MonitoringScope>(trimmed, ignoreCase: true, out var parsedScope)
                ? parsedScope.ToString()
                : trimmed;
        }

        string? searchParam = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        var parameters = new
        {
            StartOfTodayWibUtc = startOfTodayWibUtc,
            StartOfTomorrowWibUtc = startOfTomorrowWibUtc,
            Status = statusParam,
            Severity = severityParam,
            Scope = scopeParam,
            Search = searchParam
        };

        using var connection = _connectionFactory.CreateConnection();
        if (connection is DbConnection dbConnection)
        {
            await dbConnection.OpenAsync(cancellationToken);
        }
        else
        {
            connection.Open();
        }

        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(DashboardFeedSql, parameters, cancellationToken: cancellationToken));

        var summary = await multi.ReadSingleOrDefaultAsync<DashboardFeedSummaryDto>()
            ?? new DashboardFeedSummaryDto(0, 0, 0, 0, 0);

        var items = (await multi.ReadAsync<IssueFeedItemDto>()).ToList();

        return Result.Success(new GetDashboardFeedResponse(summary, items));
    }

    private (DateTime StartOfTodayWibUtc, DateTime StartOfTomorrowWibUtc) CalculateOperationalDayBoundariesUtc()
    {
        var operationalTimeZone = GetOperationalTimeZone();
        var utcNow = DateTime.UtcNow;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, operationalTimeZone);
        var localStartOfToday = localNow.Date;
        var localStartOfTomorrow = localStartOfToday.AddDays(1);

        var startOfTodayWibUtc = TimeZoneInfo.ConvertTimeToUtc(localStartOfToday, operationalTimeZone);
        var startOfTomorrowWibUtc = TimeZoneInfo.ConvertTimeToUtc(localStartOfTomorrow, operationalTimeZone);

        return (startOfTodayWibUtc, startOfTomorrowWibUtc);
    }

    private TimeZoneInfo GetOperationalTimeZone()
    {
        var timeZoneId = _configuration["OperationalTimeZone"] ?? "Asia/Jakarta";
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.CreateCustomTimeZone("Asia/Jakarta", TimeSpan.FromHours(7), "WIB", "WIB");
            }
        }
    }
}
