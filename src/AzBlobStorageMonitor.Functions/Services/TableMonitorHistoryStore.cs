using Azure;
using Azure.Data.Tables;
using AzBlobStorageMonitor.Functions.Configuration;
using AzBlobStorageMonitor.Functions.Models;

namespace AzBlobStorageMonitor.Functions.Services;

public sealed class TableMonitorHistoryStore : IMonitorHistoryStore
{
    private const string SampleRowKeyPrefix = "sample|";
    private const string StateRowKey = "state";

    private readonly TableClient tableClient;
    private readonly string partitionKey;

    public TableMonitorHistoryStore(
        TableServiceClient tableServiceClient,
        MonitorOptions options)
    {
        tableClient = tableServiceClient.GetTableClient(options.HistoryTableName);
        partitionKey = options.MonitorName;
    }

    public async Task SaveSampleAsync(
        DailyContainerSize sample,
        CancellationToken cancellationToken)
    {
        await EnsureTableExistsAsync(cancellationToken);

        var entity = new DailyContainerSizeEntity
        {
            PartitionKey = partitionKey,
            RowKey = $"{SampleRowKeyPrefix}{sample.Date:yyyyMMdd}",
            SampleDate = sample.Date.ToString("yyyy-MM-dd"),
            SizeBytes = sample.SizeBytes
        };

        await tableClient.UpsertEntityAsync(
            entity,
            TableUpdateMode.Replace,
            cancellationToken);
    }

    public async Task<IReadOnlyList<DailyContainerSize>> GetLatestSamplesAsync(
        int count,
        CancellationToken cancellationToken)
    {
        await EnsureTableExistsAsync(cancellationToken);

        var lowerBound = SampleRowKeyPrefix;
        var upperBound = "sample}";
        var filter = TableClient.CreateQueryFilter(
            $"PartitionKey eq {partitionKey} and RowKey ge {lowerBound} and RowKey lt {upperBound}");

        var samples = new List<DailyContainerSize>();
        await foreach (var entity in tableClient.QueryAsync<DailyContainerSizeEntity>(
            filter,
            cancellationToken: cancellationToken))
        {
            if (!DateOnly.TryParse(entity.SampleDate, out var date))
            {
                throw new InvalidOperationException(
                    $"History row '{entity.RowKey}' has an invalid SampleDate.");
            }

            samples.Add(new DailyContainerSize(date, entity.SizeBytes));
        }

        return samples
            .OrderByDescending(sample => sample.Date)
            .Take(count)
            .OrderBy(sample => sample.Date)
            .ToArray();
    }

    public async Task<bool> GetAlertActiveAsync(
        CancellationToken cancellationToken)
    {
        await EnsureTableExistsAsync(cancellationToken);

        var response = await tableClient.GetEntityIfExistsAsync<GrowthAlertStateEntity>(
            partitionKey,
            StateRowKey,
            cancellationToken: cancellationToken);

        return response.Value?.IsActive ?? false;
    }

    public async Task SetAlertActiveAsync(
        bool isActive,
        DateOnly evaluatedDate,
        CancellationToken cancellationToken)
    {
        await EnsureTableExistsAsync(cancellationToken);

        var entity = new GrowthAlertStateEntity
        {
            PartitionKey = partitionKey,
            RowKey = StateRowKey,
            IsActive = isActive,
            LastEvaluatedDate = evaluatedDate.ToString("yyyy-MM-dd")
        };

        await tableClient.UpsertEntityAsync(
            entity,
            TableUpdateMode.Replace,
            cancellationToken);
    }

    private async Task EnsureTableExistsAsync(CancellationToken cancellationToken)
    {
        await tableClient.CreateIfNotExistsAsync(cancellationToken);
    }

    private sealed class DailyContainerSizeEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;

        public string RowKey { get; set; } = string.Empty;

        public DateTimeOffset? Timestamp { get; set; }

        public ETag ETag { get; set; }

        public string SampleDate { get; set; } = string.Empty;

        public long SizeBytes { get; set; }
    }

    private sealed class GrowthAlertStateEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;

        public string RowKey { get; set; } = string.Empty;

        public DateTimeOffset? Timestamp { get; set; }

        public ETag ETag { get; set; }

        public bool IsActive { get; set; }

        public string LastEvaluatedDate { get; set; } = string.Empty;
    }
}
