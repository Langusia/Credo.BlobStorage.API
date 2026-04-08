using Credo.BlobStorage.Api.Configuration;
using Credo.BlobStorage.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Credo.BlobStorage.Tests.Integration;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _testStoragePath;

    public TestWebApplicationFactory()
    {
        _testStoragePath = Path.Combine(Path.GetTempPath(), "blob-storage-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_testStoragePath);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove everything related to the real database
            var descriptorsToRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<BlobStorageDbContext>)
                         || d.ServiceType == typeof(DbContextOptions)
                         || (d.ServiceType.FullName?.Contains("SqlServer") ?? false)
                         || (d.ImplementationType?.FullName?.Contains("SqlServer") ?? false))
                .ToList();
            foreach (var d in descriptorsToRemove)
                services.Remove(d);

            // Register InMemory database
            services.AddDbContext<BlobStorageDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
            });

            // Point storage at temp directory
            services.Configure<StorageOptions>(opt =>
            {
                opt.RootPath = _testStoragePath;
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        try
        {
            if (Directory.Exists(_testStoragePath))
                Directory.Delete(_testStoragePath, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
