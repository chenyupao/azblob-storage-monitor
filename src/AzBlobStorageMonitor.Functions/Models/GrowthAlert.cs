namespace AzBlobStorageMonitor.Functions.Models;

public sealed record GrowthAlert(
    string MonitorName,
    string StorageAccountResourceId,
    string ContainerName,
    IReadOnlyList<string> Subscribers,
    IReadOnlyList<DailyContainerSize> Samples)
{
    public long TotalGrowthBytes => Samples[^1].SizeBytes - Samples[0].SizeBytes;

    public double TotalGrowthPercent =>
        Samples[0].SizeBytes == 0
            ? 100
            : (double)TotalGrowthBytes / Samples[0].SizeBytes * 100;
}
