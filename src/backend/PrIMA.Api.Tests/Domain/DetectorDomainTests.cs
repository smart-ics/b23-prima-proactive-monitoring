using FluentAssertions;
using PrIMA.Api.Domain.Detectors;

namespace PrIMA.Api.Tests.Domain;

public class DetectorDomainTests
{
    private static Detector CreateTestDetector(
        int expectedCadenceSeconds = 300,
        DateTime? createdAtUtc = null)
    {
        return Detector.Create(
            name: "Telemetry Verification Probe",
            description: "Monitors transaction processing latency",
            scope: MonitoringScope.BusinessLogic,
            targetModule: "CoreTransactions",
            expectedCadenceSeconds: expectedCadenceSeconds,
            webhookUrl: "https://probe.internal/transactions/check",
            createdAtUtc: createdAtUtc ?? DateTime.UtcNow);
    }

    [Fact]
    public void UpdateConfiguration_WhenActive_UpdatesMutablePropertiesSuccessfully()
    {
        // Arrange
        var detector = CreateTestDetector();

        // Act
        var result = detector.UpdateConfiguration(
            description: "Updated probe description",
            scope: MonitoringScope.DataQuality,
            targetModule: "Settlement",
            expectedCadenceSeconds: 900,
            webhookUrl: "https://probe.internal/settlement/check");

        // Assert
        result.IsSuccess.Should().BeTrue();
        detector.Description.Should().Be("Updated probe description");
        detector.Scope.Should().Be(MonitoringScope.DataQuality);
        detector.TargetModule.Should().Be("Settlement");
        detector.ExpectedCadenceSeconds.Should().Be(900);
        detector.WebhookUrl.Should().Be("https://probe.internal/settlement/check");
    }

    [Fact]
    public void UpdateConfiguration_WhenDetectorRetired_FailsWithCannotModifyRetiredDetector()
    {
        // Arrange
        var detector = CreateTestDetector();
        var retireResult = detector.Retire();
        retireResult.IsSuccess.Should().BeTrue();
        detector.IsRetired.Should().BeTrue();

        // Act
        var result = detector.UpdateConfiguration(
            description: "Attempted change on retired detector",
            scope: MonitoringScope.ProcessFlow,
            targetModule: "Settlement",
            expectedCadenceSeconds: 600,
            webhookUrl: "https://probe.internal/retired/check");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(DetectorErrors.CannotModifyRetiredDetector.Code);
    }

    [Fact]
    public void Pause_Resume_Retire_TransitionsFollowLifecycleRules()
    {
        // Arrange
        var detector = CreateTestDetector();
        detector.AdminStatus.Should().Be(AdminStatus.Active);
        detector.IsActive.Should().BeTrue();

        // 1. Pause active detector
        var pauseResult = detector.Pause();
        pauseResult.IsSuccess.Should().BeTrue();
        detector.AdminStatus.Should().Be(AdminStatus.Paused);
        detector.IsPaused.Should().BeTrue();

        // Already paused -> fails
        var duplicatePause = detector.Pause();
        duplicatePause.IsSuccess.Should().BeFalse();
        duplicatePause.Error.Code.Should().Be(DetectorErrors.AlreadyPaused.Code);

        // 2. Resume paused detector
        var resumeResult = detector.Resume();
        resumeResult.IsSuccess.Should().BeTrue();
        detector.AdminStatus.Should().Be(AdminStatus.Active);
        detector.IsActive.Should().BeTrue();

        // Already active -> fails
        var duplicateResume = detector.Resume();
        duplicateResume.IsSuccess.Should().BeFalse();
        duplicateResume.Error.Code.Should().Be(DetectorErrors.AlreadyActive.Code);

        // 3. Retire active detector
        var retireResult = detector.Retire();
        retireResult.IsSuccess.Should().BeTrue();
        detector.AdminStatus.Should().Be(AdminStatus.Retired);
        detector.IsRetired.Should().BeTrue();

        // Already retired -> fails
        var duplicateRetire = detector.Retire();
        duplicateRetire.IsSuccess.Should().BeFalse();
        duplicateRetire.Error.Code.Should().Be(DetectorErrors.AlreadyRetired.Code);

        // Attempting to pause or resume a retired detector must fail
        detector.Pause().IsSuccess.Should().BeFalse();
        detector.Resume().IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void IsSilenceThresholdExceeded_CalculatesDoubleCadenceThresholdAccurately()
    {
        var now = DateTime.UtcNow;
        // Cadence = 300s -> threshold = 600s
        var detector = CreateTestDetector(expectedCadenceSeconds: 300, createdAtUtc: now.AddSeconds(-800));

        // When Paused, silence threshold is never exceeded
        detector.Pause();
        detector.IsSilenceThresholdExceeded(now).Should().BeFalse();

        // When Resumed (Active) with no previous heartbeat, uses CreatedAtUtc (800s > 600s)
        detector.Resume();
        detector.IsSilenceThresholdExceeded(now).Should().BeTrue();

        // Record a heartbeat 400s ago (400s <= 600s) -> not exceeded
        detector.RecordSuccessfulRun(now.AddSeconds(-400));
        detector.IsSilenceThresholdExceeded(now).Should().BeFalse();

        // At 650s after heartbeat -> exceeded
        detector.IsSilenceThresholdExceeded(now.AddSeconds(250)).Should().BeTrue();
    }

    [Fact]
    public void HealthStatusUpdates_MarkSilent_MarkFailing_RecordSuccessfulRun()
    {
        // Arrange
        var detector = CreateTestDetector();
        detector.HealthStatus.Should().Be(DetectorHealth.Healthy);

        // 1. MarkSilent
        var silentTimestamp = DateTime.UtcNow.AddMinutes(-10);
        detector.MarkSilent(silentTimestamp);
        detector.HealthStatus.Should().Be(DetectorHealth.Silent);

        // 2. MarkFailing
        var failingTimestamp = DateTime.UtcNow.AddMinutes(-5);
        detector.MarkFailing(failingTimestamp);
        detector.HealthStatus.Should().Be(DetectorHealth.Failing);
        detector.LastRunAtUtc.Should().Be(failingTimestamp);

        // 3. RecordSuccessfulRun recovers to Healthy and updates both timestamps
        var successTimestamp = DateTime.UtcNow;
        detector.RecordSuccessfulRun(successTimestamp);
        detector.HealthStatus.Should().Be(DetectorHealth.Healthy);
        detector.LastRunAtUtc.Should().Be(successTimestamp);
        detector.LastHealthyHeartbeatUtc.Should().Be(successTimestamp);
    }
}
