using PhotoSharingApp.Api.Models;

namespace PhotoSharingApp.Api.Services;

/// <summary>
/// Extracts and constructs photo metadata from upload parameters.
/// </summary>
public static class MetadataExtractor
{
    private const int MaxFilenameLength = 255;

    /// <summary>
    /// Creates a <see cref="PhotoMetadata"/> record from the provided upload parameters.
    /// Generates a new PhotoId (GUID), truncates filename to 255 characters,
    /// sets UploadedAt to UTC now with second precision, and derives the S3 key.
    /// </summary>
    /// <param name="filename">Original filename of the uploaded file.</param>
    /// <param name="fileSize">File size in bytes.</param>
    /// <param name="contentType">MIME content type (e.g. image/jpeg).</param>
    /// <returns>A fully populated <see cref="PhotoMetadata"/> record.</returns>
    public static PhotoMetadata Extract(string filename, long fileSize, string contentType)
    {
        var photoId = Guid.NewGuid().ToString();

        // Truncate filename to 255 characters if longer
        var truncatedFilename = filename.Length > MaxFilenameLength
            ? filename[..MaxFilenameLength]
            : filename;

        // UTC now with second precision (truncate milliseconds)
        var now = DateTime.UtcNow;
        var uploadedAt = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Utc);

        // Derive extension from the original filename
        var extension = Path.GetExtension(filename).TrimStart('.').ToLowerInvariant();

        // Generate S3 key in format: photos/{photoId}.{extension}
        var s3Key = $"photos/{photoId}.{extension}";

        return new PhotoMetadata
        {
            PhotoId = photoId,
            Filename = truncatedFilename,
            UploadedAt = uploadedAt,
            FileSize = fileSize,
            ContentType = contentType,
            S3Key = s3Key
        };
    }
}
