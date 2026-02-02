using System.Net;
using System.Net.Http.Json;
using Credo.BlobStorage.Api.Data;
using Credo.BlobStorage.Api.Models.Requests;
using Credo.BlobStorage.Api.Models.Responses;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credo.BlobStorage.Tests.Integration;

public class BucketsControllerTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public BucketsControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing DbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<BlobStorageDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory database for testing
                services.AddDbContext<BlobStorageDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
                });
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreateBucket_ValidName_ReturnsCreated()
    {
        var request = new CreateBucketRequest { Name = "test-bucket" };

        var response = await _client.PostAsJsonAsync("/api/buckets", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var bucket = await response.Content.ReadFromJsonAsync<BucketResponse>();
        bucket.Should().NotBeNull();
        bucket!.Name.Should().Be("test-bucket");
        bucket.ObjectCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateBucket_InvalidName_ReturnsBadRequest()
    {
        var request = new CreateBucketRequest { Name = "Invalid-Bucket-Name" };

        var response = await _client.PostAsJsonAsync("/api/buckets", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Code.Should().Be(ErrorCodes.InvalidBucketName);
    }

    [Fact]
    public async Task CreateBucket_DuplicateName_ReturnsConflict()
    {
        var request = new CreateBucketRequest { Name = "duplicate-bucket" };
        await _client.PostAsJsonAsync("/api/buckets", request);

        var response = await _client.PostAsJsonAsync("/api/buckets", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Error.Code.Should().Be(ErrorCodes.BucketAlreadyExists);
    }

    [Fact]
    public async Task GetBucket_ExistingBucket_ReturnsOk()
    {
        var createRequest = new CreateBucketRequest { Name = "get-test-bucket" };
        await _client.PostAsJsonAsync("/api/buckets", createRequest);

        var response = await _client.GetAsync("/api/buckets/get-test-bucket");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bucket = await response.Content.ReadFromJsonAsync<BucketResponse>();
        bucket!.Name.Should().Be("get-test-bucket");
    }

    [Fact]
    public async Task GetBucket_NonExistingBucket_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/buckets/nonexistent-bucket");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Error.Code.Should().Be(ErrorCodes.BucketNotFound);
    }

    [Fact]
    public async Task ListBuckets_MultipleBuckets_ReturnsAll()
    {
        await _client.PostAsJsonAsync("/api/buckets", new CreateBucketRequest { Name = "list-bucket-1" });
        await _client.PostAsJsonAsync("/api/buckets", new CreateBucketRequest { Name = "list-bucket-2" });

        var response = await _client.GetAsync("/api/buckets");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var buckets = await response.Content.ReadFromJsonAsync<List<BucketResponse>>();
        buckets.Should().Contain(b => b.Name == "list-bucket-1");
        buckets.Should().Contain(b => b.Name == "list-bucket-2");
    }

    [Fact]
    public async Task DeleteBucket_EmptyBucket_ReturnsNoContent()
    {
        await _client.PostAsJsonAsync("/api/buckets", new CreateBucketRequest { Name = "delete-bucket" });

        var response = await _client.DeleteAsync("/api/buckets/delete-bucket");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify bucket is deleted
        var getResponse = await _client.GetAsync("/api/buckets/delete-bucket");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteBucket_NonExistingBucket_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/api/buckets/nonexistent-bucket");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Error.Code.Should().Be(ErrorCodes.BucketNotFound);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
