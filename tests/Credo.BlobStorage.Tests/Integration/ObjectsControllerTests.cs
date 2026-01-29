using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Credo.BlobStorage.Api.Configuration;
using Credo.BlobStorage.Api.Data;
using Credo.BlobStorage.Api.Models.Requests;
using Credo.BlobStorage.Api.Models.Responses;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Credo.BlobStorage.Tests.Integration;

public class ObjectsControllerTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _testStoragePath;

    public ObjectsControllerTests(WebApplicationFactory<Program> factory)
    {
        _testStoragePath = Path.Combine(Path.GetTempPath(), "blob-storage-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_testStoragePath);

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

                // Configure test storage path
                services.Configure<StorageOptions>(opt =>
                {
                    opt.RootPath = _testStoragePath;
                });
            });
        });

        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        // Create a test bucket
        await _client.PostAsJsonAsync("/api/buckets", new CreateBucketRequest { Name = "test-bucket" });
    }

    public Task DisposeAsync()
    {
        _client.Dispose();

        // Clean up test storage
        try
        {
            if (Directory.Exists(_testStoragePath))
            {
                Directory.Delete(_testStoragePath, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Upload_RawStream_ReturnsCreated()
    {
        var content = "Hello, World!"u8.ToArray();
        var requestContent = new ByteArrayContent(content);
        requestContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        var response = await _client.PutAsync("/api/buckets/test-bucket/objects/test-file.txt", requestContent);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var objectResponse = await response.Content.ReadFromJsonAsync<ObjectResponse>();
        objectResponse.Should().NotBeNull();
        objectResponse!.Filename.Should().Be("test-file.txt");
        objectResponse.SizeBytes.Should().Be(content.Length);
        objectResponse.Bucket.Should().Be("test-bucket");
        objectResponse.DocId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Upload_WithYear_UsesProvidedYear()
    {
        var content = "Test content"u8.ToArray();
        var requestContent = new ByteArrayContent(content);

        var response = await _client.PutAsync("/api/buckets/test-bucket/objects/year-test.txt?year=2023", requestContent);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var objectResponse = await response.Content.ReadFromJsonAsync<ObjectResponse>();
        objectResponse!.DocId.Should().StartWith("2023-");
    }

    [Fact]
    public async Task Upload_DuplicateFilename_ReturnsConflict()
    {
        var content = new ByteArrayContent("content"u8.ToArray());
        await _client.PutAsync("/api/buckets/test-bucket/objects/duplicate.txt", content);

        var response = await _client.PutAsync("/api/buckets/test-bucket/objects/duplicate.txt", content);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Error.Code.Should().Be(ErrorCodes.ObjectAlreadyExists);
    }

    [Fact]
    public async Task Upload_NonExistentBucket_ReturnsNotFound()
    {
        var content = new ByteArrayContent("content"u8.ToArray());

        var response = await _client.PutAsync("/api/buckets/nonexistent/objects/test.txt", content);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Error.Code.Should().Be(ErrorCodes.BucketNotFound);
    }

    [Fact]
    public async Task Upload_FormFile_ReturnsCreated()
    {
        var content = "Form file content"u8.ToArray();
        var formContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        formContent.Add(fileContent, "file", "form-upload.txt");

        var response = await _client.PostAsync("/api/buckets/test-bucket/objects/form", formContent);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var objectResponse = await response.Content.ReadFromJsonAsync<ObjectResponse>();
        objectResponse!.Filename.Should().Be("form-upload.txt");
    }

    [Fact]
    public async Task DownloadById_ExistingObject_ReturnsFile()
    {
        // Upload first
        var uploadContent = "Download test content"u8.ToArray();
        var requestContent = new ByteArrayContent(uploadContent);
        var uploadResponse = await _client.PutAsync("/api/buckets/test-bucket/objects/download-test.txt", requestContent);
        var objectResponse = await uploadResponse.Content.ReadFromJsonAsync<ObjectResponse>();

        // Download
        var response = await _client.GetAsync($"/api/buckets/test-bucket/objects/{objectResponse!.DocId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var downloadedContent = await response.Content.ReadAsByteArrayAsync();
        downloadedContent.Should().BeEquivalentTo(uploadContent);
    }

    [Fact]
    public async Task DownloadByName_ExistingObject_ReturnsFile()
    {
        // Upload first
        var uploadContent = "Download by name test"u8.ToArray();
        var requestContent = new ByteArrayContent(uploadContent);
        await _client.PutAsync("/api/buckets/test-bucket/objects/download-by-name.txt", requestContent);

        // Download
        var response = await _client.GetAsync("/api/buckets/test-bucket/objects/by-name/download-by-name.txt");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var downloadedContent = await response.Content.ReadAsByteArrayAsync();
        downloadedContent.Should().BeEquivalentTo(uploadContent);
    }

    [Fact]
    public async Task Download_NonExistentObject_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/buckets/test-bucket/objects/nonexistent-doc-id");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Head_ExistingObject_ReturnsHeaders()
    {
        // Upload first
        var uploadContent = "Head test content"u8.ToArray();
        var requestContent = new ByteArrayContent(uploadContent);
        var uploadResponse = await _client.PutAsync("/api/buckets/test-bucket/objects/head-test.txt", requestContent);
        var objectResponse = await uploadResponse.Content.ReadFromJsonAsync<ObjectResponse>();

        // HEAD request
        var request = new HttpRequestMessage(HttpMethod.Head, $"/api/buckets/test-bucket/objects/{objectResponse!.DocId}");
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentLength.Should().Be(uploadContent.Length);
        response.Headers.ETag.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteById_ExistingObject_ReturnsNoContent()
    {
        // Upload first
        var content = new ByteArrayContent("Delete test"u8.ToArray());
        var uploadResponse = await _client.PutAsync("/api/buckets/test-bucket/objects/delete-test.txt", content);
        var objectResponse = await uploadResponse.Content.ReadFromJsonAsync<ObjectResponse>();

        // Delete
        var response = await _client.DeleteAsync($"/api/buckets/test-bucket/objects/{objectResponse!.DocId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deleted
        var getResponse = await _client.GetAsync($"/api/buckets/test-bucket/objects/{objectResponse.DocId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteByName_ExistingObject_ReturnsNoContent()
    {
        // Upload first
        var content = new ByteArrayContent("Delete by name test"u8.ToArray());
        await _client.PutAsync("/api/buckets/test-bucket/objects/delete-by-name.txt", content);

        // Delete
        var response = await _client.DeleteAsync("/api/buckets/test-bucket/objects/by-name/delete-by-name.txt");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ListObjects_ReturnsPagedList()
    {
        // Upload a few objects
        await _client.PutAsync("/api/buckets/test-bucket/objects/list-test-1.txt", new ByteArrayContent("1"u8.ToArray()));
        await _client.PutAsync("/api/buckets/test-bucket/objects/list-test-2.txt", new ByteArrayContent("2"u8.ToArray()));
        await _client.PutAsync("/api/buckets/test-bucket/objects/list-test-3.txt", new ByteArrayContent("3"u8.ToArray()));

        var response = await _client.GetAsync("/api/buckets/test-bucket/objects?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResponse = await response.Content.ReadFromJsonAsync<ObjectListResponse>();
        listResponse!.Objects.Should().HaveCountGreaterOrEqualTo(3);
        listResponse.Page.Should().Be(1);
    }

    [Fact]
    public async Task ListObjects_WithPrefix_FiltersResults()
    {
        // Upload objects with different prefixes
        await _client.PutAsync("/api/buckets/test-bucket/objects/invoices/inv-001.txt", new ByteArrayContent("inv1"u8.ToArray()));
        await _client.PutAsync("/api/buckets/test-bucket/objects/invoices/inv-002.txt", new ByteArrayContent("inv2"u8.ToArray()));
        await _client.PutAsync("/api/buckets/test-bucket/objects/reports/rep-001.txt", new ByteArrayContent("rep1"u8.ToArray()));

        var response = await _client.GetAsync("/api/buckets/test-bucket/objects?prefix=invoices/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResponse = await response.Content.ReadFromJsonAsync<ObjectListResponse>();
        listResponse!.Objects.Should().OnlyContain(o => o.Filename.StartsWith("invoices/"));
    }

    [Fact]
    public async Task Upload_PdfFile_DetectsMimeType()
    {
        // PDF magic bytes + some content
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        var content = new byte[100];
        Array.Copy(pdfContent, content, pdfContent.Length);
        var requestContent = new ByteArrayContent(content);

        var response = await _client.PutAsync("/api/buckets/test-bucket/objects/document.pdf", requestContent);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var objectResponse = await response.Content.ReadFromJsonAsync<ObjectResponse>();
        objectResponse!.DetectedContentType.Should().Be("application/pdf");
        objectResponse.DetectedExtension.Should().Be("pdf");
    }

    [Fact]
    public async Task Upload_WithClaimedMismatch_FlagsIsMismatch()
    {
        // PNG content
        var pngContent = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var content = new byte[100];
        Array.Copy(pngContent, content, pngContent.Length);
        var requestContent = new ByteArrayContent(content);
        requestContent.Headers.Add("X-Claimed-Content-Type", "application/pdf");

        var response = await _client.PutAsync("/api/buckets/test-bucket/objects/mismatch-test.png", requestContent);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var objectResponse = await response.Content.ReadFromJsonAsync<ObjectResponse>();
        objectResponse!.DetectedContentType.Should().Be("image/png");
        objectResponse.IsMismatch.Should().BeTrue();
    }
}
