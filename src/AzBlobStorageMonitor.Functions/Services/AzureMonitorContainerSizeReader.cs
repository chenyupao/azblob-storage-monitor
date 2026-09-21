using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using AzBlobStorageMonitor.Functions.Configuration;
using Microsoft.Extensions.Logging;

namespace AzBlobStorageMonitor.Functions.Services;

public sealed class AzureMonitorContainerSizeReader(
    MetricsQueryClient metricsClient,
    MonitorOptions options,
    ILogger<AzureMonitorContainerSizeReader> logger) : IContainerSizeReader
{
    public async Task<long> GetCurrentSizeBytesAsync(
        CancellationToken cancellationToken)
    {
        var escapedContainerName = options.ContainerName.Replace("'", "''");
        var queryOptions = new MetricsQueryOptions
        {
            TimeRange = new QueryTimeRange(
                TimeSpan.FromHours(options.MetricLookbackHours)),
            Granularity = TimeSpan.FromHours(1),
            Filter = $"ContainerName eq '{escapedContainerName}'"
        };
        queryOptions.Aggregations.Add(MetricAggregationType.Average);

        var response = await metricsClient.QueryResourceAsync(
            options.StorageAccountResourceId,
            ["ContainerUsedSize"],
            queryOptions,
            cancellationToken);

        var latestValues = response.Value.Metrics
            .SelectMany(metric => metric.TimeSeries)
            .Select(series => series.Values
                .Where(value => value.Average.HasValue)
                .OrderByDescending(value => value.TimeStamp)
                .FirstOrDefault())
            .Where(value => value?.Average.HasValue == true)
            .Select(value => value!.Average!.Value)
            .ToArray();

        if (latestValues.Length == 0)
        {
            throw new InvalidOperationException(
                $"Azure Monitor returned no ContainerUsedSize data for container " +
                $"'{options.ContainerName}' during the last " +
                $"{options.MetricLookbackHours} hours.");
        }

        var totalBytes = checked((long)Math.Round(latestValues.Sum()));
        logger.LogInformation(
            "Container {ContainerName} currently uses {SizeBytes} bytes across {SeriesCount} metric series.",
            options.ContainerName,
            totalBytes,
            latestValues.Length);

        return totalBytes;
    }
}
