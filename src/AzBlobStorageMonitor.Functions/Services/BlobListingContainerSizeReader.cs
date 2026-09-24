using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;

namespace AzBlobStorageMonitor.Functions.Services;

public sealed class BlobListingContainerSizeReader(
    BlobContainerClient containerClient,
    ILogger<BlobListingContainerSizeReader> logger) : IContainerSizeReader
{
    public async Task<long> GetCurrentSizeBytesAsync(
        CancellationToken cancellationToken)
    {
        long blobCount = 0;
        long totalBytes = 0;

        await foreach (var blob in containerClient
            .GetBlobsAsync(cancellationToken: cancellationToken))
        {
            blobCount = checked(blobCount + 1);
            totalBytes = checked(totalBytes + (blob.Properties.ContentLength ?? 0));
        }

        logger.LogInformation(
            "Container {ContainerName} contains {BlobCount} blobs using {SizeBytes} bytes.",
            containerClient.Name,
            blobCount,
            totalBytes);

        return totalBytes;
    }
}