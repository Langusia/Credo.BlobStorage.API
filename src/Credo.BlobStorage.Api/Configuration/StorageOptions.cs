namespace Credo.BlobStorage.Api.Configuration;

/// <summary>
/// Configuration options for blob storage.
/// </summary>
public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Root path for storing blob files.
    /// </summary>
    public string RootPath { get; set; } = "/mnt/storage";

    /// <summary>
    /// Maximum upload size in bytes (default: 1GB).
    /// </summary>
    public long MaxUploadBytes { get; set; } = 1073741824;

    /// <summary>
    /// Buffer size for streaming uploads (default: 64KB).
    /// </summary>
    public int UploadBufferSize { get; set; } = 65536;

    /// <summary>
    /// Size of the first chunk read for MIME detection (default: 64KB).
    /// </summary>
    public int FirstChunkSize { get; set; } = 65536;

    /// <summary>
    /// List of allowed file extensions (without leading dot).
    /// </summary>
    public string[] AllowedExtensions { get; set; } =
    [
        "pdf", "doc", "docx", "xls", "xlsx", "txt", "csv",
        "jpg", "jpeg", "png", "gif", "zip", "xml", "json"
    ];

    /// <summary>
    /// Content types that can be served inline (not as attachments).
    /// </summary>
    public string[] InlineContentTypes { get; set; } =
    [
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/gif",
        "text/plain"
    ];

    /// <summary>
    /// Buckets to seed on application startup.
    /// </summary>
    public string[] DefaultBuckets { get; set; } = ["default"];
}
