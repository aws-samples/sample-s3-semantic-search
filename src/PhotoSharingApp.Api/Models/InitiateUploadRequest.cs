namespace PhotoSharingApp.Api.Models;

/// <summary>
/// Request DTO for initiating a single presigned upload.
/// </summary>
public record InitiateUploadRequest
{
    /// <summary>Original filename including extension (e.g., "sunset-beach.jpg").</summary>
    public required string Filename { get; init; }

    /// <summary>MIME content type (image/jpeg, image/png, or image/webp).</summary>
    public required string ContentType { get; init; }

    /// <summary>File size in bytes (1 to 26,214,400).</summary>
    public required long FileSize { get; init; }
}
