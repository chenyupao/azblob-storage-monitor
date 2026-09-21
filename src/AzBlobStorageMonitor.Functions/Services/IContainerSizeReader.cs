namespace AzBlobStorageMonitor.Functions.Services;

public interface IContainerSizeReader
{
    Task<long> GetCurrentSizeBytesAsync(CancellationToken cancellationToken);
}
