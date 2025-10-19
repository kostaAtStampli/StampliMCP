using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StampliMCP.McpServer.Acumatica.Models;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tests.Unit;

public class FuzzyMatchingServiceTests
{
    private readonly FuzzyMatchingService _service;

    public FuzzyMatchingServiceTests()
    {
        var config = new FuzzyMatchingConfig
        {
            DefaultThreshold = 0.60,
            TypoToleranceThreshold = 0.70,
            OperationMatchThreshold = 0.60,
            ErrorMatchThreshold = 0.65,
            FlowMatchThreshold = 0.60,
            KeywordMatchThreshold = 0.60
        };
        var logger = NullLogger<FuzzyMatchingService>.Instance;
        _service = new FuzzyMatchingService(config, logger);
    }

    [Fact]
    public void FindAllMatches_ExactMatch_ReturnsConfidence100()
    {
        // Arrange
        var query = "vendor";
        var patterns = new[] { "vendor", "payment", "invoice" };

        // Act
        var matches = _service.FindAllMatches(query, patterns, 0.60);

        // Assert
        matches.Should().ContainSingle();
        matches.First().Pattern.Should().Be("vendor");
        matches.First().Confidence.Should().Be(1.0);
        matches.First().Distance.Should().Be(0);
    }

    [Fact]
    public void FindAllMatches_TypoTolerance_ReturnsFuzzyMatches()
    {
        // Arrange - "vendro" is 1 char different from "vendor" (60% match)
        var query = "vendro";
        var patterns = new[] { "vendor", "payment", "invoice" };

        // Act
        var matches = _service.FindAllMatches(query, patterns, 0.60);

        // Assert
        matches.Should().NotBeEmpty();
        matches.First().Pattern.Should().Be("vendor");
        matches.First().Distance.Should().Be(1);
        matches.First().Confidence.Should().BeGreaterThan(0.80); // ~83% for 1 char diff in 6 chars
    }

    [Fact]
    public void FindAllMatches_MultipleTypos_ReturnsMultipleMatches()
    {
        // Arrange - "exprt" matches both "export" and "expert"
        var query = "exprt";
        var patterns = new[] { "export", "expert", "import", "vendor" };

        // Act
        var matches = _service.FindAllMatches(query, patterns, 0.60);

        // Assert
        matches.Should().HaveCountGreaterThanOrEqualTo(2);
        matches.Should().Contain(m => m.Pattern == "export");
        matches.Should().Contain(m => m.Pattern == "expert");
    }

    [Fact]
    public void FindAllMatches_BelowThreshold_ReturnsEmpty()
    {
        // Arrange - "xyz" is completely different from all patterns
        var query = "xyz";
        var patterns = new[] { "vendor", "payment", "invoice" };

        // Act
        var matches = _service.FindAllMatches(query, patterns, 0.60);

        // Assert
        matches.Should().BeEmpty();
    }

    [Fact]
    public void FindBestMatch_ReturnsHighestConfidence()
    {
        // Arrange
        var query = "vendor";
        var patterns = new[] { "vendor", "vendro", "vendors" }; // "vendor" is exact match

        // Act
        var bestMatch = _service.FindBestMatch(query, patterns, 0.60);

        // Assert
        bestMatch.Should().NotBeNull();
        bestMatch!.Pattern.Should().Be("vendor");
        bestMatch.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void FindBestMatch_NoMatches_ReturnsNull()
    {
        // Arrange
        var query = "xyz";
        var patterns = new[] { "vendor", "payment", "invoice" };

        // Act
        var bestMatch = _service.FindBestMatch(query, patterns, 0.60);

        // Assert
        bestMatch.Should().BeNull();
    }

    [Fact]
    public void GetThreshold_ReturnsCorrectConfigValue()
    {
        // Act & Assert
        _service.GetThreshold("operation").Should().Be(0.60);
        _service.GetThreshold("typo").Should().Be(0.70);
        _service.GetThreshold("error").Should().Be(0.65);
        _service.GetThreshold("flow").Should().Be(0.60);
        _service.GetThreshold("keyword").Should().Be(0.60);
        _service.GetThreshold("unknown").Should().Be(0.60); // default
    }

    [Fact]
    public void FindAllMatches_CaseInsensitive_Works()
    {
        // Arrange - different cases should still match
        var query = "VENDOR";
        var patterns = new[] { "vendor", "payment", "invoice" };

        // Act
        var matches = _service.FindAllMatches(query, patterns, 0.60);

        // Assert
        matches.Should().ContainSingle();
        matches.First().Pattern.Should().Be("vendor");
        matches.First().Confidence.Should().Be(1.0);
    }

    [Fact]
    public void FindAllMatches_CommonTypos_AllWork()
    {
        // Test common typos that should be caught
        var testCases = new[]
        {
            ("vendro", "vendor"),       // transposition
            ("payent", "payment"),      // missing letter
            ("invocie", "invoice"),     // transposition
            ("exprt", "export"),        // missing letter
            ("imprt", "import"),        // missing letter
            ("itms", "items"),          // missing letter (4 chars, 75% match)
        };

        foreach (var (query, expected) in testCases)
        {
            // Act
            var matches = _service.FindAllMatches(query, new[] { expected }, 0.60);

            // Assert
            matches.Should().NotBeEmpty($"'{query}' should fuzzy-match '{expected}'");
            matches.First().Pattern.Should().Be(expected);
            matches.First().Confidence.Should().BeGreaterThan(0.60);
        }
    }

    [Fact]
    public void FindAllMatches_OrderedByConfidence_Descending()
    {
        // Arrange - query should match all with different confidences
        var query = "ven";
        var patterns = new[] { "vendor", "ven", "vendors", "payment" };

        // Act
        var matches = _service.FindAllMatches(query, patterns, 0.50);

        // Assert
        matches.Should().HaveCountGreaterThanOrEqualTo(3);

        // Should be ordered by confidence descending
        var confidences = matches.Select(m => m.Confidence).ToList();
        confidences.Should().BeInDescendingOrder();

        // "ven" exact match should be first
        matches.First().Pattern.Should().Be("ven");
        matches.First().Confidence.Should().Be(1.0);
    }
}
