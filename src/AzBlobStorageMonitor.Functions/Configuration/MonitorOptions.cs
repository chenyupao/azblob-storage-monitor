using Microsoft.Extensions.Configuration;

namespace AzBlobStorageMonitor.Functions.Configuration;

public enum ContainerMeasurementSource
{
    AzureMonitorMetrics,
    BlobListing
}

public sealed class MonitorOptions
{
    private const string SectionName = "GrowthMonitor";

    public required string MonitorName { get; init; }

    public required string StorageAccountResourceId { get; init; }

    public required string ContainerName { get; init; }

    public required Uri HistoryTableServiceUri { get; init; }

    public required string HistoryTableName { get; init; }

    public required Uri LogicAppWebhookUrl { get; init; }

    public required IReadOnlyList<string> Subscribers { get; init; }

    public required int GrowthWindowDays { get; init; }

    public required long MinimumDailyGrowthBytes { get; init; }

    public required int MetricLookbackHours { get; init; }

    public ContainerMeasurementSource MeasurementSource { get; init; }

    public Uri? BlobServiceUri { get; init; }

    public static MonitorOptions Load(IConfiguration configuration)
    {
        string Required(string name)
        {
            return configuration[$"{SectionName}:{name}"]
                ?? throw new InvalidOperationException(
                    $"Configuration '{SectionName}:{name}' is required.");
        }

        static int PositiveInt(string value, string name)
        {
            if (!int.TryParse(value, out var result) || result <= 0)
            {
                throw new InvalidOperationException(
                    $"Configuration '{SectionName}:{name}' must be a positive integer.");
            }

            return result;
        }

        static long NonNegativeLong(string value, string name)
        {
            if (!long.TryParse(value, out var result) || result < 0)
            {
                throw new InvalidOperationException(
                    $"Configuration '{SectionName}:{name}' must be zero or greater.");
            }

            return result;
        }

        var subscribers = configuration
            .GetSection($"{SectionName}:Subscribers")
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();

        if (subscribers.Length == 0)
        {
            subscribers = (configuration[$"{SectionName}:SubscribersCsv"] ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        if (subscribers.Length == 0)
        {
            throw new InvalidOperationException(
                $"At least one '{SectionName}:Subscribers' entry or " +
                $"'{SectionName}:SubscribersCsv' value is required.");
        }

        var measurementSourceValue = configuration[
            $"{SectionName}:{nameof(MeasurementSource)}"]
            ?? nameof(ContainerMeasurementSource.AzureMonitorMetrics);
        if (!Enum.TryParse<ContainerMeasurementSource>(
                measurementSourceValue,
                ignoreCase: true,
                out var measurementSource)
            || !Enum.IsDefined(measurementSource))
        {
            throw new InvalidOperationException(
                $"Configuration '{SectionName}:{nameof(MeasurementSource)}' " +
                $"must be '{nameof(ContainerMeasurementSource.AzureMonitorMetrics)}' " +
                $"or '{nameof(ContainerMeasurementSource.BlobListing)}'.");
        }

        Uri? blobServiceUri = null;
        var blobServiceUriValue = configuration[
            $"{SectionName}:{nameof(BlobServiceUri)}"];
        if (!string.IsNullOrWhiteSpace(blobServiceUriValue))
        {
            blobServiceUri = new Uri(blobServiceUriValue);
        }
        else if (measurementSource == ContainerMeasurementSource.BlobListing)
        {
            throw new InvalidOperationException(
                $"Configuration '{SectionName}:{nameof(BlobServiceUri)}' is " +
                $"required when '{SectionName}:{nameof(MeasurementSource)}' is " +
                $"'{nameof(ContainerMeasurementSource.BlobListing)}'.");
        }

        return new MonitorOptions
        {
            MonitorName = Required(nameof(MonitorName)),
            StorageAccountResourceId = Required(nameof(StorageAccountResourceId)),
            ContainerName = Required(nameof(ContainerName)),
            HistoryTableServiceUri = new Uri(Required(nameof(HistoryTableServiceUri))),
            HistoryTableName = configuration[$"{SectionName}:{nameof(HistoryTableName)}"]
                ?? "BlobGrowthHistory",
            LogicAppWebhookUrl = new Uri(Required(nameof(LogicAppWebhookUrl))),
            Subscribers = subscribers,
            GrowthWindowDays = PositiveInt(
                configuration[$"{SectionName}:{nameof(GrowthWindowDays)}"]
                    ?? configuration[$"{SectionName}:ConsecutiveGrowthDays"]
                    ?? "3",
                nameof(GrowthWindowDays)),
            MinimumDailyGrowthBytes = NonNegativeLong(
                configuration[$"{SectionName}:{nameof(MinimumDailyGrowthBytes)}"] ?? "0",
                nameof(MinimumDailyGrowthBytes)),
            MetricLookbackHours = PositiveInt(
                configuration[$"{SectionName}:{nameof(MetricLookbackHours)}"] ?? "24",
                nameof(MetricLookbackHours)),
            MeasurementSource = measurementSource,
            BlobServiceUri = blobServiceUri
        };
    }
}
