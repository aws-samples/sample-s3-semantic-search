namespace PhotoSharingApp.Api.Models;

/// <summary>
/// Represents a generated presigned PUT URL with its expiration time.
/// </summary>
public record PresignedUploadUrl
{
    /// <summary>The presigned S3 PUT URL.</summary>
    public required string Url { get; init; }

    /// <summary>Timestamp when the presigned URL expires.</summary>
    public required DateTime ExpiresAt { get; init; }
}
