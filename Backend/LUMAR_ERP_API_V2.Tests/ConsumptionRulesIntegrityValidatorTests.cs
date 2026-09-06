using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public class ConsumptionRulesIntegrityValidatorTests
{
    [Fact]
    public void ShouldTreatEqualBoundariesAsConnectedWithoutOverlap()
    {
        var rules = new[]
        {
            new ConsumptionRuleIntegrityCandidate(1, 58, 1, "hips", 1, 1, 30, "formula", "Active"),
            new ConsumptionRuleIntegrityCandidate(1, 58, 2, "hips", 2, 30, 38, "formula", "Active"),
            new ConsumptionRuleIntegrityCandidate(1, 58, 3, "hips", 3, 38, 43, "formula", "Active"),
            new ConsumptionRuleIntegrityCandidate(1, 58, 4, "hips", 4, 43, null, "formula", "Active")
        };

        var result = ConsumptionRuleIntegrityValidator.Validate(rules);

        Assert.Equal(0, result.OverlapCount);
        Assert.Equal(0, result.GapCount);
    }

    [Fact]
    public void ShouldMatchOnlyTheHalfOpenRangeForDecimalValues()
    {
        var rules = new[]
        {
            new ConsumptionRuleIntegrityCandidate(1, 58, 1, "hips", 1, 1, 30, "formula", "Active"),
            new ConsumptionRuleIntegrityCandidate(1, 58, 2, "hips", 2, 30, 38, "formula", "Active"),
            new ConsumptionRuleIntegrityCandidate(1, 58, 3, "hips", 3, 38, 43, "formula", "Active"),
            new ConsumptionRuleIntegrityCandidate(1, 58, 4, "hips", 4, 43, null, "formula", "Active")
        };

        Assert.Equal(1, ConsumptionRuleRangePolicy.FindMatches(rules, 1, "hips", 29.9m, 58).Single().SizeClassId);
        Assert.Equal(2, ConsumptionRuleRangePolicy.FindMatches(rules, 1, "hips", 30m, 58).Single().SizeClassId);
        Assert.Equal(2, ConsumptionRuleRangePolicy.FindMatches(rules, 1, "hips", 30.5m, 58).Single().SizeClassId);
        Assert.Equal(2, ConsumptionRuleRangePolicy.FindMatches(rules, 1, "hips", 37.9m, 58).Single().SizeClassId);
        Assert.Equal(3, ConsumptionRuleRangePolicy.FindMatches(rules, 1, "hips", 38m, 58).Single().SizeClassId);
        Assert.Equal(3, ConsumptionRuleRangePolicy.FindMatches(rules, 1, "hips", 42.9m, 58).Single().SizeClassId);
        Assert.Equal(4, ConsumptionRuleRangePolicy.FindMatches(rules, 1, "hips", 43m, 58).Single().SizeClassId);
        Assert.Equal(4, ConsumptionRuleRangePolicy.FindMatches(rules, 1, "hips", 50.5m, 58).Single().SizeClassId);
    }

    [Fact]
    public void ShouldDetectRealOverlapButNotBoundaryTouching()
    {
        var touching = new[]
        {
            new ConsumptionRuleIntegrityCandidate(1, 58, 1, "hips", 1, 30, 38, "formula", "Active"),
            new ConsumptionRuleIntegrityCandidate(1, 58, 2, "hips", 2, 38, 43, "formula", "Active")
        };
        var overlapping = new[]
        {
            new ConsumptionRuleIntegrityCandidate(1, 58, 1, "hips", 1, 30, 39, "formula", "Active"),
            new ConsumptionRuleIntegrityCandidate(1, 58, 2, "hips", 2, 38, 43, "formula", "Active")
        };

        Assert.Equal(0, ConsumptionRuleIntegrityValidator.Validate(touching).OverlapCount);
        Assert.Equal(1, ConsumptionRuleIntegrityValidator.Validate(overlapping).OverlapCount);
    }

    [Fact]
    public void ShouldDetectDecimalGapWithoutAddingOne()
    {
        var rules = new[]
        {
            new ConsumptionRuleIntegrityCandidate(1, 58, 1, "hips", 1, 1, 30, "formula", "Active"),
            new ConsumptionRuleIntegrityCandidate(1, 58, 2, "hips", 2, 31, 38, "formula", "Active")
        };

        Assert.Equal(1, ConsumptionRuleIntegrityValidator.Validate(rules).GapCount);
    }

    [Fact]
    public void ShouldAcceptDecimalSafeNonPantsCoverageExample()
    {
        var rules = new[]
        {
            new ConsumptionRuleIntegrityCandidate(99, 58, 1, "custom_measurement", 1, 10, 25, "formula", "Active"),
            new ConsumptionRuleIntegrityCandidate(99, 58, 2, "custom_measurement", 2, 25, 40, "formula", "Active"),
            new ConsumptionRuleIntegrityCandidate(99, 58, 3, "custom_measurement", 3, 40, 55, "formula", "Active"),
            new ConsumptionRuleIntegrityCandidate(99, 58, 4, "custom_measurement", 4, 55, null, "formula", "Active")
        };

        var result = ConsumptionRuleIntegrityValidator.Validate(rules);

        Assert.Equal(0, result.OverlapCount);
        Assert.Equal(0, result.GapCount);
        Assert.Equal(2, ConsumptionRuleRangePolicy.FindSingleMatch(rules, 99, "custom_measurement", 25m, 58)?.SizeClassId);
        Assert.Equal(4, ConsumptionRuleRangePolicy.FindSingleMatch(rules, 99, "custom_measurement", 55m, 58)?.SizeClassId);
    }

    [Fact]
    public void ShouldKeepDifferentMeasurementKeysIndependent()
    {
        var rules = new[]
        {
            new ConsumptionRuleIntegrityCandidate(1, 58, 1, "hips", 1, 1, 30, "formula", "Active"),
            new ConsumptionRuleIntegrityCandidate(1, 58, 2, "waist", 2, 1, 30, "formula", "Active")
        };

        var result = ConsumptionRuleIntegrityValidator.Validate(rules);

        Assert.Equal(0, result.OverlapCount);
        Assert.Equal(0, result.GapCount);
    }

    [Fact]
    public void ShouldRejectAnOpenRangeBeforeTheLastRange()
    {
        var rules = new[]
        {
            new ConsumptionRuleIntegrityCandidate(1, 58, 1, "hips", 1, 1, null, "formula", "Active"),
            new ConsumptionRuleIntegrityCandidate(1, 58, 2, "hips", 2, 30, 38, "formula", "Active")
        };

        var result = ConsumptionRuleIntegrityValidator.Validate(rules);

        Assert.Contains(result.Issues, issue => issue.Type == "MISSING_MAXIMUM");
    }

    [Fact]
    public void ShouldRejectAnEmptyRange()
    {
        var rules = new[]
        {
            new ConsumptionRuleIntegrityCandidate(1, 58, 1, "hips", 1, 30, 30, "formula", "Active")
        };

        var result = ConsumptionRuleIntegrityValidator.Validate(rules);

        Assert.Contains(result.Issues, issue => issue.Type == "INVALID_RANGE");
    }

    [Fact]
    public void ShouldDetectDuplicateRanges()
    {
        var rules = new[]
        {
            new ConsumptionRuleIntegrityCandidate(1, 58, 1, "سعة الصدر", 1, 42, null, "kot_length + sleeve_length + 10", "Active"),
            new ConsumptionRuleIntegrityCandidate(1, 58, 1, "سعة الصدر", 1, 42, null, "kot_length + sleeve_length + 10", "Active")
        };

        var result = ConsumptionRuleIntegrityValidator.Validate(rules);

        Assert.True(result.OverlapCount > 0 || result.DuplicateCount > 0);
    }

    [Fact]
    public void ShouldDetectOverlappingRanges()
    {
        var rules = new[]
        {
            new ConsumptionRuleIntegrityCandidate(1, 58, 1, "سعة الصدر", 1, 40, 45, "kot_length + sleeve_length + 10", "Active"),
            new ConsumptionRuleIntegrityCandidate(1, 58, 1, "سعة الصدر", 1, 44, 48, "kot_length + sleeve_length + 12", "Active")
        };

        var result = ConsumptionRuleIntegrityValidator.Validate(rules);

        Assert.True(result.OverlapCount >= 1);
    }

    [Fact]
    public void ShouldDetectGapBetweenRanges()
    {
        var rules = new[]
        {
            new ConsumptionRuleIntegrityCandidate(1, 58, 1, "سعة الصدر", 1, 1, 42, "kot_length + sleeve_length + 10", "Active"),
            new ConsumptionRuleIntegrityCandidate(1, 58, 1, "سعة الصدر", 1, 45, 60, "kot_length + sleeve_length + 12", "Active")
        };

        var result = ConsumptionRuleIntegrityValidator.Validate(rules);

        Assert.True(result.GapCount >= 1);
    }

    [Fact]
    public void ShouldAcceptCompleteCoveringSet()
    {
        var rules = new[]
        {
            new ConsumptionRuleIntegrityCandidate(1, 58, 1, "سعة الصدر", 1, 1, 42, "kot_length + sleeve_length + 10", "Active"),
            new ConsumptionRuleIntegrityCandidate(1, 58, 1, "سعة الصدر", 2, 42, 45, "kot_length + sleeve_length + 12", "Active"),
            new ConsumptionRuleIntegrityCandidate(1, 58, 1, "سعة الصدر", 3, 45, null, "kot_length * 2 + 12", "Active")
        };

        var result = ConsumptionRuleIntegrityValidator.Validate(rules);

        Assert.True(result.OverlapCount == 0);
        Assert.True(result.DuplicateCount == 0);
        Assert.True(result.GapCount == 0);
    }
}
