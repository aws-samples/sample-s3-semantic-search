namespace PhotoSharingApp.Api.Models;

/// <summary>
/// Response DTO returned after successfully initiating a presigned upload.
/// </summary>
public record InitiateUploadResponse
{
    /// <summary>Unique identifier for the photo (GUID).</summary>
    public required string PhotoId { get; init; }

    /// <summary>Presigned S3 PUT URL for direct client upload.</summary>
    public required string UploadUrl { get; init; }

    /// <summary>Timestamp when the presigned URL expires (5 minutes from generation).</summary>
    public required DateTime ExpiresAt { get; init; }

    /// <summary>S3 object key in the format photos/{photoId}.{extension}.</summary>
    public required string S3Key { get; init; }
}
