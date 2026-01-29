namespace Credo.BlobStorage.Api.Models.Responses;

/// <summary>
/// Response model for paginated list of objects.
/// </summary>
public record ObjectListResponse
{
    /// <summary>
    /// List of objects on the current page.
    /// </summary>
    public required IReadOnlyList<ObjectResponse> Objects { get; init; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// Total number of objects matching the query.
    /// </summary>
    public long TotalCount { get; init; }

    /// <summary>
    /// Total number of pages.
    /// </summary>
    public int TotalPages { get; init; }

    /// <summary>
    /// Indicates if there is a next page.
    /// </summary>
    public bool HasNextPage { get; init; }

    /// <summary>
    /// Indicates if there is a previous page.
    /// </summary>
    public bool HasPreviousPage { get; init; }
}
