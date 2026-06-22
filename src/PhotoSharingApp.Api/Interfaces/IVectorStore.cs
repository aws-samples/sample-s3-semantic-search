using PhotoSharingApp.Api.Models;

namespace PhotoSharingApp.Api.Interfaces;

/// <summary>
/// Provides operations for storing and querying vectors in the S3 Vector Bucket.
/// Uses the "photo-embeddings" vector index with 1024 dimensions and cosine distance metric.
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Stores photo metadata and its associated vector embedding in the vector bucket.
    /// </summary>
    /// <param name="metadata">The photo metadata to store.</param>
    /// <param name="embedding">The 1024-dimension vector embedding.</param>
    Task PutVectorsAsync(PhotoMetadata metadata, float[] embedding);

    /// <summary>
    /// Queries the vector bucket for the nearest neighbors using cosine similarity.
    /// Returns up to 20 results ranked by similarity score in descending order.
    /// </summary>
    /// <param name="queryVector">The query vector to search against.</param>
    /// <returns>A list of matching results with metadata and similarity scores.</returns>
    Task<IReadOnlyList<VectorSearchResult>> QueryVectorsAsync(float[] queryVector);

    /// <summary>
    /// Retrieves a single photo's metadata by its ID from the vector bucket.
    /// </summary>
    /// <param name="photoId">The unique photo identifier (GUID).</param>
    /// <returns>The photo metadata if found; otherwise null.</returns>
    Task<PhotoMetadata?> GetVectorsAsync(string photoId);
}

/// <summary>
/// Represents a single result from a vector similarity search.
/// </summary>
public record VectorSearchResult
{
    /// <summary>The photo metadata associated with this result.</summary>
    public required PhotoMetadata Metadata { get; init; }

    /// <summary>The cosine similarity score (0 to 1, higher is more similar).</summary>
    public required float SimilarityScore { get; init; }
}
