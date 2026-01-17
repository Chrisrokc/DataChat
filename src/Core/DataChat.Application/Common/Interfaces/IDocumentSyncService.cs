namespace DataChat.Application.Common.Interfaces;

public interface IDocumentSyncService
{
    Task<SyncJob> StartSyncAsync(Guid dataSourceId, CancellationToken cancellationToken = default);
    Task<SyncJob?> GetSyncStatusAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SyncJob>> GetActiveSyncJobsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SyncJob>> GetRecentSyncJobsAsync(int count = 10, CancellationToken cancellationToken = default);
    Task CancelSyncAsync(Guid jobId, CancellationToken cancellationToken = default);
}

public class SyncJob
{
    public Guid Id { get; set; }
    public Guid DataSourceId { get; set; }
    public string DataSourceName { get; set; } = string.Empty;
    public SyncJobStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int SuccessfulFiles { get; set; }
    public int FailedFiles { get; set; }
    public string? CurrentFile { get; set; }
    public string? ErrorMessage { get; set; }
    public List<SyncFileResult> FileResults { get; set; } = new();

    public double ProgressPercentage => TotalFiles > 0 ? (ProcessedFiles * 100.0 / TotalFiles) : 0;
}

public class SyncFileResult
{
    public string FileName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int ChunksCreated { get; set; }
}

public enum SyncJobStatus
{
    Queued,
    Scanning,
    Processing,
    Completed,
    Failed,
    Cancelled
}
