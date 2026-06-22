using Amazon.Runtime.Documents;
using Amazon.S3Vectors;
using Amazon.S3Vectors.Model;
using PhotoSharingApp.Api.Interfaces;
using PhotoSharingApp.Api.Models;

namespace PhotoSharingApp.Api.Services;

/// <summary>
/// S3 Vector Bucket implementation of <see cref="IVectorStore"/> for storing and querying
/// photo embeddings with metadata using the "photo-embeddings" vector index.
/// </summary>
public class S3VectorStore : IVectorStore
{
    private const string VectorIndexName = "photo-embeddings";
    private const int TopK = 20;

    private readonly IAmazonS3Vectors _s3VectorsClient;
    private readonly string _vectorBucketName;

    /// <summary>
    /// Initializes a new instance of <see cref="S3VectorStore"/>.
    /// </summary>
    /// <param name="s3VectorsClient">The Amazon S3 Vectors client.</param>
    /// <param name="vectorBucketName">The name of the vector bucket.</param>
    public S3VectorStore(IAmazonS3Vectors s3VectorsClient, string vectorBucketName)
    {
        _s3VectorsClient = s3VectorsClient ?? throw new ArgumentNullException(nameof(s3VectorsClient));
        _vectorBucketName = vectorBucketName ?? throw new ArgumentNullException(nameof(vectorBucketName));
    }

    /// <inheritdoc />
    public async Task PutVectorsAsync(PhotoMetadata metadata, float[] embedding)
    {
        var metadataDocument = new Document(new Dictionary<string, Document>
        {
            ["filename"] = new Document(metadata.Filename),
            ["contentType"] = new Document(metadata.ContentType),
            ["fileSize"] = new Document(metadata.FileSize),
            ["uploadedAt"] = new Document(metadata.UploadedAt.ToString("o")),
            ["s3Key"] = new Document(metadata.S3Key)
        });

        var vectorData = new VectorData
        {
            Float32 = embedding.Select(f => (float)f).ToList()
        };

        var putVector = new PutInputVector
        {
            Key = metadata.PhotoId,
            Data = vectorData,
            Metadata = metadataDocument
        };

        var request = new PutVectorsRequest
        {
            VectorBucketName = _vectorBucketName,
            IndexName = VectorIndexName,
            Vectors = new List<PutInputVector> { putVector }
        };

        await _s3VectorsClient.PutVectorsAsync(request);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VectorSearchResult>> QueryVectorsAsync(float[] queryVector)
    {
        var vectorData = new VectorData
        {
            Float32 = queryVector.Select(f => (float)f).ToList()
        };

        var request = new QueryVectorsRequest
        {
            VectorBucketName = _vectorBucketName,
            IndexName = VectorIndexName,
            QueryVector = vectorData,
            TopK = TopK,
            ReturnMetadata = true,
            ReturnDistance = true
        };

        var response = await _s3VectorsClient.QueryVectorsAsync(request);

        var results = new List<VectorSearchResult>();

        foreach (var vector in response.Vectors)
        {
            var photoMetadata = ExtractPhotoMetadata(vector.Key, vector.Metadata);
            if (photoMetadata == null) continue;

            // Cosine distance: similarity = 1 - distance
            var similarityScore = vector.Distance.HasValue
                ? (float)(1.0 - vector.Distance.Value)
                : 0f;

            results.Add(new VectorSearchResult
            {
                Metadata = photoMetadata,
                SimilarityScore = similarityScore
            });
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<PhotoMetadata?> GetVectorsAsync(string photoId)
    {
        var request = new GetVectorsRequest
        {
            VectorBucketName = _vectorBucketName,
            IndexName = VectorIndexName,
            Keys = new List<string> { photoId },
            ReturnMetadata = true
        };

        var response = await _s3VectorsClient.GetVectorsAsync(request);

        if (response.Vectors == null || response.Vectors.Count == 0)
        {
            return null;
        }

        var vector = response.Vectors[0];
        return ExtractPhotoMetadata(vector.Key, vector.Metadata);
    }

    private static PhotoMetadata? ExtractPhotoMetadata(string key, Document? metadataDocument)
    {
        if (metadataDocument == null || !metadataDocument.Value.IsDictionary())
        {
            return null;
        }

        var dict = metadataDocument.Value.AsDictionary();

        return new PhotoMetadata
        {
            PhotoId = key,
            Filename = dict.TryGetValue("filename", out var filename) ? filename.AsString() : string.Empty,
            ContentType = dict.TryGetValue("contentType", out var contentType) ? contentType.AsString() : string.Empty,
            FileSize = dict.TryGetValue("fileSize", out var fileSize) ? fileSize.AsInt() : 0,
            UploadedAt = dict.TryGetValue("uploadedAt", out var uploadedAt)
                ? DateTime.Parse(uploadedAt.AsString(), null, System.Globalization.DateTimeStyles.RoundtripKind)
                : DateTime.MinValue,
            S3Key = dict.TryGetValue("s3Key", out var s3Key) ? s3Key.AsString() : string.Empty
        };
    }
}
