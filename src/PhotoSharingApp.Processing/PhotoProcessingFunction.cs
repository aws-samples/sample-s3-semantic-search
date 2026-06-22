using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Runtime.Documents;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3Vectors;
using Amazon.S3Vectors.Model;
using PhotoSharingApp.Processing.Models;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace PhotoSharingApp.Processing;

/// <summary>
/// Lambda function triggered by S3 ObjectCreated events on the photos/ prefix.
/// Downloads the uploaded photo, generates a 1024-dimension embedding via Bedrock,
/// and stores the vector with metadata in S3 Vectors.
/// </summary>
public class PhotoProcessingFunction
{
    private static readonly Regex S3KeyPattern = new(
        @"^photos/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\.(jpe?g|png|webp)$",
        RegexOptions.Compiled);

    private const int EmbeddingDimension = 1024;

    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonBedrockRuntime _bedrockClient;
    private readonly IAmazonS3Vectors _s3VectorsClient;

    private readonly string _photosBucketName;
    private readonly string _vectorBucketName;
    private readonly string _vectorIndexName;
    private readonly string _bedrockModelId;

    /// <summary>
    /// Default constructor used by the Lambda runtime.
    /// Reads configuration from environment variables.
    /// </summary>
    public PhotoProcessingFunction()
    {
        _s3Client = new AmazonS3Client();
        _bedrockClient = new AmazonBedrockRuntimeClient();
        _s3VectorsClient = new AmazonS3VectorsClient();

        _photosBucketName = Environment.GetEnvironmentVariable("PHOTOS_BUCKET_NAME")
            ?? throw new InvalidOperationException("PHOTOS_BUCKET_NAME environment variable is not set.");
        _vectorBucketName = Environment.GetEnvironmentVariable("VECTOR_BUCKET_NAME")
            ?? throw new InvalidOperationException("VECTOR_BUCKET_NAME environment variable is not set.");
        _vectorIndexName = Environment.GetEnvironmentVariable("VECTOR_INDEX_NAME")
            ?? throw new InvalidOperationException("VECTOR_INDEX_NAME environment variable is not set.");
        _bedrockModelId = Environment.GetEnvironmentVariable("BEDROCK_MODEL_ID")
            ?? throw new InvalidOperationException("BEDROCK_MODEL_ID environment variable is not set.");
    }

    /// <summary>
    /// Constructor for testing, allowing injection of AWS service clients.
    /// </summary>
    internal PhotoProcessingFunction(
        IAmazonS3 s3Client,
        IAmazonBedrockRuntime bedrockClient,
        IAmazonS3Vectors s3VectorsClient,
        string photosBucketName,
        string vectorBucketName,
        string vectorIndexName,
        string bedrockModelId)
    {
        _s3Client = s3Client;
        _bedrockClient = bedrockClient;
        _s3VectorsClient = s3VectorsClient;
        _photosBucketName = photosBucketName;
        _vectorBucketName = vectorBucketName;
        _vectorIndexName = vectorIndexName;
        _bedrockModelId = bedrockModelId;
    }

    /// <summary>
    /// Entry point for the Lambda function. Processes each S3 event record independently.
    /// </summary>
    public async Task FunctionHandler(S3Event s3Event, ILambdaContext context)
    {
        var exceptions = new List<Exception>();

        foreach (var record in s3Event.Records)
        {
            try
            {
                await ProcessRecordAsync(record, context);
            }
            catch (Exception ex)
            {
                var key = record.S3?.Object?.Key ?? "unknown";
                context.Logger.LogError($"Failed to process record with key '{key}': {ex.Message}");
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException(
                $"Failed to process {exceptions.Count} record(s).", exceptions);
        }
    }

    private async Task ProcessRecordAsync(S3Event.S3EventNotificationRecord record, ILambdaContext context)
    {
        var bucket = record.S3.Bucket.Name;
        var key = record.S3.Object.Key;

        // Validate key matches expected pattern
        var match = S3KeyPattern.Match(key);
        if (!match.Success)
        {
            context.Logger.LogWarning($"Skipping record with invalid key: '{key}'");
            return;
        }

        var photoId = match.Groups[1].Value;
        var extension = match.Groups[2].Value;

        context.Logger.LogInformation($"Processing photo {photoId} from key '{key}'");

        // Idempotency guard: check if vector already exists
        if (await VectorExistsAsync(photoId))
        {
            context.Logger.LogInformation($"Photo {photoId} already processed, skipping.");
            return;
        }

        // Download photo from S3
        var getObjectResponse = await _s3Client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = bucket,
            Key = key
        });

