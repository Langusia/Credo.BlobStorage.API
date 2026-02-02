using System.Diagnostics;
using Credo.BlobStorage.Migrator.Configuration;
using Credo.BlobStorage.Migrator.Data.Migration;
using Credo.BlobStorage.Migrator.Data.Source;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credo.BlobStorage.Migrator.Services;

/// <summary>
/// Background service that runs the migration process once and exits.
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
            _logger.LogInformation("Source tables: {DocumentsTable}, {ContentTable}",
                _options.DocumentsTable, _options.ContentTable);
            _logger.LogInformation("Target bucket: {Bucket}", _options.TargetBucket);
            _logger.LogInformation("Batch size: {BatchSize}, Max parallelism: {MaxParallelism}",
                _options.BatchSize, _options.MaxParallelism);

            // Step 1: Apply migrations
            await ApplyMigrationsAsync(stoppingToken);

            // Step 2: Ensure target bucket exists
            if (!await EnsureBucketExistsAsync(stoppingToken))
            {
                _logger.LogError("Failed to ensure target bucket exists. Aborting.");
                return;
            }

            // Step 3: Seed phase
            await SeedPendingDocumentsAsync(stoppingToken);

            // Step 4: Migrate phase
            await MigrateDocumentsAsync(stoppingToken);

            // Step 5: Report statistics
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
        _logger.LogInformation("Applying database migrations for schema '{Schema}'...", MigrationDbContext.SchemaName);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MigrationDbContext>();

        var pendingMigrations = await context.Database.GetPendingMigrationsAsync(ct);
        _logger.LogInformation("Pending migrations: {Count}", pendingMigrations.Count());

        foreach (var migration in pendingMigrations)
        {
            _logger.LogInformation("  - {Migration}", migration);
        }

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

    private async Task SeedPendingDocumentsAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting seed phase - scanning source documents...");

        using var scope = _serviceProvider.CreateScope();
        var sourceRepo = scope.ServiceProvider.GetRequiredService<ISourceRepository>();
        var migrationContext = scope.ServiceProvider.GetRequiredService<MigrationDbContext>();

        var sourceCount = await sourceRepo.GetDocumentCountAsync(ct);
        _logger.LogInformation("Found {Count} non-deleted documents in source", sourceCount);

        // Get existing document IDs from migration log
        var existingIds = await migrationContext.MigrationLog
            .Where(l => l.SourceYear == _options.Year)
            .Select(l => l.SourceDocumentId)
            .ToHashSetAsync(ct);

        _logger.LogInformation("Found {Count} documents already in migration log", existingIds.Count);

        var seededCount = 0;
        var batchEntries = new List<MigrationLogEntry>();

        await foreach (var document in sourceRepo.GetDocumentsAsync(ct))
        {
            if (existingIds.Contains(document.DocumentId))
                continue;

            var entry = new MigrationLogEntry
            {
                SourceDocumentId = document.DocumentId,
                SourceYear = _options.Year,
                OriginalFilename = document.DocumentName,
                OriginalExtension = document.DocumentExt?.TrimStart('.'),
                ClaimedContentType = document.ContentType,
                SourceFileSize = document.FileSize,
                SourceRecordDate = document.RecordDate,
                Status = MigrationStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow
            };

            batchEntries.Add(entry);

            if (batchEntries.Count >= _options.BatchSize)
            {
                await migrationContext.MigrationLog.AddRangeAsync(batchEntries, ct);
                await migrationContext.SaveChangesAsync(ct);
                seededCount += batchEntries.Count;
                _logger.LogInformation("Seeded {Count} documents...", seededCount);
                batchEntries.Clear();
            }
        }

        // Save remaining entries
        if (batchEntries.Count > 0)
        {
            await migrationContext.MigrationLog.AddRangeAsync(batchEntries, ct);
            await migrationContext.SaveChangesAsync(ct);
            seededCount += batchEntries.Count;
        }

        _logger.LogInformation("Seed phase complete. Added {Count} new documents to migration log", seededCount);
    }

    private async Task MigrateDocumentsAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting migration phase...");

        using var semaphore = new SemaphoreSlim(_options.MaxParallelism);
        var processedCount = 0;
        var successCount = 0;
        var failedCount = 0;
        var skippedCount = 0;

        while (!ct.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var migrationContext = scope.ServiceProvider.GetRequiredService<MigrationDbContext>();

            // Fetch pending batch
            var pendingEntries = await migrationContext.MigrationLog
                .Where(l => l.SourceYear == _options.Year &&
                           (l.Status == MigrationStatus.Pending ||
                            (l.Status == MigrationStatus.Failed && l.RetryCount < _options.MaxRetries)))
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
            var extension = string.IsNullOrEmpty(entry.OriginalExtension) ? "" : $".{entry.OriginalExtension}";
            var targetFilename = $"{entry.SourceDocumentId}/{entry.OriginalFilename}{extension}";

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

        var stats = await migrationContext.MigrationLog
            .Where(l => l.SourceYear == _options.Year)
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        _logger.LogInformation("=== Migration Statistics for Year {Year} ===", _options.Year);

        foreach (var stat in stats.OrderBy(s => s.Status))
        {
            _logger.LogInformation("  {Status}: {Count}", stat.Status, stat.Count);
        }

        var failedWithMaxRetries = await migrationContext.MigrationLog
            .CountAsync(l => l.SourceYear == _options.Year &&
                            l.Status == MigrationStatus.Failed &&
                            l.RetryCount >= _options.MaxRetries, ct);

        if (failedWithMaxRetries > 0)
        {
            _logger.LogWarning("  {Count} documents failed after {MaxRetries} retries",
                failedWithMaxRetries, _options.MaxRetries);
        }
    }
}
