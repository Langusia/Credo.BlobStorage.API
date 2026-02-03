using Credo.BlobStorage.Migrator.Configuration;
using Credo.BlobStorage.Migrator.Data.Migration;
using Credo.BlobStorage.Migrator.Data.Source;
using Credo.BlobStorage.Migrator.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/migration-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting Credo.BlobStorage.Migrator");

    var builder = Host.CreateApplicationBuilder(args);

    // Use Serilog
    builder.Logging.ClearProviders();
    builder.Services.AddSerilog();

    // Bind configuration
    builder.Services.Configure<MigrationOptions>(
        builder.Configuration.GetSection(MigrationOptions.SectionName));

    var migrationOptions = builder.Configuration
        .GetSection(MigrationOptions.SectionName)
        .Get<MigrationOptions>()
        ?? throw new InvalidOperationException("Migration configuration is missing");

    // Register Source DbContext (SQL Server - main Documents DB with metadata)
    builder.Services.AddDbContext<SourceDbContext>(options =>
    {
        options.UseSqlServer(migrationOptions.SourceConnectionString);
    });

    // Register Content DbContext (SQL Server - year-specific DB with content, e.g., Documents_2017)
    builder.Services.AddDbContext<ContentDbContext>(options =>
    {
        options.UseSqlServer(migrationOptions.ContentConnectionString);
    });

    // Register Migration DbContext (SQL Server - migration log under 'migration' schema)
    builder.Services.AddDbContext<MigrationDbContext>(options =>
    {
        options.UseSqlServer(migrationOptions.MigrationDbConnectionString, sqlOptions =>
        {
            // Use simple assembly name (not FullName which includes version info)
            sqlOptions.MigrationsAssembly(typeof(MigrationDbContext).Assembly.GetName().Name);
        });
    });

    // Register HttpClient for BlobStorage API
    builder.Services.AddHttpClient<IBlobStorageApiClient, BlobStorageApiClient>(client =>
    {
        client.BaseAddress = new Uri(migrationOptions.TargetApiBaseUrl);
        client.Timeout = TimeSpan.FromMinutes(5);
    });

    // Register services
    builder.Services.AddScoped<ISourceRepository, SourceRepository>();

    // Register the worker
    builder.Services.AddHostedService<MigrationWorker>();

    var host = builder.Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
