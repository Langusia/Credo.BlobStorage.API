namespace Credo.BlobStorage.Core.Mime;

/// <summary>
/// Known MIME types and their corresponding file extensions.
/// </summary>
public static class MimeTypes
{
    /// <summary>
    /// Magic byte signatures for common file types.
    /// Key: hex string of magic bytes, Value: (MIME type, extension)
    /// </summary>
    public static readonly Dictionary<string, (string MimeType, string Extension)> MagicBytes = new()
    {
        // Images
        { "89504E47", ("image/png", "png") },
        { "FFD8FF", ("image/jpeg", "jpg") },
        { "47494638", ("image/gif", "gif") },
        { "424D", ("image/bmp", "bmp") },
        { "49492A00", ("image/tiff", "tiff") },
        { "4D4D002A", ("image/tiff", "tiff") },

        // PDF
        { "25504446", ("application/pdf", "pdf") },

        // ZIP-based formats (Office documents, archives)
        { "504B0304", ("application/zip", "zip") },
        { "504B0506", ("application/zip", "zip") },
        { "504B0708", ("application/zip", "zip") },

        // Microsoft Office (OLE2/Compound Document)
        { "D0CF11E0A1B11AE1", ("application/msword", "doc") },

        // XML
        { "3C3F786D6C", ("application/xml", "xml") },

        // Rich Text Format
        { "7B5C72746631", ("application/rtf", "rtf") },

        // Executables (dangerous)
        { "4D5A", ("application/x-msdownload", "exe") },
        { "7F454C46", ("application/x-executable", "elf") },

        // Archives
        { "1F8B08", ("application/gzip", "gz") },
        { "425A68", ("application/x-bzip2", "bz2") },
        { "526172211A0700", ("application/x-rar-compressed", "rar") },
        { "526172211A070100", ("application/x-rar-compressed", "rar") },
        { "377ABCAF271C", ("application/x-7z-compressed", "7z") },

        // Audio
        { "494433", ("audio/mpeg", "mp3") },
        { "FFFB", ("audio/mpeg", "mp3") },
        { "FFF3", ("audio/mpeg", "mp3") },
        { "FFF2", ("audio/mpeg", "mp3") },
        { "52494646", ("audio/wav", "wav") }, // Also used for WebP, AVI

        // Video
        { "000000186674797069736F6D", ("video/mp4", "mp4") },
        { "0000001C6674797069736F6D", ("video/mp4", "mp4") },
        { "000000206674797069736F6D", ("video/mp4", "mp4") },
    };

    /// <summary>
    /// File extension to MIME type mapping.
    /// </summary>
    public static readonly Dictionary<string, string> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Documents
        { ".pdf", "application/pdf" },
        { ".doc", "application/msword" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".xls", "application/vnd.ms-excel" },
        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        { ".ppt", "application/vnd.ms-powerpoint" },
        { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
        { ".odt", "application/vnd.oasis.opendocument.text" },
        { ".ods", "application/vnd.oasis.opendocument.spreadsheet" },
        { ".odp", "application/vnd.oasis.opendocument.presentation" },
        { ".rtf", "application/rtf" },

        // Text
        { ".txt", "text/plain" },
        { ".csv", "text/csv" },
        { ".json", "application/json" },
        { ".xml", "application/xml" },
        { ".html", "text/html" },
        { ".htm", "text/html" },
        { ".css", "text/css" },
        { ".js", "application/javascript" },
        { ".md", "text/markdown" },
        { ".yaml", "application/x-yaml" },
        { ".yml", "application/x-yaml" },

        // Images
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png", "image/png" },
        { ".gif", "image/gif" },
        { ".bmp", "image/bmp" },
        { ".tiff", "image/tiff" },
        { ".tif", "image/tiff" },
        { ".webp", "image/webp" },
        { ".svg", "image/svg+xml" },
        { ".ico", "image/x-icon" },

        // Archives
        { ".zip", "application/zip" },
        { ".rar", "application/x-rar-compressed" },
        { ".7z", "application/x-7z-compressed" },
        { ".tar", "application/x-tar" },
        { ".gz", "application/gzip" },
        { ".bz2", "application/x-bzip2" },

        // Audio
        { ".mp3", "audio/mpeg" },
        { ".wav", "audio/wav" },
        { ".ogg", "audio/ogg" },
        { ".flac", "audio/flac" },
        { ".m4a", "audio/mp4" },

        // Video
        { ".mp4", "video/mp4" },
        { ".avi", "video/x-msvideo" },
        { ".mkv", "video/x-matroska" },
        { ".mov", "video/quicktime" },
        { ".wmv", "video/x-ms-wmv" },
        { ".webm", "video/webm" },

        // Executables (dangerous)
        { ".exe", "application/x-msdownload" },
        { ".dll", "application/x-msdownload" },
        { ".msi", "application/x-msi" },
        { ".bat", "application/x-bat" },
        { ".cmd", "application/x-bat" },
        { ".sh", "application/x-sh" },
        { ".ps1", "application/x-powershell" },

        // Other
        { ".eml", "message/rfc822" },
        { ".msg", "application/vnd.ms-outlook" },
    };

    /// <summary>
    /// Dangerous MIME types that should never be served inline.
    /// </summary>
    public static readonly HashSet<string> DangerousTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/x-msdownload",
        "application/x-msdos-program",
        "application/x-executable",
        "application/x-sh",
        "application/x-bash",
        "application/javascript",
        "text/javascript",
        "text/html",
        "application/xhtml+xml",
        "application/x-httpd-php",
        "application/x-php",
        "application/x-bat",
        "application/x-powershell",
        "application/x-msi"
    };

    /// <summary>
    /// OOXML content types found in Office documents (DOCX, XLSX, PPTX).
    /// </summary>
    public static readonly Dictionary<string, (string MimeType, string Extension)> OoxmlTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "word/", ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx") },
        { "xl/", ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx") },
        { "ppt/", ("application/vnd.openxmlformats-officedocument.presentationml.presentation", "pptx") },
    };

    /// <summary>
    /// MIME type to extension mapping for serving files.
    /// </summary>
    public static readonly Dictionary<string, string> MimeToExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        { "application/pdf", "pdf" },
        { "application/msword", "doc" },
        { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx" },
        { "application/vnd.ms-excel", "xls" },
        { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx" },
        { "application/vnd.ms-powerpoint", "ppt" },
        { "application/vnd.openxmlformats-officedocument.presentationml.presentation", "pptx" },
        { "text/plain", "txt" },
        { "text/csv", "csv" },
        { "application/json", "json" },
        { "application/xml", "xml" },
        { "image/jpeg", "jpg" },
        { "image/png", "png" },
        { "image/gif", "gif" },
        { "image/bmp", "bmp" },
        { "image/tiff", "tiff" },
        { "image/webp", "webp" },
        { "application/zip", "zip" },
        { "application/x-rar-compressed", "rar" },
        { "application/x-7z-compressed", "7z" },
        { "application/gzip", "gz" },
        { "audio/mpeg", "mp3" },
        { "audio/wav", "wav" },
        { "video/mp4", "mp4" },
        { "application/octet-stream", "bin" },
    };

    public const string DefaultMimeType = "application/octet-stream";
    public const string DefaultExtension = "bin";
}
