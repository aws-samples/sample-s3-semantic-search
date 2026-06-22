namespace PhotoSharingApp.Api.Interfaces;

/// <summary>
/// Generates vector embeddings using Amazon Bedrock Nova Multimodal Embeddings model.
/// Supports both image and text inputs for cross-modal similarity search.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generates a vector embedding for an image.
    /// Uses GENERIC_INDEX purpose and produces a 1024-dimension vector.
    /// </summary>
    /// <param name="imageBase64">The base64-encoded image data.</param>
    /// <param name="contentType">The MIME content type (image/jpeg, image/png, image/webp).</param>
    /// <returns>A 1024-dimension float array representing the image embedding.</returns>
    Task<float[]> GenerateImageEmbeddingAsync(string imageBase64, string contentType);

    /// <summary>
    /// Generates a vector embedding for a text query.
    /// Uses GENERIC_INDEX purpose and produces a 1024-dimension vector.
    /// </summary>
    /// <param name="text">The text query to embed.</param>
    /// <returns>A 1024-dimension float array representing the text embedding.</returns>
    Task<float[]> GenerateTextEmbeddingAsync(string text);
}
