using System.Diagnostics;
using Credo.BlobStorage.Migrator.Configuration;
using Credo.BlobStorage.Migrator.Data.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credo.BlobStorage.Migrator.Services;

/// <summary>
/// Background service that runs the migration process once and exits.
/// Supports parallel processing via WorkerToken partitioning.
/// </summary>
public class MigrationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MigrationOptions _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<MigrationWorker> _logger;

    public MigrationWorker(
        IServiceProvider serviceProvider,
        IOptions<MigrationOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<MigrationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting migration for year {Year}", _options.Year);
            _logger.LogInformation("Worker token: {WorkerToken}", _options.WorkerToken?.ToString() ?? "ALL");
            _logger.LogInformation("Content source: {ContentTable} (Documents_{Year} DB)", _options.ContentTable, _options.Year);
            _logger.LogInformation("Target bucket: {Bucket}", _options.TargetBucket);
            _logger.LogInformation("Batch size: {BatchSize}, Max parallelism: {MaxParallelism}",
                _options.BatchSize, _options.MaxParallelism);

            // Step 1: Ensure migration log table exists
            await ApplyMigrationsAsync(stoppingToken);

            // Step 2: Ensure target bucket exists
            if (!await EnsureBucketExistsAsync(stoppingToken))
            {
                _logger.LogError("Failed to ensure target bucket exists. Aborting.");
                return;
            }

            // Step 3: Migrate documents to API
            await MigrateDocumentsAsync(stoppingToken);

            // Step 4: Report statistics
            await ReportStatisticsAsync(stoppingToken);

            stopwatch.Stop();
            _logger.LogInformation("Migration completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Migration was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration failed with error");
        }
        finally
        {
            // Stop the application
            _lifetime.StopApplication();
        }
    }

    private async Task ApplyMigrationsAsync(CancellationToken ct)
    {
        _logger.LogInformation("Applying migration database migrations...");

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MigrationDbContext>();

        // Apply EF Core migrations - creates schema and tables with correct nullability
        await context.Database.MigrateAsync(ct);

        _logger.LogInformation("Database migrations applied successfully");
    }

    private async Task<bool> EnsureBucketExistsAsync(CancellationToken ct)
    {
        _logger.LogInformation("Ensuring target bucket '{Bucket}' exists...", _options.TargetBucket);

        using var scope = _serviceProvider.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<IBlobStorageApiClient>();

        return await apiClient.EnsureBucketExistsAsync(_options.TargetBucket, ct);
    }

    /// <summary>
    /// Step 3: Migrate documents to API.
    /// Fetches blob from content database and uploads to BlobStorage API.
    /// Filters by WorkerToken when set for parallel processing.
    /// </summary>
    private async Task MigrateDocumentsAsync(CancellationToken ct)
    {
        _logger.LogInformation("=== Step 3: Migrating documents to API ===");

        using var semaphore = new SemaphoreSlim(_options.MaxParallelism);
        var processedCount = 0;
        var successCount = 0;
        var failedCount = 0;
        var skippedCount = 0;

        while (!ct.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var migrationContext = scope.ServiceProvider.GetRequiredService<MigrationDbContext>();

            // Build query with optional WorkerToken filter
            var query = migrationContext.MigrationLog
                .Where(l => l.SourceYear == _options.Year &&
                           (l.Status == MigrationStatus.Pending ||
                            (l.Status == MigrationStatus.Failed && l.RetryCount < _options.MaxRetries)));

            if (_options.WorkerToken.HasValue)
            {
                query = query.Where(l => l.WorkerToken == _options.WorkerToken);
            }

            var pendingEntries = await query
                .OrderBy(l => l.SourceDocumentId)
                .Take(_options.BatchSize)
                .ToListAsync(ct);

            if (pendingEntries.Count == 0)
            {
                _logger.LogInformation("No more pending documents to process");
                break;
            }

            _logger.LogInformation("Processing batch of {Count} documents", pendingEntries.Count);

            var tasks = pendingEntries.Select(async entry =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var result = await ProcessDocumentAsync(entry, ct);
                    Interlocked.Increment(ref processedCount);

                    switch (result)
                    {
                        case MigrationStatus.Completed:
                            Interlocked.Increment(ref successCount);
                            break;
                        case MigrationStatus.Failed:
                            Interlocked.Increment(ref failedCount);
                            break;
                        case MigrationStatus.Skipped:
                            Interlocked.Increment(ref skippedCount);
                            break;
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation("Progress: {Processed} processed, {Success} success, {Failed} failed, {Skipped} skipped",
                processedCount, successCount, failedCount, skippedCount);
        }

        _logger.LogInformation("Migration phase complete. Total: {Processed}, Success: {Success}, Failed: {Failed}, Skipped: {Skipped}",
            processedCount, successCount, failedCount, skippedCount);
    }

    private async Task<MigrationStatus> ProcessDocumentAsync(MigrationLogEntry entry, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var sourceRepo = scope.ServiceProvider.GetRequiredService<ISourceRepository>();
        var apiClient = scope.ServiceProvider.GetRequiredService<IBlobStorageApiClient>();
        var migrationContext = scope.ServiceProvider.GetRequiredService<MigrationDbContext>();

        try
        {
            // Update status to InProgress
            var trackedEntry = await migrationContext.MigrationLog
                .FirstAsync(l => l.Id == entry.Id, ct);

            trackedEntry.Status = MigrationStatus.InProgress;
            await migrationContext.SaveChangesAsync(ct);

            // Get content from source
            var content = await sourceRepo.GetDocumentContentAsync(entry.SourceDocumentId, ct);

            if (content == null || content.Length == 0)
            {
                _logger.LogWarning("No content found for document {DocumentId}, marking as skipped",
                    entry.SourceDocumentId);

                trackedEntry.Status = MigrationStatus.Skipped;
                trackedEntry.ErrorMessage = "No content found in source database";
                trackedEntry.ProcessedAtUtc = DateTime.UtcNow;
                await migrationContext.SaveChangesAsync(ct);

                return MigrationStatus.Skipped;
            }

            // Build target filename: {SourceDocumentId}/{OriginalName}.{ClaimedExtension}
            var filename = entry.OriginalFilename ?? entry.SourceDocumentId.ToString();
            var extension = string.IsNullOrEmpty(entry.OriginalExtension) ? "" : $".{entry.OriginalExtension}";
            var targetFilename = $"{entry.SourceDocumentId}/{filename}{extension}";

            // Upload to API
            var result = await apiClient.UploadAsync(
                _options.TargetBucket,
                targetFilename,
                content,
                entry.ClaimedContentType,
                _options.Year,
                ct);

            if (result.Success)
            {
                trackedEntry.Status = MigrationStatus.Completed;
                trackedEntry.TargetDocId = result.DocId;
                trackedEntry.TargetBucket = _options.TargetBucket;
                trackedEntry.TargetFilename = targetFilename;
                trackedEntry.TargetSha256 = result.Sha256;
                trackedEntry.DetectedContentType = result.DetectedContentType;
                trackedEntry.ProcessedAtUtc = DateTime.UtcNow;

                if (result.AlreadyExists)
                {
                    _logger.LogDebug("Document {DocumentId} already exists in target", entry.SourceDocumentId);
                }
                else
                {
                    _logger.LogDebug("Document {DocumentId} uploaded successfully as {DocId}",
                        entry.SourceDocumentId, result.DocId);
                }

                await migrationContext.SaveChangesAsync(ct);
                return MigrationStatus.Completed;
            }
            else
            {
                trackedEntry.Status = MigrationStatus.Failed;
                trackedEntry.RetryCount++;
                trackedEntry.ErrorMessage = result.ErrorMessage?.Length > 2000
                    ? result.ErrorMessage[..2000]
                    : result.ErrorMessage;
                trackedEntry.ProcessedAtUtc = DateTime.UtcNow;

                _logger.LogWarning("Failed to upload document {DocumentId}: {Error}",
                    entry.SourceDocumentId, result.ErrorMessage);

                await migrationContext.SaveChangesAsync(ct);
                return MigrationStatus.Failed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document {DocumentId}", entry.SourceDocumentId);

            try
            {
                var trackedEntry = await migrationContext.MigrationLog
                    .FirstAsync(l => l.Id == entry.Id, ct);

                trackedEntry.Status = MigrationStatus.Failed;
                trackedEntry.RetryCount++;
                trackedEntry.ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                trackedEntry.ProcessedAtUtc = DateTime.UtcNow;
                await migrationContext.SaveChangesAsync(ct);
            }
            catch
            {
                // Ignore save errors in exception handler
            }

            return MigrationStatus.Failed;
        }
    }

    private async Task ReportStatisticsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var migrationContext = scope.ServiceProvider.GetRequiredService<MigrationDbContext>();

        // Build base query with optional WorkerToken filter
        var baseQuery = migrationContext.MigrationLog
            .Where(l => l.SourceYear == _options.Year);

        if (_options.WorkerToken.HasValue)
        {
            baseQuery = baseQuery.Where(l => l.WorkerToken == _options.WorkerToken);
        }

        var stats = await baseQuery
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var workerLabel = _options.WorkerToken.HasValue
            ? $"Year {_options.Year}, WorkerToken {_options.WorkerToken}"
            : $"Year {_options.Year}";

        _logger.LogInformation("=== Migration Statistics for {Label} ===", workerLabel);

        foreach (var stat in stats.OrderBy(s => s.Status))
        {
            _logger.LogInformation("  {Status}: {Count}", stat.Status, stat.Count);
        }

        var failedWithMaxRetries = await baseQuery
            .CountAsync(l => l.Status == MigrationStatus.Failed &&
                            l.RetryCount >= _options.MaxRetries, ct);

        if (failedWithMaxRetries > 0)
        {
            _logger.LogWarning("  {Count} documents failed after {MaxRetries} retries",
                failedWithMaxRetries, _options.MaxRetries);
        }
    }
}
