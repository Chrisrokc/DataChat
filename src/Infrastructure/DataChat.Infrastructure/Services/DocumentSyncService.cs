using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DataChat.Application.Common.Interfaces;
using DataChat.Domain.Entities;
using DataChat.Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataChat.Infrastructure.Services;

public class DocumentSyncService : IDocumentSyncService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentSyncService> _logger;
    private static readonly ConcurrentDictionary<Guid, SyncJob> _activeJobs = new();
    private static readonly ConcurrentDictionary<Guid, CancellationTokenSource> _jobCancellations = new();
    private static readonly List<SyncJob> _completedJobs = new();
    private static readonly object _completedJobsLock = new();

    public DocumentSyncService(
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<SyncJob> StartSyncAsync(Guid dataSourceId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var dataSource = await dbContext.DataSources
            .Include(ds => ds.FileSystemDataSource)
            .Include(ds => ds.SqlViewDataSource)
                .ThenInclude(s => s!.DatabaseConnection)
            .FirstOrDefaultAsync(ds => ds.Id == dataSourceId, cancellationToken);

        if (dataSource == null)
            throw new ArgumentException($"Data source {dataSourceId} not found");

        if (dataSource.Type != DataSourceType.FileSystem && dataSource.Type != DataSourceType.SqlView)
            throw new NotSupportedException("Only file system and SQL view data sources can be synced");

        // Check if already syncing
        if (_activeJobs.Values.Any(j => j.DataSourceId == dataSourceId &&
            j.Status != SyncJobStatus.Completed &&
            j.Status != SyncJobStatus.Failed &&
            j.Status != SyncJobStatus.Cancelled))
        {
            throw new InvalidOperationException("A sync job is already running for this data source");
        }

        var job = new SyncJob
        {
            Id = Guid.NewGuid(),
            DataSourceId = dataSourceId,
            DataSourceName = dataSource.Name,
            Status = SyncJobStatus.Queued,
            StartedAt = DateTime.UtcNow
        };

        var cts = new CancellationTokenSource();
        _activeJobs[job.Id] = job;
        _jobCancellations[job.Id] = cts;

        // Update data source status
        if (dataSource.FileSystemDataSource != null)
        {
            dataSource.FileSystemDataSource.SyncStatus = SyncStatus.InProgress;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (dataSource.SqlViewDataSource != null)
        {
            dataSource.SqlViewDataSource.SyncStatus = SyncStatus.InProgress;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Start background processing
        _ = Task.Run(() => ProcessSyncJobAsync(job.Id, cts.Token), cts.Token);

        _logger.LogInformation("Started sync job {JobId} for data source {DataSourceName}", job.Id, dataSource.Name);

        return job;
    }

    private async Task ProcessSyncJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        if (!_activeJobs.TryGetValue(jobId, out var job))
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var parserFactory = scope.ServiceProvider.GetRequiredService<IDocumentParserFactory>();
            var chunkingStrategy = scope.ServiceProvider.GetRequiredService<IChunkingStrategy>();
            var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
            var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();
            var connectionService = scope.ServiceProvider.GetRequiredService<IDatabaseConnectionService>();

            var dataSource = await dbContext.DataSources
                .Include(ds => ds.FileSystemDataSource)
                .Include(ds => ds.SqlViewDataSource)
                    .ThenInclude(s => s!.DatabaseConnection)
                .Include(ds => ds.Documents)
                .FirstOrDefaultAsync(ds => ds.Id == job.DataSourceId, cancellationToken);

            if (dataSource == null)
            {
                job.Status = SyncJobStatus.Failed;
                job.ErrorMessage = "Data source not found";
                return;
            }

            // Route to appropriate sync method based on type
            if (dataSource.Type == DataSourceType.SqlView && dataSource.SqlViewDataSource != null)
            {
                await ProcessSqlViewSyncAsync(job, dataSource, dbContext, embeddingService, vectorStore, connectionService, cancellationToken);
                return;
            }

            if (dataSource.FileSystemDataSource == null)
            {
                job.Status = SyncJobStatus.Failed;
                job.ErrorMessage = "Data source not found or not a file system source";
                return;
            }

            var fsSource = dataSource.FileSystemDataSource;
            var folderPath = fsSource.FolderPath;

            if (!Directory.Exists(folderPath))
            {
                job.Status = SyncJobStatus.Failed;
                job.ErrorMessage = $"Folder not found: {folderPath}";
                return;
            }

            // Scanning phase
            job.Status = SyncJobStatus.Scanning;
            _logger.LogInformation("Scanning folder: {FolderPath}", folderPath);

            var searchOption = fsSource.IncludeSubfolders
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var patterns = (fsSource.FilePatterns ?? "*.pdf;*.docx;*.txt")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var files = new List<string>();
            foreach (var pattern in patterns)
            {
                try
                {
                    files.AddRange(Directory.GetFiles(folderPath, pattern, searchOption));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error scanning with pattern {Pattern}", pattern);
                }
            }

            files = files.Distinct().ToList();
            job.TotalFiles = files.Count;
            _logger.LogInformation("Found {FileCount} files to process", files.Count);

            if (files.Count == 0)
            {
                job.Status = SyncJobStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;
                fsSource.SyncStatus = SyncStatus.Completed;
                fsSource.LastSyncAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            // Processing phase
            job.Status = SyncJobStatus.Processing;

            foreach (var filePath in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    job.Status = SyncJobStatus.Cancelled;
                    break;
                }

                var fileName = Path.GetFileName(filePath);
                job.CurrentFile = fileName;

                var fileResult = new SyncFileResult { FileName = fileName };

                try
                {
                    await ProcessFileAsync(
                        filePath,
                        dataSource,
                        dbContext,
                        parserFactory,
                        chunkingStrategy,
                        embeddingService,
                        vectorStore,
                        fileResult,
                        cancellationToken);

                    fileResult.Success = true;
                    job.SuccessfulFiles++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing file: {FilePath}", filePath);
                    fileResult.Success = false;
                    fileResult.ErrorMessage = ex.Message;
                    job.FailedFiles++;
                }

                job.FileResults.Add(fileResult);
                job.ProcessedFiles++;
            }

            // Update completion status
            if (job.Status != SyncJobStatus.Cancelled)
            {
                job.Status = job.FailedFiles > 0 && job.SuccessfulFiles == 0
                    ? SyncJobStatus.Failed
                    : SyncJobStatus.Completed;
            }

            job.CompletedAt = DateTime.UtcNow;
            job.CurrentFile = null;

            // Update data source
            fsSource.SyncStatus = job.Status == SyncJobStatus.Completed
                ? SyncStatus.Completed
                : (job.Status == SyncJobStatus.Failed ? SyncStatus.Failed : SyncStatus.Pending);
            fsSource.LastSyncAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Sync job {JobId} completed. Processed: {Processed}, Successful: {Success}, Failed: {Failed}",
                jobId, job.ProcessedFiles, job.SuccessfulFiles, job.FailedFiles);
        }
        catch (OperationCanceledException)
        {
            job.Status = SyncJobStatus.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Sync job {JobId} was cancelled", jobId);
        }
        catch (Exception ex)
        {
            job.Status = SyncJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
            _logger.LogError(ex, "Sync job {JobId} failed with error", jobId);
        }
        finally
        {
            // Move to completed jobs
            if (_activeJobs.TryRemove(jobId, out var completedJob))
            {
                lock (_completedJobsLock)
                {
                    _completedJobs.Insert(0, completedJob);
                    // Keep only last 50 completed jobs
                    while (_completedJobs.Count > 50)
                        _completedJobs.RemoveAt(_completedJobs.Count - 1);
                }
            }
            _jobCancellations.TryRemove(jobId, out _);
        }
    }

    private async Task ProcessFileAsync(
        string filePath,
        DataSource dataSource,
        IApplicationDbContext dbContext,
        IDocumentParserFactory parserFactory,
        IChunkingStrategy chunkingStrategy,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        SyncFileResult result,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(filePath);
        var fileInfo = new FileInfo(filePath);
        var fileHash = await ComputeFileHashAsync(filePath, cancellationToken);

        // Check if document already exists and is unchanged
        var existingDoc = await dbContext.Documents
            .FirstOrDefaultAsync(d => d.DataSourceId == dataSource.Id && d.FilePath == filePath, cancellationToken);

        if (existingDoc != null && existingDoc.FileHash == fileHash && existingDoc.Status == DocumentStatus.Indexed)
        {
            _logger.LogDebug("Skipping unchanged file: {FileName}", fileName);
            result.ChunksCreated = 0;
            return;
        }

        // If document exists but changed, delete old chunks and embeddings
        if (existingDoc != null)
        {
            await vectorStore.DeleteByDocumentIdAsync(existingDoc.Id, cancellationToken);
            dbContext.DocumentChunks.RemoveRange(
                await dbContext.DocumentChunks.Where(c => c.DocumentId == existingDoc.Id).ToListAsync(cancellationToken));
            dbContext.Documents.Remove(existingDoc);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Create new document record
        var document = new Document
        {
            Id = Guid.NewGuid(),
            DataSourceId = dataSource.Id,
            FileName = fileName,
            FilePath = filePath,
            FileHash = fileHash,
            FileSize = fileInfo.Length,
            MimeType = GetMimeType(filePath),
            Status = DocumentStatus.Processing,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            // Parse document
            var parser = parserFactory.GetParser(filePath);
            var parsed = await parser.ParseAsync(filePath, cancellationToken);

            if (string.IsNullOrWhiteSpace(parsed.Content))
            {
                document.Status = DocumentStatus.Indexed;
                document.ProcessedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            // Chunk the content
            var textChunks = chunkingStrategy.ChunkDocument(parsed).ToList();
            _logger.LogDebug("Created {ChunkCount} chunks for {FileName}", textChunks.Count, fileName);

            // Process chunks
            var chunkEntities = new List<DocumentChunk>();
            for (int i = 0; i < textChunks.Count; i++)
            {
                var chunkEntity = new DocumentChunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    Content = textChunks[i].Content,
                    ChunkIndex = i,
                    CreatedAt = DateTime.UtcNow
                };
                chunkEntities.Add(chunkEntity);
                dbContext.DocumentChunks.Add(chunkEntity);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            // Generate embeddings and store in vector DB
            var chunkTexts = chunkEntities.Select(c => c.Content).ToList();
            var embeddings = (await embeddingService.GenerateEmbeddingsAsync(chunkTexts, cancellationToken)).ToList();

            for (int i = 0; i < chunkEntities.Count; i++)
            {
                await vectorStore.StoreEmbeddingAsync(chunkEntities[i].Id, embeddings[i], cancellationToken);
            }

            document.Status = DocumentStatus.Indexed;
            document.ProcessedAt = DateTime.UtcNow;
            result.ChunksCreated = textChunks.Count;

            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully indexed {FileName} with {ChunkCount} chunks", fileName, textChunks.Count);
        }
        catch (Exception ex)
        {
            document.Status = DocumentStatus.Failed;
            document.ErrorMessage = ex.Message;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static string? GetMimeType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };
    }

    #region SQL View Sync

    private async Task ProcessSqlViewSyncAsync(
        SyncJob job,
        DataSource dataSource,
        IApplicationDbContext dbContext,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        IDatabaseConnectionService connectionService,
        CancellationToken cancellationToken)
    {
        var sqlSource = dataSource.SqlViewDataSource!;
        var fullViewName = $"[{sqlSource.SchemaName}].[{sqlSource.ViewName}]";

        _logger.LogInformation("Starting SQL View sync for {ViewName}", fullViewName);

        try
        {
            // Validate connection exists
            if (sqlSource.DatabaseConnectionId == null || sqlSource.DatabaseConnection == null)
            {
                job.Status = SyncJobStatus.Failed;
                job.ErrorMessage = "No database connection configured for this SQL View data source";
                sqlSource.SyncStatus = SyncStatus.Failed;
                sqlSource.LastSyncError = job.ErrorMessage;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            // Build connection string
            var connectionString = connectionService.BuildConnectionString(sqlSource.DatabaseConnection);

            // Scanning phase - count rows
            job.Status = SyncJobStatus.Scanning;
            _logger.LogInformation("Scanning SQL View: {ViewName}", fullViewName);

            int rowCount;
            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(cancellationToken);

                // Get row count
                await using var countCmd = new SqlCommand($"SELECT COUNT(*) FROM {fullViewName}", connection);
                countCmd.CommandTimeout = sqlSource.DatabaseConnection.ConnectionTimeout;
                rowCount = (int)await countCmd.ExecuteScalarAsync(cancellationToken);
            }

            var maxRows = sqlSource.MaxRowsReturned > 0 ? sqlSource.MaxRowsReturned : 1000;
            var rowsToProcess = Math.Min(rowCount, maxRows);
            job.TotalFiles = rowsToProcess; // Repurpose TotalFiles for row count

            _logger.LogInformation("Found {RowCount} rows in view (processing max {MaxRows})", rowCount, rowsToProcess);

            if (rowsToProcess == 0)
            {
                job.Status = SyncJobStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;
                sqlSource.SyncStatus = SyncStatus.Completed;
                sqlSource.LastSyncAt = DateTime.UtcNow;
                sqlSource.LastSyncRowCount = 0;
                sqlSource.LastSyncError = null;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            // Delete existing documents and chunks for this data source (full resync)
            var existingDocs = await dbContext.Documents
                .Where(d => d.DataSourceId == dataSource.Id)
                .ToListAsync(cancellationToken);

            foreach (var doc in existingDocs)
            {
                await vectorStore.DeleteByDocumentIdAsync(doc.Id, cancellationToken);
                dbContext.DocumentChunks.RemoveRange(
                    await dbContext.DocumentChunks.Where(c => c.DocumentId == doc.Id).ToListAsync(cancellationToken));
            }
            dbContext.Documents.RemoveRange(existingDocs);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Processing phase
            job.Status = SyncJobStatus.Processing;

            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(cancellationToken);

                // Fetch rows with limit
                var query = $"SELECT TOP ({maxRows}) * FROM {fullViewName}";
                await using var cmd = new SqlCommand(query, connection);
                cmd.CommandTimeout = sqlSource.DatabaseConnection.ConnectionTimeout;

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

                // Get column information
                var columnNames = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columnNames.Add(reader.GetName(i));
                }

                // Store column metadata for AI context
                sqlSource.ColumnMetadata = JsonSerializer.Serialize(columnNames);

                // Process rows in batches
                var batchSize = 50; // Process 50 rows at a time for embedding
                var rowBatch = new List<(Guid docId, Guid chunkId, string content)>();
                var rowIndex = 0;

                while (await reader.ReadAsync(cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        job.Status = SyncJobStatus.Cancelled;
                        break;
                    }

                    // Build row content as readable text
                    var rowData = new Dictionary<string, object?>();
                    var contentBuilder = new StringBuilder();

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var columnName = columnNames[i];
                        var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        rowData[columnName] = value;

                        // Build human-readable content for embedding
                        var valueStr = value?.ToString() ?? "(empty)";
                        contentBuilder.AppendLine($"{columnName}: {valueStr}");
                    }

                    var rowContent = contentBuilder.ToString();
                    var rowJson = JsonSerializer.Serialize(rowData);

                    // Create document for this row
                    var document = new Document
                    {
                        Id = Guid.NewGuid(),
                        DataSourceId = dataSource.Id,
                        FileName = $"Row_{rowIndex + 1}",
                        FilePath = $"sqlview://{sqlSource.SchemaName}.{sqlSource.ViewName}/row/{rowIndex}",
                        FileHash = ComputeStringHash(rowJson),
                        FileSize = rowJson.Length,
                        MimeType = "application/json",
                        Status = DocumentStatus.Processing,
                        CreatedAt = DateTime.UtcNow
                    };

                    dbContext.Documents.Add(document);

                    // Create single chunk per row
                    var chunk = new DocumentChunk
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = document.Id,
                        Content = rowContent,
                        ChunkIndex = 0,
                        CreatedAt = DateTime.UtcNow
                    };

                    dbContext.DocumentChunks.Add(chunk);

                    rowBatch.Add((document.Id, chunk.Id, rowContent));

                    // Process batch when full
                    if (rowBatch.Count >= batchSize)
                    {
                        await ProcessRowBatchAsync(rowBatch, dbContext, embeddingService, vectorStore, job, cancellationToken);
                        rowBatch.Clear();
                    }

                    rowIndex++;
                    job.ProcessedFiles = rowIndex;
                    job.CurrentFile = $"Row {rowIndex}";
                }

                // Process remaining rows
                if (rowBatch.Count > 0)
                {
                    await ProcessRowBatchAsync(rowBatch, dbContext, embeddingService, vectorStore, job, cancellationToken);
                }
            }

            // Update completion status
            if (job.Status != SyncJobStatus.Cancelled)
            {
                job.Status = job.FailedFiles > 0 && job.SuccessfulFiles == 0
                    ? SyncJobStatus.Failed
                    : SyncJobStatus.Completed;
            }

            job.CompletedAt = DateTime.UtcNow;
            job.CurrentFile = null;

            // Update data source
            sqlSource.SyncStatus = job.Status == SyncJobStatus.Completed
                ? SyncStatus.Completed
                : (job.Status == SyncJobStatus.Failed ? SyncStatus.Failed : SyncStatus.Pending);
            sqlSource.LastSyncAt = DateTime.UtcNow;
            sqlSource.LastSyncRowCount = job.SuccessfulFiles;
            sqlSource.LastSyncError = job.Status == SyncJobStatus.Failed ? job.ErrorMessage : null;
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "SQL View sync job completed. Processed: {Processed}, Successful: {Success}, Failed: {Failed}",
                job.ProcessedFiles, job.SuccessfulFiles, job.FailedFiles);
        }
        catch (OperationCanceledException)
        {
            job.Status = SyncJobStatus.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
            sqlSource.SyncStatus = SyncStatus.Pending;
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("SQL View sync job was cancelled");
        }
        catch (Exception ex)
        {
            job.Status = SyncJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
            sqlSource.SyncStatus = SyncStatus.Failed;
            sqlSource.LastSyncError = ex.Message;
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogError(ex, "SQL View sync job failed with error");
        }
        finally
        {
            // Move to completed jobs
            if (_activeJobs.TryRemove(job.Id, out var completedJob))
            {
                lock (_completedJobsLock)
                {
                    _completedJobs.Insert(0, completedJob);
                    while (_completedJobs.Count > 50)
                        _completedJobs.RemoveAt(_completedJobs.Count - 1);
                }
            }
            _jobCancellations.TryRemove(job.Id, out _);
        }
    }

    private async Task ProcessRowBatchAsync(
        List<(Guid docId, Guid chunkId, string content)> batch,
        IApplicationDbContext dbContext,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        SyncJob job,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            // Generate embeddings for the batch
            var contents = batch.Select(b => b.content).ToList();
            var embeddings = (await embeddingService.GenerateEmbeddingsAsync(contents, cancellationToken)).ToList();

            // Store embeddings in vector store
            for (int i = 0; i < batch.Count; i++)
            {
                await vectorStore.StoreEmbeddingAsync(batch[i].chunkId, embeddings[i], cancellationToken);
            }

            // Update document statuses to indexed
            var docIds = batch.Select(b => b.docId).ToList();
            var docs = await dbContext.Documents.Where(d => docIds.Contains(d.Id)).ToListAsync(cancellationToken);
            foreach (var doc in docs)
            {
                doc.Status = DocumentStatus.Indexed;
                doc.ProcessedAt = DateTime.UtcNow;
            }
            await dbContext.SaveChangesAsync(cancellationToken);

            job.SuccessfulFiles += batch.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process row batch");
            job.FailedFiles += batch.Count;

            // Mark documents as failed
            var docIds = batch.Select(b => b.docId).ToList();
            var docs = await dbContext.Documents.Where(d => docIds.Contains(d.Id)).ToListAsync(cancellationToken);
            foreach (var doc in docs)
            {
                doc.Status = DocumentStatus.Failed;
                doc.ErrorMessage = ex.Message;
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string ComputeStringHash(string content)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }

    #endregion

    public Task<SyncJob?> GetSyncStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (_activeJobs.TryGetValue(jobId, out var activeJob))
            return Task.FromResult<SyncJob?>(activeJob);

        lock (_completedJobsLock)
        {
            var completedJob = _completedJobs.FirstOrDefault(j => j.Id == jobId);
            return Task.FromResult(completedJob);
        }
    }

    public Task<IEnumerable<SyncJob>> GetActiveSyncJobsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<SyncJob>>(_activeJobs.Values.ToList());
    }

    public Task<IEnumerable<SyncJob>> GetRecentSyncJobsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        var jobs = new List<SyncJob>();
        jobs.AddRange(_activeJobs.Values);

        lock (_completedJobsLock)
        {
            jobs.AddRange(_completedJobs.Take(count));
        }

        return Task.FromResult<IEnumerable<SyncJob>>(jobs.OrderByDescending(j => j.StartedAt).Take(count).ToList());
    }

    public Task CancelSyncAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (_jobCancellations.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
            _logger.LogInformation("Cancelled sync job {JobId}", jobId);
        }

        return Task.CompletedTask;
    }
}
