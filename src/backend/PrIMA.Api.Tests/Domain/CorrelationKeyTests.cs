using FluentAssertions;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Tests.Domain;

public class CorrelationKeyTests
{
    [Fact]
    public void Create_WithValidInputs_ProducesCanonicalKey()
    {
        // Arrange
        var detectorId = Guid.Parse("a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d");
        var targetEntity = "billing_record_98745";

        // Act
        var key = CorrelationKey.Create(detectorId, targetEntity);

        // Assert
        key.Value.Should().Be("a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d:BILLING_RECORD_98745");
        key.ToString().Should().Be("a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d:BILLING_RECORD_98745");
        string stringVal = key;
        stringVal.Should().Be("a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d:BILLING_RECORD_98745");
    }

    [Fact]
    public void Create_TrimsWhitespaceAndConvertsToUppercase()
    {
        // Arrange
        var detectorId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var targetEntity = "   patient_adm-00123 \t  ";

        // Act
        var key = CorrelationKey.Create(detectorId, targetEntity);

        // Assert
        key.Value.Should().Be("11111111-2222-3333-4444-555555555555:PATIENT_ADM-00123");
    }

    [Fact]
    public void Create_WhenDetectorIdIsEmpty_ThrowsArgumentException()
    {
        // Act
        var act = () => CorrelationKey.Create(Guid.Empty, "ENTITY-123");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("detectorId")
            .WithMessage("*Detector ID cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Create_WhenTargetEntityReferenceIsNullOrWhitespace_ThrowsArgumentException(string? invalidEntity)
    {
        // Act
        var act = () => CorrelationKey.Create(Guid.NewGuid(), invalidEntity!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("targetEntityReference");
    }

    [Fact]
    public void Create_WhenCanonicalKeyExceedsMaxLength_ThrowsArgumentException()
    {
        // Guid formatted: 36 chars, colon: 1 char -> 37 chars prefix.
        // MaxLength is 250. Allowed targetEntity length: 250 - 37 = 213 chars.
        var detectorId = Guid.NewGuid();
        var entityExceedingMax = new string('A', 214); // 36 + 1 + 214 = 251

        // Act
        var act = () => CorrelationKey.Create(detectorId, entityExceedingMax);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("targetEntityReference")
            .WithMessage("*exceeds maximum allowed length of 250*");
    }

    [Fact]
    public void Create_WhenCanonicalKeyAtMaxLength_Succeeds()
    {
        var detectorId = Guid.NewGuid();
        var entityAtMax = new string('X', 213); // 36 + 1 + 213 = 250

        // Act
        var key = CorrelationKey.Create(detectorId, entityAtMax);

        // Assert
        key.Value.Length.Should().Be(250);
        key.Value.Should().StartWith($"{detectorId:D}:");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromCanonical_WhenIsNullOrWhitespace_ThrowsArgumentException(string? invalidValue)
    {
        // Act
        var act = () => CorrelationKey.FromCanonical(invalidValue!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("value");
    }

    [Fact]
    public void FromCanonical_WhenExceedsMaxLength_ThrowsArgumentException()
    {
        var tooLong = new string('A', 251);

        // Act
        var act = () => CorrelationKey.FromCanonical(tooLong);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("value")
            .WithMessage("*exceeds maximum allowed length of 250*");
    }

    [Fact]
    public void FromCanonical_TrimsInputAndCreatesInstance()
    {
        var raw = "  some-canonical-correlation-key  ";

        // Act
        var key = CorrelationKey.FromCanonical(raw);

        // Assert
        key.Value.Should().Be("some-canonical-correlation-key");
    }

    [Fact]
    public void ImplicitOperator_ConvertsStringToCorrelationKeyAndViceVersa()
    {
        CorrelationKey key = "valid-canonical-key";
        key.Value.Should().Be("valid-canonical-key");

        string stringVal = key;
        stringVal.Should().Be("valid-canonical-key");
    }
}
