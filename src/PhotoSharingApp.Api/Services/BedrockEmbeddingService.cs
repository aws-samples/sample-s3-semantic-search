using System.Text;
using System.Text.Json;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using PhotoSharingApp.Api.Interfaces;

namespace PhotoSharingApp.Api.Services;

/// <summary>
/// Generates vector embeddings using Amazon Bedrock Nova Multimodal Embeddings V2 model.
/// Supports both image (base64) and text inputs for cross-modal similarity search.
/// </summary>
public class BedrockEmbeddingService : IEmbeddingService
{
    private const string ModelId = "amazon.nova-2-multimodal-embeddings-v1:0";
    private const int OutputEmbeddingLength = 1024;

    private readonly IAmazonBedrockRuntime _bedrockRuntime;

    public BedrockEmbeddingService(IAmazonBedrockRuntime bedrockRuntime)
    {
        _bedrockRuntime = bedrockRuntime ?? throw new ArgumentNullException(nameof(bedrockRuntime));
    }

    /// <inheritdoc />
    public async Task<float[]> GenerateImageEmbeddingAsync(string imageBase64, string contentType)
    {
        var format = contentType switch
        {
            "image/jpeg" => "jpeg",
            "image/png" => "png",
            "image/webp" => "webp",
            _ => throw new ArgumentException($"Unsupported content type: {contentType}", nameof(contentType))
        };

        var requestBody = new
        {
            taskType = "SINGLE_EMBEDDING",
            singleEmbeddingParams = new
            {
                embeddingPurpose = "GENERIC_INDEX",
                embeddingDimension = OutputEmbeddingLength,
                image = new
                {
                    format,
                    source = new { bytes = imageBase64 }
                }
            }
        };

        return await InvokeModelAndExtractEmbeddingAsync(requestBody);
    }

    /// <inheritdoc />
    public async Task<float[]> GenerateTextEmbeddingAsync(string text)
    {
        var requestBody = new
        {
            taskType = "SINGLE_EMBEDDING",
            singleEmbeddingParams = new
            {
                embeddingPurpose = "GENERIC_INDEX",
                embeddingDimension = OutputEmbeddingLength,
                text = new
                {
                    truncationMode = "END",
                    value = text
                }
            }
        };

        return await InvokeModelAndExtractEmbeddingAsync(requestBody);
    }

    private async Task<float[]> InvokeModelAndExtractEmbeddingAsync(object requestBody)
    {
        var jsonBody = JsonSerializer.Serialize(requestBody);

        var request = new InvokeModelRequest
        {
            ModelId = ModelId,
            ContentType = "application/json",
            Accept = "application/json",
            Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonBody))
        };

        var response = await _bedrockRuntime.InvokeModelAsync(request);

        using var responseStream = response.Body;
        var responseDocument = await JsonDocument.ParseAsync(responseStream);

        // Response format: { "embeddings": [{ "embeddingType": "...", "embedding": [...] }] }
        var firstEmbedding = responseDocument.RootElement.GetProperty("embeddings")[0].GetProperty("embedding");
        var embedding = new float[firstEmbedding.GetArrayLength()];

        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = firstEmbedding[i].GetSingle();
        }

        return embedding;
    }
}
