using AzBlobStorageMonitor.Functions.Models;

namespace AzBlobStorageMonitor.Functions.Services;

public interface INotificationSender
{
    Task SendAsync(GrowthAlert alert, CancellationToken cancellationToken);
}
