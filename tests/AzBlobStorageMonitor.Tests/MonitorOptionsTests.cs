using AzBlobStorageMonitor.Functions.Configuration;
using Microsoft.Extensions.Configuration;

namespace AzBlobStorageMonitor.Tests;

public sealed class MonitorOptionsTests
{
    [Fact]
    public void LoadsConfiguredGrowthWindow()
    {
        var options = MonitorOptions.Load(CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["GrowthMonitor:GrowthWindowDays"] = "5"
            }));

        Assert.Equal(5, options.GrowthWindowDays);
    }

    [Fact]
    public void DefaultsGrowthWindowToThreeDays()
    {
        var options = MonitorOptions.Load(CreateConfiguration());

        Assert.Equal(3, options.GrowthWindowDays);
    }

    [Fact]
    public void SupportsFormerConsecutiveGrowthDaysSetting()
    {
        var options = MonitorOptions.Load(CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["GrowthMonitor:ConsecutiveGrowthDays"] = "7"
            }));

        Assert.Equal(7, options.GrowthWindowDays);
    }

    [Fact]
    public void RejectsNonPositiveGrowthWindow()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["GrowthMonitor:GrowthWindowDays"] = "0"
            });

        var exception = Assert.Throws<InvalidOperationException>(
            () => MonitorOptions.Load(configuration));

        Assert.Contains("GrowthWindowDays", exception.Message);
    }

    [Fact]
    public void LoadsCommaSeparatedSubscribers()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["GrowthMonitor:Subscribers:0"] = null,
                ["GrowthMonitor:SubscribersCsv"] =
                    "first@example.test, second@example.test"
            });

        var options = MonitorOptions.Load(configuration);

        Assert.Equal(
            ["first@example.test", "second@example.test"],
            options.Subscribers);
    }

    [Fact]
    public void DefaultsMeasurementSourceToAzureMonitorMetrics()
    {
        var options = MonitorOptions.Load(CreateConfiguration());

        Assert.Equal(
            ContainerMeasurementSource.AzureMonitorMetrics,
            options.MeasurementSource);
    }

    [Fact]
    public void LoadsBlobListingMeasurementSource()
    {
        var options = MonitorOptions.Load(CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["GrowthMonitor:MeasurementSource"] = "BlobListing",
                ["GrowthMonitor:BlobServiceUri"] = "https://test.blob.core.windows.net"
            }));

        Assert.Equal(ContainerMeasurementSource.BlobListing, options.MeasurementSource);
        Assert.Equal(
            new Uri("https://test.blob.core.windows.net"),
            options.BlobServiceUri);
    }

    [Fact]
    public void RequiresBlobServiceUriForBlobListing()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["GrowthMonitor:MeasurementSource"] = "BlobListing"
            });

        var exception = Assert.Throws<InvalidOperationException>(
            () => MonitorOptions.Load(configuration));

        Assert.Contains("BlobServiceUri", exception.Message);
    }

    [Fact]
    public void RejectsUnknownMeasurementSource()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["GrowthMonitor:MeasurementSource"] = "Unknown"
            });

        var exception = Assert.Throws<InvalidOperationException>(
            () => MonitorOptions.Load(configuration));

        Assert.Contains("MeasurementSource", exception.Message);
    }

    private static IConfiguration CreateConfiguration(
        IDictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["GrowthMonitor:MonitorName"] = "test-monitor",
            ["GrowthMonitor:StorageAccountResourceId"] = "/subscriptions/test/resourceGroups/test/providers/Microsoft.Storage/storageAccounts/test",
            ["GrowthMonitor:ContainerName"] = "archive",
            ["GrowthMonitor:HistoryTableServiceUri"] = "https://state.table.core.windows.net",
            ["GrowthMonitor:LogicAppWebhookUrl"] = "https://example.test/webhook",
            ["GrowthMonitor:Subscribers:0"] = "operations@example.test"
        };

        if (overrides is not null)
        {
            foreach (var item in overrides)
            {
                values[item.Key] = item.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
