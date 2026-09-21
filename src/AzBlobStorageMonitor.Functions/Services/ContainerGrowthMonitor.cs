using AzBlobStorageMonitor.Functions.Configuration;
using AzBlobStorageMonitor.Functions.Models;
using Microsoft.Extensions.Logging;

namespace AzBlobStorageMonitor.Functions.Services;

public sealed class ContainerGrowthMonitor(
    IContainerSizeReader sizeReader,
    IMonitorHistoryStore historyStore,
    INotificationSender notificationSender,
    GrowthTrendEvaluator trendEvaluator,
    MonitorOptions options,
    TimeProvider timeProvider,
    ILogger<ContainerGrowthMonitor> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var sizeBytes = await sizeReader.GetCurrentSizeBytesAsync(cancellationToken);
        await historyStore.SaveSampleAsync(
            new DailyContainerSize(today, sizeBytes),
            cancellationToken);

        var samples = await historyStore.GetLatestSamplesAsync(
            options.GrowthWindowDays + 1,
            cancellationToken);
        var isContinuousGrowth = trendEvaluator.IsContinuousGrowth(
            samples,
            options.GrowthWindowDays,
            options.MinimumDailyGrowthBytes);
        var alertIsActive = await historyStore.GetAlertActiveAsync(cancellationToken);

        if (!isContinuousGrowth)
        {
            if (alertIsActive)
            {
                await historyStore.SetAlertActiveAsync(
                    false,
                    today,
                    cancellationToken);
            }

            logger.LogInformation(
                "Container {ContainerName} has not met the continuous growth condition.",
                options.ContainerName);
            return;
        }

        if (alertIsActive)
        {
            logger.LogInformation(
                "Container {ContainerName} is still growing; the alert for this streak was already sent.",
                options.ContainerName);
            return;
        }

        var alert = new GrowthAlert(
            options.MonitorName,
            options.StorageAccountResourceId,
            options.ContainerName,
            options.Subscribers,
            samples);

        await notificationSender.SendAsync(alert, cancellationToken);
        await historyStore.SetAlertActiveAsync(true, today, cancellationToken);

        logger.LogWarning(
            "Sent growth alert for container {ContainerName}; it grew by {GrowthBytes} bytes over {GrowthDays} days.",
            options.ContainerName,
            alert.TotalGrowthBytes,
            options.GrowthWindowDays);
    }
}
