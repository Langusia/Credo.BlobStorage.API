# Credo.BlobStorage API

A Linux-hosted, disk-backed blob storage API built with .NET 9.0. This infrastructure-level service stores files directly to disk with full MIME detection, SHA-256 checksums, and S3-style naming conventions.

## Features

- **Disk-backed storage** with year-based filesystem partitioning
- **Multi-layer MIME detection** using magic bytes, ZIP inspection, and file extensions
- **S3-style validation** for bucket names and object keys
- **Streaming uploads** with incremental SHA-256 hashing
- **Dangerous content detection** for executable file mismatches
- **PostgreSQL** metadata storage with Entity Framework Core
- **Swagger/OpenAPI** documentation

## Tech Stack

- .NET 9.0
- ASP.NET Core Web API
- PostgreSQL with EF Core
- Linux deployment target

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- PostgreSQL 14+
- Linux environment (recommended)

### Configuration

Update `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=blobstorage;Username=postgres;Password=postgres"
  },
  "Storage": {
    "RootPath": "/mnt/storage",
    "MaxUploadBytes": 1073741824,
    "AllowedExtensions": ["pdf", "doc", "docx", "xls", "xlsx", "txt", "csv", "jpg", "jpeg", "png", "gif", "zip", "xml", "json"]
  }
}
```

### Running the API

```bash
cd src/Credo.BlobStorage.Api
dotnet run
```

The API will be available at `http://localhost:5000` with Swagger UI at `/swagger`.

### Running Tests

```bash
dotnet test
```

## API Endpoints

### Buckets

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/buckets` | List all buckets |
| POST | `/api/buckets` | Create a bucket |
| GET | `/api/buckets/{bucket}` | Get bucket info |
| DELETE | `/api/buckets/{bucket}` | Delete bucket (must be empty) |

### Objects

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/buckets/{bucket}/objects` | List objects (paginated) |
| PUT | `/api/buckets/{bucket}/objects/{filename}` | Upload via raw stream |
| POST | `/api/buckets/{bucket}/objects/form` | Upload via form |
| GET | `/api/buckets/{bucket}/objects/{docId}` | Download by DocId |
| GET | `/api/buckets/{bucket}/objects/by-name/{filename}` | Download by filename |
| DELETE | `/api/buckets/{bucket}/objects/{docId}` | Delete by DocId |
| DELETE | `/api/buckets/{bucket}/objects/by-name/{filename}` | Delete by filename |
| HEAD | `/api/buckets/{bucket}/objects/{docId}` | Get headers only |

## Usage Examples

### Create a Bucket

```bash
curl -X POST http://localhost:5000/api/buckets \
  -H "Content-Type: application/json" \
  -d '{"name": "invoices"}'
```

### Upload a File (Raw Stream)

```bash
curl -X PUT http://localhost:5000/api/buckets/invoices/objects/report.pdf \
  -H "Content-Type: application/octet-stream" \
  --data-binary @report.pdf
```

### Upload a File (Form)

```bash
curl -X POST http://localhost:5000/api/buckets/invoices/objects/form \
  -F "file=@report.pdf"
```

### Download a File

```bash
# By DocId
curl http://localhost:5000/api/buckets/invoices/objects/2026-3f0d2a7e-8c1b-4a0c-9f0f-2d3a4b5c6d7e -o file.pdf

# By filename
curl http://localhost:5000/api/buckets/invoices/objects/by-name/report.pdf -o file.pdf
```

### List Objects with Prefix

```bash
curl "http://localhost:5000/api/buckets/invoices/objects?prefix=2026/&page=1&pageSize=50"
```

## Filesystem Layout

Files are stored using year-based partitioning:

```
{RootPath}/{yyyy}/{lvl1}/{lvl2}/{docId}/blob.{ext}

Example:
/mnt/storage/2026/3f/0d/2026-3f0d2a7e-8c1b-4a0c-9f0f-2d3a4b5c6d7e/blob.pdf
```

## DocId Format

Each uploaded file receives a unique identifier:

```
{yyyy}-{guid}
Example: 2026-3f0d2a7e-8c1b-4a0c-9f0f-2d3a4b5c6d7e
```

## S3-Style Validation Rules

### Bucket Names
- Length: 3-63 characters
- Allowed: lowercase a-z, digits 0-9, dots (.), hyphens (-)
- Must start and end with letter or digit
- No consecutive dots (..)
- Must not look like IPv4 address
- Must not start with "xn--" or end with "-s3alias"

### Filenames (Object Keys)
- UTF-8 encoded, max 1024 bytes
- Allowed: a-z A-Z 0-9 . _ - /
- Forward slash (/) allowed for prefixes
- No control characters or backslashes

## Project Structure

```
Credo.BlobStorage/
├── src/
│   ├── Credo.BlobStorage.Core/        # Reusable library
│   │   ├── Checksums/                 # SHA-256 calculator
│   │   ├── Mime/                      # MIME detection
│   │   └── Validation/                # S3-style validators
│   │
│   └── Credo.BlobStorage.Api/
│       ├── Controllers/               # API endpoints
│       ├── Services/                  # Business logic
│       ├── Data/                      # EF Core entities
│       ├── Models/                    # DTOs
│       └── Middleware/                # Request logging
│
└── tests/
    └── Credo.BlobStorage.Tests/       # Unit & integration tests
```

## License

This project is proprietary software.
