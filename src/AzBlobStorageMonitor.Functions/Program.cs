using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Storage.Blobs;
using AzBlobStorageMonitor.Functions.Configuration;
using AzBlobStorageMonitor.Functions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var options = MonitorOptions.Load(context.Configuration);

        services.AddSingleton(options);
        services.AddSingleton<TokenCredential>(_ =>
        {
            if (string.Equals(
                context.HostingEnvironment.EnvironmentName,
                "Development",
                StringComparison.OrdinalIgnoreCase))
            {
                return new DefaultAzureCredential();
            }

            // the Function App only has a user-assigned identity, so the client ID must be pinned explicitly
            var clientId = context.Configuration["AZURE_CLIENT_ID"]
                ?? throw new InvalidOperationException(
                    "Configuration 'AZURE_CLIENT_ID' is required to select the user-assigned managed identity.");
            return new ManagedIdentityCredential(
                ManagedIdentityId.FromUserAssignedClientId(clientId));
        });
        services.AddSingleton(sp =>
            new MetricsQueryClient(sp.GetRequiredService<TokenCredential>()));
        services.AddSingleton(sp =>
            new BlobServiceClient(
                options.BlobServiceUri!,
                sp.GetRequiredService<TokenCredential>())
                .GetBlobContainerClient(options.ContainerName));
        services.AddSingleton(sp =>
            new TableServiceClient(
                options.HistoryTableServiceUri,
                sp.GetRequiredService<TokenCredential>()));
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<HttpClient>();
        services.AddSingleton<AzureMonitorContainerSizeReader>();
        services.AddSingleton<BlobListingContainerSizeReader>();
        services.AddSingleton<IContainerSizeReader>(sp =>
            options.MeasurementSource switch
            {
                ContainerMeasurementSource.AzureMonitorMetrics =>
                    sp.GetRequiredService<AzureMonitorContainerSizeReader>(),
                ContainerMeasurementSource.BlobListing =>
                    sp.GetRequiredService<BlobListingContainerSizeReader>(),
                _ => throw new InvalidOperationException(
                    $"Unsupported measurement source '{options.MeasurementSource}'.")
            });
        services.AddSingleton<IMonitorHistoryStore, TableMonitorHistoryStore>();
        services.AddSingleton<INotificationSender, LogicAppNotificationSender>();
        services.AddSingleton<GrowthTrendEvaluator>();
        services.AddSingleton<ContainerGrowthMonitor>();
    })
    .Build();

await host.RunAsync();
