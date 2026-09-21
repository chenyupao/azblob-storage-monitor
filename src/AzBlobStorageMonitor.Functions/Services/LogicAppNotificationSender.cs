using System.Net.Http.Json;
using AzBlobStorageMonitor.Functions.Configuration;
using AzBlobStorageMonitor.Functions.Models;

namespace AzBlobStorageMonitor.Functions.Services;

public sealed class LogicAppNotificationSender(
    HttpClient httpClient,
    MonitorOptions options) : INotificationSender
{
    public async Task SendAsync(
        GrowthAlert alert,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            alert.MonitorName,
            alert.StorageAccountResourceId,
            alert.ContainerName,
            alert.Subscribers,
            Subject = $"Blob container growth alert: {alert.ContainerName}",
            Summary = $"Container grew for {alert.Samples.Count - 1} consecutive days.",
            alert.TotalGrowthBytes,
            alert.TotalGrowthPercent,
            Samples = alert.Samples.Select(sample => new
            {
                Date = sample.Date.ToString("yyyy-MM-dd"),
                sample.SizeBytes
            })
        };

        using var response = await httpClient.PostAsJsonAsync(
            options.LogicAppWebhookUrl,
            payload,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
