namespace Credo.BlobStorage.Api.Services;

/// <summary>
/// Interface for building filesystem paths for blob storage.
/// </summary>
public interface IPathBuilder
{
    /// <summary>
    /// Builds the full filesystem path for storing a blob.
    /// Format: {RootPath}/{yyyy}/{lvl1}/{lvl2}/{docId}/blob.{ext}
    /// </summary>
    /// <param name="docId">Document ID in format yyyy-guid.</param>
    /// <param name="extension">File extension without leading dot.</param>
    /// <returns>Full filesystem path.</returns>
    string BuildBlobPath(string docId, string extension);

    /// <summary>
    /// Builds the directory path for a blob (without filename).
    /// Format: {RootPath}/{yyyy}/{lvl1}/{lvl2}/{docId}
    /// </summary>
    /// <param name="docId">Document ID in format yyyy-guid.</param>
    /// <returns>Directory path.</returns>
    string BuildDirectoryPath(string docId);

    /// <summary>
    /// Builds the temporary file path for upload.
    /// Format: {RootPath}/{yyyy}/{lvl1}/{lvl2}/{docId}/blob.tmp
    /// </summary>
    /// <param name="docId">Document ID in format yyyy-guid.</param>
    /// <returns>Temporary file path.</returns>
    string BuildTempPath(string docId);

    /// <summary>
    /// Generates a new DocId in format: {yyyy}-{guid}.
    /// </summary>
    /// <param name="year">Optional year; uses current year if not provided.</param>
    /// <returns>New DocId string.</returns>
    string GenerateDocId(int? year = null);

    /// <summary>
    /// Extracts the year from a DocId.
    /// </summary>
    /// <param name="docId">Document ID.</param>
    /// <returns>Year portion of the DocId.</returns>
    int ExtractYear(string docId);

    /// <summary>
    /// Extracts the GUID portion from a DocId.
    /// </summary>
    /// <param name="docId">Document ID.</param>
    /// <returns>GUID portion of the DocId.</returns>
    string ExtractGuid(string docId);
}
