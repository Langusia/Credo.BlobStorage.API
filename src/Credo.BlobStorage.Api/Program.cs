using Credo.BlobStorage.Api.Configuration;
using Credo.BlobStorage.Api.Data;
using Credo.BlobStorage.Api.Middleware;
using Credo.BlobStorage.Api.Services;
using Credo.BlobStorage.Core.Checksums;
using Credo.BlobStorage.Core.Mime;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Configure options
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));

// Configure database
builder.Services.AddDbContext<BlobStorageDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Credo.BlobStorage API v1");
    });
}

// Add request logging middleware
app.UseRequestLogging();

// Use routing
app.UseRouting();

// Map controllers
app.MapControllers();

// Apply pending migrations on startup (in development)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<BlobStorageDbContext>();
    dbContext.Database.Migrate();
}

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
