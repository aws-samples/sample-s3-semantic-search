namespace PhotoSharingApp.Api.Models;

/// <summary>
/// Response DTO returned for a successful photo search (200 OK).
/// </summary>
public record SearchResponse
{
    /// <summary>Array of matching photo results ordered by similarity score descending.</summary>
    public required SearchResult[] Results { get; init; }

    /// <summary>Total number of results returned.</summary>
    public required int Count { get; init; }
}

/// <summary>
/// A single search result entry containing photo metadata and similarity score.
/// </summary>
public record SearchResult
{
    /// <summary>Unique identifier for the photo (GUID).</summary>
    public required string PhotoId { get; init; }

    /// <summary>Original filename of the photo.</summary>
    public required string Filename { get; init; }

    /// <summary>MIME content type (image/jpeg, image/png, image/webp).</summary>
    public required string ContentType { get; init; }

    /// <summary>File size in bytes.</summary>
    public required long FileSize { get; init; }

    /// <summary>Upload timestamp in UTC (ISO 8601 format).</summary>
    public required DateTime UploadedAt { get; init; }

    /// <summary>Cosine similarity score (0.0 to 1.0).</summary>
    public required double SimilarityScore { get; init; }

    /// <summary>Presigned URL for the photo thumbnail.</summary>
    public required string ThumbnailUrl { get; init; }
}
