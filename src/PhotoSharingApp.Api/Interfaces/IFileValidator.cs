namespace PhotoSharingApp.Api.Interfaces;

/// <summary>
/// Validates uploaded files for format, size, and image data integrity.
/// </summary>
public interface IFileValidator
{
    /// <summary>
    /// Validates that the file extension and MIME type correspond to a supported format (JPEG, PNG, or WebP).
    /// </summary>
    /// <param name="filename">The original filename including extension.</param>
    /// <param name="contentType">The MIME content type of the file.</param>
    /// <returns>True if the format is valid; otherwise false.</returns>
    bool ValidateFormat(string filename, string contentType);

    /// <summary>
    /// Validates that the file size does not exceed the maximum allowed size (6 MB / 6,291,456 bytes).
    /// </summary>
    /// <param name="fileSize">The file size in bytes.</param>
    /// <returns>True if the size is within the allowed limit; otherwise false.</returns>
    bool ValidateSize(long fileSize);

    /// <summary>
    /// Validates that the file bytes constitute valid image data for the claimed format.
    /// </summary>
    /// <param name="fileData">The raw file bytes.</param>
    /// <param name="contentType">The claimed MIME content type.</param>
    /// <returns>True if the data is a valid image; otherwise false.</returns>
    bool ValidateImageData(byte[] fileData, string contentType);

    /// <summary>
    /// Validates that the filename is non-empty, non-whitespace, and between 1 and 255 characters in length.
    /// </summary>
    /// <param name="filename">The original filename including extension.</param>
    /// <returns>True if the filename is valid; otherwise false.</returns>
    bool ValidateFilename(string filename);

    /// <summary>
    /// Validates that the file size is within the allowed range for presigned uploads (1 to 26,214,400 bytes).
    /// </summary>
    /// <param name="fileSize">The file size in bytes.</param>
    /// <returns>True if the size is within the allowed limit; otherwise false.</returns>
    bool ValidatePresignedUploadSize(long fileSize);

    /// <summary>
    /// Validates that the file extension in the filename is consistent with the declared content type.
    /// Supported mappings: .jpg/.jpeg → image/jpeg, .png → image/png, .webp → image/webp.
    /// </summary>
    /// <param name="filename">The original filename including extension.</param>
    /// <param name="contentType">The MIME content type of the file.</param>
    /// <returns>True if the extension matches the content type; otherwise false.</returns>
    bool ValidateExtensionContentTypeMatch(string filename, string contentType);
}
