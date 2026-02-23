using System;

namespace Credo.BlobStorage.Client.Models
{
    public class UploadResponse
    {
        public string DocId { get; set; } = string.Empty;
        public string Bucket { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string? DetectedContentType { get; set; }
        public string? DetectedExtension { get; set; }
        public bool IsMismatch { get; set; }
        public bool IsDangerousMismatch { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
        public string DownloadByNameUrl { get; set; } = string.Empty;
    }
}
