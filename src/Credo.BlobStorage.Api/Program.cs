using Credo.BlobStorage.Api.Configuration;
using Credo.BlobStorage.Api.Data;
using Credo.BlobStorage.Api.Data.Entities;
using Credo.BlobStorage.Api.Middleware;
using Credo.BlobStorage.Api.Services;
using Credo.BlobStorage.Core.Checksums;
using Credo.BlobStorage.Core.Mime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Configure options
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));

// Configure database (SQL Server)
builder.Services.AddDbContext<BlobStorageDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register core services
builder.Services.AddSingleton<IChecksumCalculator, Sha256ChecksumCalculator>();
builder.Services.AddSingleton<IMimeDetector, MimeDetector>();

// Register application services
builder.Services.AddScoped<IPathBuilder, PathBuilder>();
builder.Services.AddScoped<IStorageService, StorageService>();

// Configure controllers
builder.Services.AddControllers();

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Credo.BlobStorage API",
        Version = "v1",
        Description = "A disk-backed blob storage API with MIME detection"
    });

    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Configure request size limits
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // Allow large file uploads
    serverOptions.Limits.MaxRequestBodySize = null;
});

var app = builder.Build();

// Apply migrations and seed default buckets on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BlobStorageDbContext>();
    var storageOptions = scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        // Apply migrations
        logger.LogInformation("Applying database migrations for schema '{Schema}'...", BlobStorageDbContext.SchemaName);
        dbContext.Database.Migrate();
        logger.LogInformation("Database migrations applied successfully");

        // Seed default buckets
        if (storageOptions.DefaultBuckets.Length > 0)
        {
            var bucketsToSeed = storageOptions.DefaultBuckets.Distinct().ToList();
            logger.LogInformation("Seeding {Count} default bucket(s)...", bucketsToSeed.Count);

            foreach (var bucketName in bucketsToSeed)
            {
                // FindAsync checks both tracker and database
                var existing = await dbContext.Buckets.FindAsync(bucketName);
                if (existing == null)
                {
                    dbContext.Buckets.Add(new BucketEntity
                    {
                        Name = bucketName,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                    logger.LogInformation("Created default bucket: {BucketName}", bucketName);
                }
                else
                {
                    logger.LogDebug("Default bucket '{BucketName}' already exists", bucketName);
                }
            }

            await dbContext.SaveChangesAsync();
            logger.LogInformation("Default buckets seeded successfully");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during database initialization");
        throw;
    }
}

// Configure Swagger (enabled in all environments)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Credo.BlobStorage API v1");
    c.RoutePrefix = "swagger";
});

// Add request logging middleware
app.UseRequestLogging();

// Use routing
app.UseRouting();

// Map controllers
app.MapControllers();

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
