using AzBlobStorageMonitor.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzBlobStorageMonitor.Functions.Functions;

public sealed class CheckContainerGrowthFunction(
    ContainerGrowthMonitor monitor,
    ILogger<CheckContainerGrowthFunction> logger)
{
    [Function(nameof(CheckContainerGrowthFunction))]
    public async Task RunAsync(
        [TimerTrigger("%GrowthMonitorSchedule%")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Container growth check started at {StartedAt}; next run is {NextRun}.",
            DateTimeOffset.UtcNow,
            timer.ScheduleStatus?.Next);

        await monitor.RunAsync(cancellationToken);
    }
}
