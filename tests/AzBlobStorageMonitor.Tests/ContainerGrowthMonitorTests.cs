using AzBlobStorageMonitor.Functions.Configuration;
using AzBlobStorageMonitor.Functions.Models;
using AzBlobStorageMonitor.Functions.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AzBlobStorageMonitor.Tests;

public sealed class ContainerGrowthMonitorTests
{
    [Fact]
    public async Task SendsOneAlertForAnUninterruptedGrowthStreak()
    {
        var history = new FakeHistoryStore(
            Samples(100, 110, 120),
            alertActive: false);
        var notifier = new FakeNotificationSender();
        var monitor = CreateMonitor(130, history, notifier);

        await monitor.RunAsync(CancellationToken.None);
        await monitor.RunAsync(CancellationToken.None);

        Assert.Single(notifier.Alerts);
        Assert.True(history.AlertActive);
    }

    [Fact]
    public async Task ResetsAlertStateWhenGrowthStops()
    {
        var history = new FakeHistoryStore(
            Samples(100, 110, 120),
            alertActive: true);
        var notifier = new FakeNotificationSender();
        var monitor = CreateMonitor(120, history, notifier);

        await monitor.RunAsync(CancellationToken.None);

        Assert.False(history.AlertActive);
        Assert.Empty(notifier.Alerts);
    }

    [Fact]
    public async Task DoesNotMarkAlertActiveWhenNotificationFails()
    {
        var history = new FakeHistoryStore(
            Samples(100, 110, 120),
            alertActive: false);
        var notifier = new FakeNotificationSender(shouldFail: true);
        var monitor = CreateMonitor(130, history, notifier);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => monitor.RunAsync(CancellationToken.None));

        Assert.False(history.AlertActive);
    }

    private static ContainerGrowthMonitor CreateMonitor(
        long currentSize,
        FakeHistoryStore history,
        FakeNotificationSender notifier)
    {
        var options = new MonitorOptions
        {
            MonitorName = "test-monitor",
            StorageAccountResourceId = "/subscriptions/test/resourceGroups/test/providers/Microsoft.Storage/storageAccounts/test",
            ContainerName = "archive",
            HistoryTableServiceUri = new Uri("https://state.table.core.windows.net"),
            HistoryTableName = "BlobGrowthHistory",
            LogicAppWebhookUrl = new Uri("https://example.test/webhook"),
            Subscribers = ["operations@example.test"],
            GrowthWindowDays = 3,
            MinimumDailyGrowthBytes = 0,
            MetricLookbackHours = 24
        };

        return new ContainerGrowthMonitor(
            new FakeSizeReader(currentSize),
            history,
            notifier,
            new GrowthTrendEvaluator(),
            options,
            new FixedTimeProvider(
                new DateTimeOffset(2026, 9, 21, 0, 15, 0, TimeSpan.Zero)),
            NullLogger<ContainerGrowthMonitor>.Instance);
    }

    private static DailyContainerSize[] Samples(params long[] sizes)
    {
        var start = new DateOnly(2026, 9, 18);
        return sizes
            .Select((size, index) =>
                new DailyContainerSize(start.AddDays(index), size))
            .ToArray();
    }

    private sealed class FakeSizeReader(long sizeBytes) : IContainerSizeReader
    {
        public Task<long> GetCurrentSizeBytesAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(sizeBytes);
        }
    }

    private sealed class FakeHistoryStore(
        IEnumerable<DailyContainerSize> initialSamples,
        bool alertActive) : IMonitorHistoryStore
    {
        private readonly List<DailyContainerSize> samples = [.. initialSamples];

        public bool AlertActive { get; private set; } = alertActive;

        public Task SaveSampleAsync(
            DailyContainerSize sample,
            CancellationToken cancellationToken)
        {
            samples.RemoveAll(existing => existing.Date == sample.Date);
            samples.Add(sample);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DailyContainerSize>> GetLatestSamplesAsync(
            int count,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<DailyContainerSize> result = samples
                .OrderByDescending(sample => sample.Date)
                .Take(count)
                .OrderBy(sample => sample.Date)
                .ToArray();
            return Task.FromResult(result);
        }

        public Task<bool> GetAlertActiveAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(AlertActive);
        }

        public Task SetAlertActiveAsync(
            bool isActive,
            DateOnly evaluatedDate,
            CancellationToken cancellationToken)
        {
            AlertActive = isActive;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotificationSender(bool shouldFail = false)
        : INotificationSender
    {
        public List<GrowthAlert> Alerts { get; } = [];

        public Task SendAsync(
            GrowthAlert alert,
            CancellationToken cancellationToken)
        {
            if (shouldFail)
            {
                throw new HttpRequestException("Simulated notification failure.");
            }

            Alerts.Add(alert);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
