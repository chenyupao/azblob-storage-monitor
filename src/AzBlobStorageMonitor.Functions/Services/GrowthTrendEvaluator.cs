using AzBlobStorageMonitor.Functions.Models;

namespace AzBlobStorageMonitor.Functions.Services;

public sealed class GrowthTrendEvaluator
{
    public bool IsContinuousGrowth(
        IReadOnlyList<DailyContainerSize> samples,
        int growthWindowDays,
        long minimumDailyGrowthBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(growthWindowDays, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumDailyGrowthBytes);

        var requiredSampleCount = growthWindowDays + 1;
        if (samples.Count != requiredSampleCount)
        {
            return false;
        }

        for (var index = 1; index < samples.Count; index++)
        {
            if (samples[index].Date != samples[index - 1].Date.AddDays(1))
            {
                return false;
            }

            var growth = samples[index].SizeBytes - samples[index - 1].SizeBytes;
            if (growth <= 0 || growth < minimumDailyGrowthBytes)
            {
                return false;
            }
        }

        return true;
    }
}
