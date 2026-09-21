using AzBlobStorageMonitor.Functions.Models;
using AzBlobStorageMonitor.Functions.Services;

namespace AzBlobStorageMonitor.Tests;

public sealed class GrowthTrendEvaluatorTests
{
    private readonly GrowthTrendEvaluator evaluator = new();

    [Fact]
    public void ReturnsTrueForThreeConsecutiveDailyIncreases()
    {
        var samples = Samples(100, 110, 130, 131);

        var result = evaluator.IsContinuousGrowth(samples, 3, 0);

        Assert.True(result);
    }

    [Fact]
    public void ReturnsFalseWhenOneDayDoesNotGrow()
    {
        var samples = Samples(100, 110, 110, 130);

        var result = evaluator.IsContinuousGrowth(samples, 3, 0);

        Assert.False(result);
    }

    [Fact]
    public void ReturnsFalseWhenARequiredCalendarDayIsMissing()
    {
        var samples = new[]
        {
            new DailyContainerSize(new DateOnly(2026, 9, 17), 100),
            new DailyContainerSize(new DateOnly(2026, 9, 18), 110),
            new DailyContainerSize(new DateOnly(2026, 9, 20), 120),
            new DailyContainerSize(new DateOnly(2026, 9, 21), 130)
        };

        var result = evaluator.IsContinuousGrowth(samples, 3, 0);

        Assert.False(result);
    }

    [Fact]
    public void ReturnsFalseWhenGrowthIsBelowMinimum()
    {
        var samples = Samples(100, 110, 119, 130);

        var result = evaluator.IsContinuousGrowth(samples, 3, 10);

        Assert.False(result);
    }

    [Fact]
    public void ReturnsFalseUntilFourSamplesExistForThreeGrowthDays()
    {
        var samples = Samples(100, 110, 120);

        var result = evaluator.IsContinuousGrowth(samples, 3, 0);

        Assert.False(result);
    }

    private static DailyContainerSize[] Samples(params long[] sizes)
    {
        var start = new DateOnly(2026, 9, 18);
        return sizes
            .Select((size, index) =>
                new DailyContainerSize(start.AddDays(index), size))
            .ToArray();
    }
}
