using PhotoSharingApp.Api.Models;

namespace PhotoSharingApp.Api.Interfaces;

/// <summary>
/// Provides operations for storing and serving photos via the S3 photos bucket.
/// </summary>
public interface IPhotoStore
{
    /// <summary>
    /// Uploads a photo to the S3 photos bucket.
    /// </summary>
    /// <param name="s3Key">The S3 object key (format: photos/{guid}.{extension}).</param>
    /// <param name="fileData">The raw photo file bytes.</param>
    /// <param name="contentType">The MIME content type of the photo.</param>
    Task PutObjectAsync(string s3Key, byte[] fileData, string contentType);

    /// <summary>
    /// Generates a presigned URL for accessing a photo without authentication.
    /// </summary>
    /// <param name="s3Key">The S3 object key.</param>
    /// <returns>A presigned URL string with a limited expiration time.</returns>
    Task<string> GeneratePresignedUrlAsync(string s3Key);

    /// <summary>
    /// Generates a presigned PUT URL for direct client-to-S3 uploads with content constraints.
    /// </summary>
    /// <param name="s3Key">The S3 object key (format: photos/{guid}.{extension}).</param>
    /// <param name="contentType">The MIME content type constraint for the upload.</param>
    /// <param name="fileSize">The maximum allowed file size in bytes for the content-length-range condition.</param>
    /// <returns>A presigned upload URL with expiration metadata.</returns>
    Task<PresignedUploadUrl> GeneratePresignedPutUrlAsync(string s3Key, string contentType, long fileSize);

    /// <summary>
    /// Downloads a photo object from the S3 photos bucket.
    /// </summary>
    /// <param name="s3Key">The S3 object key.</param>
    /// <returns>The raw bytes of the photo object.</returns>
    Task<byte[]> GetObjectAsync(string s3Key);
}
