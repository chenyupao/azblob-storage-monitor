namespace AzBlobStorageMonitor.Functions.Models;

public sealed record DailyContainerSize(DateOnly Date, long SizeBytes);
