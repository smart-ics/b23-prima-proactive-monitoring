using FluentAssertions;
using PrIMA.Api.Domain.Detectors;

namespace PrIMA.Api.Tests.Domain;

public class DetectorAggregateTests
{
    private static Detector CreateTestDetector(
        int expectedCadenceSeconds = 300,
        DateTime? createdAtUtc = null)
    {
        return Detector.Create(
            name: "Billing Reconciliation Probe",
            description: "Monitors debit/credit ledger equality",
            scope: MonitoringScope.DataIntegrity,
            targetModule: "Billing",
            expectedCadenceSeconds: expectedCadenceSeconds,
            webhookUrl: "https://probe.internal/billing/reconcile",
            createdAtUtc: createdAtUtc ?? DateTime.UtcNow);
    }

    [Fact]
    public void UpdateConfiguration_WhenValid_UpdatesAllMutablePropertiesAndTrims()
    {
        // Arrange
        var detector = CreateTestDetector();

        // Act
        var result = detector.UpdateConfiguration(
            description: "  Updated description  ",
            scope: MonitoringScope.ProcessIntegrity,
            targetModule: "  Pharmacy  ",
            expectedCadenceSeconds: 600,
            webhookUrl: "  https://probe.internal/pharmacy  ");

        // Assert
        result.IsSuccess.Should().BeTrue();
        detector.Description.Should().Be("Updated description");
        detector.Scope.Should().Be(MonitoringScope.ProcessIntegrity);
        detector.TargetModule.Should().Be("Pharmacy");
        detector.ExpectedCadenceSeconds.Should().Be(600);
        detector.WebhookUrl.Should().Be("https://probe.internal/pharmacy");
    }