        byte[] photoBytes;
        using (var memoryStream = new MemoryStream())
        {
            await getObjectResponse.ResponseStream.CopyToAsync(memoryStream);
            photoBytes = memoryStream.ToArray();
        }

        // Derive content type from extension
        var contentType = GetContentType(extension);

        // Generate embedding via Bedrock
        var imageBase64 = Convert.ToBase64String(photoBytes);
        var embedding = await GenerateEmbeddingAsync(imageBase64, contentType);

        // Build metadata
        var metadata = new PhotoMetadata
        {
            PhotoId = photoId,
            Filename = Path.GetFileName(key),
            ContentType = contentType,
            FileSize = photoBytes.LongLength,
            S3Key = key,
            UploadedAt = getObjectResponse.LastModified?.ToUniversalTime() ?? DateTime.UtcNow
        };

        // Store vector + metadata in S3 Vectors
        await PutVectorAsync(metadata, embedding);

        context.Logger.LogInformation($"Photo {photoId} processed successfully.");
    }

    private async Task<bool> VectorExistsAsync(string photoId)
    {
        var request = new GetVectorsRequest
        {
            VectorBucketName = _vectorBucketName,
            IndexName = _vectorIndexName,
            Keys = new List<string> { photoId },
            ReturnMetadata = false
        };

        var response = await _s3VectorsClient.GetVectorsAsync(request);
        return response.Vectors != null && response.Vectors.Count > 0;
    }

    private async Task<float[]> GenerateEmbeddingAsync(string imageBase64, string contentType)
    {
        var format = contentType switch
        {
            "image/jpeg" => "jpeg",
            "image/png" => "png",
            "image/webp" => "webp",
            _ => throw new ArgumentException($"Unsupported content type: {contentType}")
        };

        var requestBody = new
        {
            taskType = "SINGLE_EMBEDDING",
            singleEmbeddingParams = new
            {
                embeddingPurpose = "GENERIC_INDEX",
                embeddingDimension = EmbeddingDimension,
                image = new
                {
                    format,
                    source = new { bytes = imageBase64 }
                }
            }
        };

        var jsonBody = JsonSerializer.Serialize(requestBody);

        var request = new InvokeModelRequest
        {
            ModelId = _bedrockModelId,
            ContentType = "application/json",
            Accept = "application/json",
            Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonBody))
        };

        var response = await _bedrockClient.InvokeModelAsync(request);

        using var responseStream = response.Body;
        var responseDocument = await JsonDocument.ParseAsync(responseStream);

        var firstEmbedding = responseDocument.RootElement
            .GetProperty("embeddings")[0]
            .GetProperty("embedding");

        var embedding = new float[firstEmbedding.GetArrayLength()];
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = firstEmbedding[i].GetSingle();
        }

        return embedding;
    }

    private async Task PutVectorAsync(PhotoMetadata metadata, float[] embedding)
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
            IndexName = _vectorIndexName,
            Vectors = new List<PutInputVector> { putVector }
        };

        await _s3VectorsClient.PutVectorsAsync(request);
    }

    private static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        "jpg" or "jpeg" => "image/jpeg",
        "png" => "image/png",
        "webp" => "image/webp",
        _ => throw new ArgumentException($"Unsupported file extension: {extension}")
    };
}
