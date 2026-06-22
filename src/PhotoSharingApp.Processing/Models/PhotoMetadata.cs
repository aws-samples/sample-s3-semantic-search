namespace PhotoSharingApp.Processing.Models;

/// <summary>
/// Represents metadata for an uploaded photo, stored alongside the vector embedding in the S3 Vector Bucket.
/// This record mirrors the schema used by the API project to ensure backward compatibility.
/// </summary>
public record PhotoMetadata
{
    /// <summary>Unique identifier for the photo (GUID), used as vector key.</summary>
    public required string PhotoId { get; init; }

    /// <summary>Original filename (up to 255 characters).</summary>
    public required string Filename { get; init; }

    /// <summary>Upload timestamp in UTC with second precision.</summary>
    public required DateTime UploadedAt { get; init; }

    /// <summary>File size in bytes.</summary>
    public required long FileSize { get; init; }

    /// <summary>MIME content type (image/jpeg, image/png, image/webp).</summary>
    public required string ContentType { get; init; }

    /// <summary>S3 object key in the photos bucket.</summary>
    public required string S3Key { get; init; }
}
