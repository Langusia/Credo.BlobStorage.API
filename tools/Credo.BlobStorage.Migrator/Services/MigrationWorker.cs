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
            _logger.LogInformation("Metadata source: {DocumentsTable} (main Documents DB)", _options.DocumentsTable);
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

            // Step 3: Seed document IDs from content database
            await SeedDocumentIdsAsync(stoppingToken);

            // Step 4: Enrich with metadata from source database
            await EnrichMetadataAsync(stoppingToken);

            // Step 5: Migrate documents to API
            await MigrateDocumentsAsync(stoppingToken);

            // Step 6: Report statistics
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

        // First, ensure the schema and table exist using raw SQL (most reliable approach)
        await EnsureMigrationLogTableExistsAsync(context, ct);

        _logger.LogInformation("Database schema ready");
    }

    private async Task EnsureMigrationLogTableExistsAsync(MigrationDbContext context, CancellationToken ct)
    {
        const string createSchemaSql = @"
            IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'migration')
            BEGIN
                EXEC('CREATE SCHEMA [migration]')
            END";

        const string createTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[migration].[MigrationLog]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [migration].[MigrationLog] (
                    [Id] int NOT NULL IDENTITY(1,1),
                    [SourceDocumentId] bigint NOT NULL,
                    [SourceYear] int NOT NULL,
                    [OriginalFilename] nvarchar(256) NULL,
                    [OriginalExtension] nvarchar(10) NULL,
                    [ClaimedContentType] nvarchar(50) NULL,
                    [SourceFileSize] int NULL,
                    [SourceRecordDate] datetime2 NULL,
                    [Status] int NOT NULL DEFAULT 0,
                    [TargetDocId] nvarchar(50) NULL,
                    [TargetBucket] nvarchar(63) NULL,
                    [TargetFilename] nvarchar(1024) NULL,
                    [TargetSha256] nvarchar(64) NULL,
                    [DetectedContentType] nvarchar(255) NULL,
                    [ErrorMessage] nvarchar(2000) NULL,
                    [RetryCount] int NOT NULL DEFAULT 0,
                    [CreatedAtUtc] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                    [ProcessedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_MigrationLog] PRIMARY KEY ([Id])
                )

                CREATE INDEX [IX_MigrationLog_Status] ON [migration].[MigrationLog] ([Status])
                CREATE UNIQUE INDEX [UQ_MigrationLog_SourceYear_SourceDocumentId] ON [migration].[MigrationLog] ([SourceYear], [SourceDocumentId])
            END";

        // Alter existing table to make metadata columns nullable (if table already exists with NOT NULL constraints)
        const string alterTableSql = @"
            IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[migration].[MigrationLog]') AND type in (N'U'))
            BEGIN
                -- Make OriginalFilename nullable if it's NOT NULL
                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[migration].[MigrationLog]') AND name = 'OriginalFilename' AND is_nullable = 0)
                    ALTER TABLE [migration].[MigrationLog] ALTER COLUMN [OriginalFilename] nvarchar(256) NULL

                -- Make SourceFileSize nullable if it's NOT NULL
                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[migration].[MigrationLog]') AND name = 'SourceFileSize' AND is_nullable = 0)
                    ALTER TABLE [migration].[MigrationLog] ALTER COLUMN [SourceFileSize] int NULL

                -- Make SourceRecordDate nullable if it's NOT NULL
                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[migration].[MigrationLog]') AND name = 'SourceRecordDate' AND is_nullable = 0)
                    ALTER TABLE [migration].[MigrationLog] ALTER COLUMN [SourceRecordDate] datetime2 NULL
            END";

        _logger.LogInformation("Ensuring migration schema exists...");
        await context.Database.ExecuteSqlRawAsync(createSchemaSql, ct);

        _logger.LogInformation("Ensuring MigrationLog table exists...");
        await context.Database.ExecuteSqlRawAsync(createTableSql, ct);

        _logger.LogInformation("Ensuring columns are nullable for 3-step flow...");
        await context.Database.ExecuteSqlRawAsync(alterTableSql, ct);
    }

    private async Task<bool> EnsureBucketExistsAsync(CancellationToken ct)
    {
        _logger.LogInformation("Ensuring target bucket '{Bucket}' exists...", _options.TargetBucket);

        using var scope = _serviceProvider.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<IBlobStorageApiClient>();

        return await apiClient.EnsureBucketExistsAsync(_options.TargetBucket, ct);
    }

    /// <summary>
    /// Step 3: Seed ContentIds from content database (Documents_2017.DocumentsContent.Id).
    /// Only inserts IDs with Status=Seeded, no metadata yet.
    /// SourceDocumentId field stores the ContentId (DocumentsContent.Id).
    /// </summary>
    private async Task SeedDocumentIdsAsync(CancellationToken ct)
    {
        _logger.LogInformation("=== Step 3: Seeding ContentIds from content database ===");

        using var scope = _serviceProvider.CreateScope();
        var sourceRepo = scope.ServiceProvider.GetRequiredService<ISourceRepository>();
        var migrationContext = scope.ServiceProvider.GetRequiredService<MigrationDbContext>();

        // Get ContentIds (DocumentsContent.Id) from the year-specific database
        _logger.LogInformation("Fetching ContentIds from {ContentTable}...", _options.ContentTable);
        var contentIds = await sourceRepo.GetContentIdsAsync(ct);
        _logger.LogInformation("Found {Count} unique ContentIds with content", contentIds.Count);

        // Get existing ContentIds from migration log (already seeded)
        var existingIds = await migrationContext.MigrationLog
            .Where(l => l.SourceYear == _options.Year)
            .Select(l => l.SourceDocumentId)  // SourceDocumentId stores ContentId
            .ToHashSetAsync(ct);

        _logger.LogInformation("Found {Count} entries already in migration log", existingIds.Count);

        // Filter out already seeded
        var newContentIds = contentIds.Except(existingIds).ToList();
        _logger.LogInformation("Found {Count} new ContentIds to seed", newContentIds.Count);

        if (newContentIds.Count == 0)
        {
            _logger.LogInformation("No new ContentIds to seed");
            return;
        }

        // Seed IDs in batches (only IDs, no metadata yet)
        var seededCount = 0;
        foreach (var batch in newContentIds.Chunk(_options.BatchSize))
        {
            var entries = batch.Select(contentId => new MigrationLogEntry
            {
                SourceDocumentId = contentId,  // Stores ContentId (DocumentsContent.Id)
                SourceYear = _options.Year,
                Status = MigrationStatus.Seeded,
                CreatedAtUtc = DateTime.UtcNow
            }).ToList();

            await migrationContext.MigrationLog.AddRangeAsync(entries, ct);
            await migrationContext.SaveChangesAsync(ct);
            seededCount += entries.Count;
            _logger.LogInformation("Seeded {Count}/{Total} ContentIds...", seededCount, newContentIds.Count);
        }

        _logger.LogInformation("Seed phase complete. Added {Count} new ContentIds to migration log", seededCount);
    }

    /// <summary>
    /// Step 4: Enrich seeded entries with metadata from source database (Documents.Documents).
    /// Matches Documents.ContentId = seeded ContentId (stored in SourceDocumentId).
    /// Updates entries from Status=Seeded to Status=Pending.
    /// </summary>
    private async Task EnrichMetadataAsync(CancellationToken ct)
    {
        _logger.LogInformation("=== Step 4: Enriching with metadata from source database ===");

        using var scope = _serviceProvider.CreateScope();
        var sourceRepo = scope.ServiceProvider.GetRequiredService<ISourceRepository>();
        var migrationContext = scope.ServiceProvider.GetRequiredService<MigrationDbContext>();

        // Get entries that need metadata enrichment
        var seededCount = await migrationContext.MigrationLog
            .Where(l => l.SourceYear == _options.Year && l.Status == MigrationStatus.Seeded)
            .CountAsync(ct);

        _logger.LogInformation("Found {Count} entries awaiting metadata enrichment", seededCount);

        if (seededCount == 0)
        {
            _logger.LogInformation("No entries need metadata enrichment");
            return;
        }

        var enrichedCount = 0;
        var skippedCount = 0;

        // Process in batches
        while (!ct.IsCancellationRequested)
        {
            // Fetch batch of seeded entries
            var seededEntries = await migrationContext.MigrationLog
                .Where(l => l.SourceYear == _options.Year && l.Status == MigrationStatus.Seeded)
                .OrderBy(l => l.SourceDocumentId)
                .Take(_options.BatchSize)
                .ToListAsync(ct);

            if (seededEntries.Count == 0)
                break;

            // Get ContentIds for this batch (stored in SourceDocumentId)
            var contentIds = seededEntries.Select(e => e.SourceDocumentId).ToList();

            // Fetch metadata from source database where Documents.ContentId matches
            var documents = await sourceRepo.GetDocumentsForContentIdsAsync(contentIds, ct);
            // Key by ContentId since that's what we're matching
            var documentsDict = documents.ToDictionary(d => d.ContentId);

            _logger.LogDebug("Fetched metadata for {Found}/{Requested} ContentIds",
                documents.Count, contentIds.Count);

            // Update entries with metadata
            foreach (var entry in seededEntries)
            {
                if (documentsDict.TryGetValue(entry.SourceDocumentId, out var doc))
                {
                    entry.OriginalFilename = doc.DocumentName;
                    entry.OriginalExtension = doc.DocumentExt?.TrimStart('.');
                    entry.ClaimedContentType = doc.ContentType;
                    entry.SourceFileSize = doc.FileSize;
                    entry.SourceRecordDate = doc.RecordDate;
                    entry.Status = MigrationStatus.Pending;
                    enrichedCount++;
                }
                else
                {
                    // No metadata found - mark as skipped
                    entry.Status = MigrationStatus.Skipped;
                    entry.ErrorMessage = "No metadata found in source database (no Documents.ContentId match)";
                    entry.ProcessedAtUtc = DateTime.UtcNow;
                    skippedCount++;
                }
            }

            await migrationContext.SaveChangesAsync(ct);
            _logger.LogInformation("Enriched {Enriched} entries, skipped {Skipped}...",
                enrichedCount, skippedCount);
        }

        _logger.LogInformation("Metadata enrichment complete. Enriched: {Enriched}, Skipped: {Skipped}",
            enrichedCount, skippedCount);
    }

    /// <summary>
    /// Step 5: Migrate documents to API.
    /// Fetches blob from content database and uploads to BlobStorage API.
    /// </summary>
    private async Task MigrateDocumentsAsync(CancellationToken ct)
    {
        _logger.LogInformation("=== Step 5: Migrating documents to API ===");

        using var semaphore = new SemaphoreSlim(_options.MaxParallelism);
        var processedCount = 0;
        var successCount = 0;
        var failedCount = 0;
        var skippedCount = 0;

        while (!ct.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var migrationContext = scope.ServiceProvider.GetRequiredService<MigrationDbContext>();

            // Fetch pending batch (only entries with metadata - Status=Pending)
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