    [Fact]
    public void UpdateConfiguration_WhenWebhookUrlIsWhitespaceOrEmpty_SetsWebhookUrlToNull()
    {
        // Arrange
        var detector = CreateTestDetector();

        // Act
        var result = detector.UpdateConfiguration(
            description: "Valid Description",
            scope: MonitoringScope.DataIntegrity,
            targetModule: "Billing",
            expectedCadenceSeconds: 300,
            webhookUrl: "   ");

        // Assert
        result.IsSuccess.Should().BeTrue();
        detector.WebhookUrl.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UpdateConfiguration_WhenDescriptionEmptyOrWhitespace_FailsWithEmptyDescription(string? invalidDescription)
    {
        // Arrange
        var detector = CreateTestDetector();

        // Act
        var result = detector.UpdateConfiguration(
            description: invalidDescription!,
            scope: MonitoringScope.DataIntegrity,
            targetModule: "Billing",
            expectedCadenceSeconds: 300,
            webhookUrl: null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(DetectorErrors.EmptyDescription.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UpdateConfiguration_WhenTargetModuleEmptyOrWhitespace_FailsWithEmptyTargetModule(string? invalidModule)
    {
        // Arrange
        var detector = CreateTestDetector();

        // Act
        var result = detector.UpdateConfiguration(
            description: "Valid Description",
            scope: MonitoringScope.DataIntegrity,
            targetModule: invalidModule!,
            expectedCadenceSeconds: 300,
            webhookUrl: null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(DetectorErrors.EmptyTargetModule.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void UpdateConfiguration_WhenCadenceZeroOrNegative_FailsWithInvalidCadence(int invalidCadence)
    {
        // Arrange
        var detector = CreateTestDetector();

        // Act
        var result = detector.UpdateConfiguration(
            description: "Valid Description",
            scope: MonitoringScope.DataIntegrity,
            targetModule: "Billing",
            expectedCadenceSeconds: invalidCadence,
            webhookUrl: null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(DetectorErrors.InvalidCadence.Code);
    }

    [Fact]
    public void UpdateConfiguration_WhenDetectorRetired_FailsWithCannotModifyRetiredDetector()
    {
        // Arrange
        var detector = CreateTestDetector();
        detector.Retire();
        detector.IsRetired.Should().BeTrue();

        // Act
        var result = detector.UpdateConfiguration(
            description: "Valid Description",
            scope: MonitoringScope.DataIntegrity,
            targetModule: "Billing",
            expectedCadenceSeconds: 300,
            webhookUrl: null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(DetectorErrors.CannotModifyRetiredDetector.Code);
    }

    [Fact]
    public void Pause_WhenActive_TransitionsToPaused()
    {
        // Arrange
        var detector = CreateTestDetector();
        detector.IsActive.Should().BeTrue();

        // Act
        var result = detector.Pause();

        // Assert
        result.IsSuccess.Should().BeTrue();
        detector.IsPaused.Should().BeTrue();
        detector.AdminStatus.Should().Be(AdminStatus.Paused);
    }

    [Fact]
    public void Pause_WhenAlreadyPaused_FailsWithAlreadyPaused()
    {
        // Arrange
        var detector = CreateTestDetector();
        detector.Pause();
        detector.IsPaused.Should().BeTrue();

        // Act
        var result = detector.Pause();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(DetectorErrors.AlreadyPaused.Code);
    }

    [Fact]
    public void Pause_WhenRetired_FailsWithCannotModifyRetiredDetector()
    {
        // Arrange
        var detector = CreateTestDetector();
        detector.Retire();

        // Act
        var result = detector.Pause();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(DetectorErrors.CannotModifyRetiredDetector.Code);
    }

    [Fact]
    public void Resume_WhenPaused_TransitionsToActive()
    {
        // Arrange
        var detector = CreateTestDetector();
        detector.Pause();
        detector.IsPaused.Should().BeTrue();

        // Act
        var result = detector.Resume();

        // Assert
        result.IsSuccess.Should().BeTrue();
        detector.IsActive.Should().BeTrue();
        detector.AdminStatus.Should().Be(AdminStatus.Active);
    }

    [Fact]
    public void Resume_WhenAlreadyActive_FailsWithAlreadyActive()
    {
        // Arrange
        var detector = CreateTestDetector();
        detector.IsActive.Should().BeTrue();

        // Act
        var result = detector.Resume();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(DetectorErrors.AlreadyActive.Code);
    }

    [Fact]
    public void Resume_WhenRetired_FailsWithCannotModifyRetiredDetector()
    {
        // Arrange
        var detector = CreateTestDetector();
        detector.Retire();

        // Act
        var result = detector.Resume();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(DetectorErrors.CannotModifyRetiredDetector.Code);
    }

    [Fact]
    public void Retire_WhenActiveOrPaused_TransitionsToRetired()
    {
        // Arrange
        var detector = CreateTestDetector();

        // Act
        var result = detector.Retire();

        // Assert
        result.IsSuccess.Should().BeTrue();
        detector.IsRetired.Should().BeTrue();
        detector.AdminStatus.Should().Be(AdminStatus.Retired);
    }

    [Fact]
    public void Retire_WhenAlreadyRetired_FailsWithAlreadyRetired()
    {
        // Arrange
        var detector = CreateTestDetector();
        detector.Retire();

        // Act
        var result = detector.Retire();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(DetectorErrors.AlreadyRetired.Code);
    }

    [Fact]
    public void IsSilenceThresholdExceeded_WhenNotActive_ReturnsFalse()
    {
        // Arrange (Paused detector past 2x cadence)
        var createdAt = DateTime.UtcNow.AddSeconds(-1000);
        var detector = CreateTestDetector(expectedCadenceSeconds: 300, createdAtUtc: createdAt);
        detector.Pause();

        // Act
        var exceeded = detector.IsSilenceThresholdExceeded(DateTime.UtcNow);

        // Assert
        exceeded.Should().BeFalse();
    }

    [Fact]
    public void IsSilenceThresholdExceeded_WhenNoHeartbeat_UsesCreatedAtUtcAsBaseline()
    {
        // Expected cadence 300s -> threshold is 600s
        var now = DateTime.UtcNow;

        // Detector created 500s ago (within 600s threshold)
        var det1 = CreateTestDetector(expectedCadenceSeconds: 300, createdAtUtc: now.AddSeconds(-500));
        det1.IsSilenceThresholdExceeded(now).Should().BeFalse();

        // Detector created 601s ago (past 600s threshold)
        var det2 = CreateTestDetector(expectedCadenceSeconds: 300, createdAtUtc: now.AddSeconds(-601));
        det2.IsSilenceThresholdExceeded(now).Should().BeTrue();
    }

    [Fact]
    public void IsSilenceThresholdExceeded_WhenHeartbeatPresent_UsesLastHealthyHeartbeatUtcAsBaseline()
    {
        var now = DateTime.UtcNow;
        // Created long ago, but healthy heartbeat recorded recently
        var det = CreateTestDetector(expectedCadenceSeconds: 300, createdAtUtc: now.AddHours(-10));
        det.RecordSuccessfulRun(now.AddSeconds(-500));

        // 500s <= 600s -> not exceeded
        det.IsSilenceThresholdExceeded(now).Should().BeFalse();

        // At now + 150s (650s from heartbeat) -> exceeded
        det.IsSilenceThresholdExceeded(now.AddSeconds(150)).Should().BeTrue();
    }

    [Fact]
    public void MarkSilent_SetsHealthStatusToSilent()
    {
        // Arrange
        var detector = CreateTestDetector();
        detector.HealthStatus.Should().Be(DetectorHealth.Healthy);

        // Act
        detector.MarkSilent(DateTime.UtcNow);

        // Assert
        detector.HealthStatus.Should().Be(DetectorHealth.Silent);
    }

    [Fact]
    public void MarkFailing_SetsHealthStatusToFailingAndUpdatesLastRunAtUtc()
    {
        // Arrange
        var detector = CreateTestDetector();
        var failureTime = DateTime.UtcNow;

        // Act
        detector.MarkFailing(failureTime);

        // Assert
        detector.HealthStatus.Should().Be(DetectorHealth.Failing);
        detector.LastRunAtUtc.Should().Be(failureTime);
    }

    [Fact]
    public void RecordSuccessfulRun_SetsHealthyAndUpdatesLastRunAndLastHealthyHeartbeat()
    {
        // Arrange
        var detector = CreateTestDetector();
        detector.MarkFailing(DateTime.UtcNow.AddMinutes(-5));
        var successTime = DateTime.UtcNow;

        // Act
        detector.RecordSuccessfulRun(successTime);

        // Assert
        detector.HealthStatus.Should().Be(DetectorHealth.Healthy);
        detector.LastRunAtUtc.Should().Be(successTime);
        detector.LastHealthyHeartbeatUtc.Should().Be(successTime);
    }
}
