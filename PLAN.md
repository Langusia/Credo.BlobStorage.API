# Credo.BlobStorage.Client — Implementation Plan

## Overview
Reusable NuGet-ready client library (`netstandard2.0`) for internal consumers to interact with the BlobStorage API. Wraps HTTP calls with convenience helpers (auto-create bucket on upload, global Get/Delete by docId).

---

## 1. API Extensions (src/Credo.BlobStorage.Api)

The current API requires `bucket` in paths. Since DocId is globally unique, add two new endpoints:

### New controller: `GlobalObjectsController`
- **GET** `/api/objects/{docId}` — Lookup ObjectEntity by DocId (any bucket), stream file back
- **DELETE** `/api/objects/{docId}` — Lookup and delete by DocId (any bucket)

Both return 404 with `ObjectNotFound` error code if not found.

---

## 2. New Project: `src/Credo.BlobStorage.Client`

**Target**: `netstandard2.0`
**Dependencies**: `Microsoft.Extensions.Http`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `System.Text.Json`

### File Structure
```
src/Credo.BlobStorage.Client/
├── Credo.BlobStorage.Client.csproj
├── Channel.cs                          # Enum: CSS, MyCredo
├── IBlobStorageClient.cs               # Main interface
├── BlobStorageClient.cs                # HttpClient-based implementation
├── BlobStorageClientOptions.cs         # BaseUrl config
├── BlobStorageResult.cs                # Result<T> wrapper
├── BlobStorageException.cs             # For ThrowOnError()
├── Models/
│   ├── UploadResponse.cs               # DocId, Sha256, ContentType, etc.
│   └── BlobMetadata.cs                 # Head response: ContentType, Size, Filename
└── DependencyInjection/
    └── ServiceCollectionExtensions.cs  # AddBlobStorageClient()
```

### Channel Enum
```csharp
public enum Channel
{
    CSS,
    MyCredo
}
```
Internal mapping: `CSS` → bucket `"css"`, `MyCredo` → bucket `"mycredo"`.

### IBlobStorageClient Interface
```csharp
public interface IBlobStorageClient
{
    // Upload from stream — auto-creates bucket if needed
    Task<BlobStorageResult<UploadResponse>> UploadAsync(
        Channel channel,
        string filename,
        Stream content,
        string? contentType = null,
        CancellationToken ct = default);

    // Upload from byte[] — convenience overload
    Task<BlobStorageResult<UploadResponse>> UploadAsync(
        Channel channel,
        string filename,
        byte[] content,
        string? contentType = null,
        CancellationToken ct = default);

    // Get by docId — returns file stream (global, no bucket needed)
    Task<BlobStorageResult<Stream>> GetAsync(
        string docId,
        CancellationToken ct = default);

    // Delete by docId (global, no bucket needed)
    Task<BlobStorageResult> DeleteAsync(
        string docId,
        CancellationToken ct = default);
}
```

### BlobStorageResult / BlobStorageResult<T>
```csharp
public class BlobStorageResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public int HttpStatusCode { get; }

    // Throws BlobStorageException if not successful
    public BlobStorageResult EnsureSuccess();
}

public class BlobStorageResult<T> : BlobStorageResult
{
    public T Value { get; }  // Only valid when IsSuccess

    public BlobStorageResult<T> EnsureSuccess();  // Returns self for chaining
}
```

### BlobStorageException
```csharp
public class BlobStorageException : Exception
{
    public string ErrorCode { get; }
    public int HttpStatusCode { get; }
}
```

### UploadResponse Model
```csharp
public class UploadResponse
{
    public string DocId { get; set; }
    public string Bucket { get; set; }
    public string Filename { get; set; }
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; }
    public string ContentType { get; set; }
}
```

### BlobStorageClient Implementation Details
- **Upload flow**:
  1. PUT `/api/buckets/{bucket}/objects/{filename}` with stream body
  2. If 404 (bucket not found) → POST `/api/buckets` to create → retry PUT
  3. If 409 (already exists) → treat as success (idempotent)
- **Get flow**: GET `/api/objects/{docId}` → return response stream
- **Delete flow**: DELETE `/api/objects/{docId}`

### DI Registration
```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBlobStorageClient(
        this IServiceCollection services,
        Action<BlobStorageClientOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<IBlobStorageClient, BlobStorageClient>(...);
        return services;
    }
}
```

### BlobStorageClientOptions
```csharp
public class BlobStorageClientOptions
{
    public string BaseUrl { get; set; }
}
```

---

## 3. Solution Integration

- Add project to `Credo.BlobStorage.sln`
- No dependency on Core project (client is standalone HTTP wrapper)

---

## 4. Files Modified

| File | Change |
|------|--------|
| `Credo.BlobStorage.sln` | Add new Client project |
| `src/Credo.BlobStorage.Api/Controllers/GlobalObjectsController.cs` | **NEW** — GET/DELETE by docId |
| `src/Credo.BlobStorage.Client/Credo.BlobStorage.Client.csproj` | **NEW** |
| `src/Credo.BlobStorage.Client/Channel.cs` | **NEW** |
| `src/Credo.BlobStorage.Client/IBlobStorageClient.cs` | **NEW** |
| `src/Credo.BlobStorage.Client/BlobStorageClient.cs` | **NEW** |
| `src/Credo.BlobStorage.Client/BlobStorageClientOptions.cs` | **NEW** |
| `src/Credo.BlobStorage.Client/BlobStorageResult.cs` | **NEW** |
| `src/Credo.BlobStorage.Client/BlobStorageException.cs` | **NEW** |
| `src/Credo.BlobStorage.Client/Models/UploadResponse.cs` | **NEW** |
| `src/Credo.BlobStorage.Client/DependencyInjection/ServiceCollectionExtensions.cs` | **NEW** |
