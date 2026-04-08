using Credo.BlobStorage.Api.Configuration;
using Credo.BlobStorage.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<BlobStorageDbContext>>();

            services.AddDbContext<BlobStorageDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
            });

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
