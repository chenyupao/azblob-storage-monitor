using AzBlobStorageMonitor.Functions.Models;

namespace AzBlobStorageMonitor.Functions.Services;

public interface IMonitorHistoryStore
{
    Task SaveSampleAsync(
        DailyContainerSize sample,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DailyContainerSize>> GetLatestSamplesAsync(
        int count,
        CancellationToken cancellationToken);

    Task<bool> GetAlertActiveAsync(CancellationToken cancellationToken);

    Task SetAlertActiveAsync(
        bool isActive,
        DateOnly evaluatedDate,
        CancellationToken cancellationToken);
}
