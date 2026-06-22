namespace PhotoSharingApp.Api.Validators;

using PhotoSharingApp.Api.Interfaces;

/// <summary>
/// Validates uploaded files for format, size, and image data integrity.
/// </summary>
public class FileValidator : IFileValidator
{
    private static readonly Dictionary<string, string> ExtensionToMimeType = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private const long MaxFileSizeBytes = 6_291_456; // 6 MB (API Gateway limit accounting for base64 inflation)

    // Magic bytes for supported image formats
    private static readonly byte[] JpegMagicBytes = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagicBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] RiffMagicBytes = [(byte)'R', (byte)'I', (byte)'F', (byte)'F'];
    private static readonly byte[] WebpMagicBytes = [(byte)'W', (byte)'E', (byte)'B', (byte)'P'];

    /// <inheritdoc />
    public bool ValidateFormat(string filename, string contentType)
    {
        if (string.IsNullOrWhiteSpace(filename) || string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var extension = Path.GetExtension(filename);

        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        // Check that the extension is one of the supported extensions
        if (!ExtensionToMimeType.TryGetValue(extension, out var expectedMimeType))
        {
            return false;
        }

        // Check that the MIME type is one of the supported MIME types
        if (!SupportedMimeTypes.Contains(contentType))
        {
            return false;
        }

        // Check that the extension and MIME type correspond to each other
        return string.Equals(expectedMimeType, contentType, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public bool ValidateSize(long fileSize)
    {
        return fileSize >= 0 && fileSize <= MaxFileSizeBytes;
    }

    /// <inheritdoc />
    public bool ValidateImageData(byte[] fileData, string contentType)
    {
        if (fileData == null || fileData.Length == 0)
            return false;

        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        var normalizedContentType = contentType.ToLowerInvariant().Trim();

        return normalizedContentType switch
        {
            "image/jpeg" => IsValidJpeg(fileData),
            "image/png" => IsValidPng(fileData),
            "image/webp" => IsValidWebp(fileData),
            _ => false
        };
    }

    private static bool IsValidJpeg(byte[] data)
    {
        if (data.Length < JpegMagicBytes.Length)
            return false;

        return data[0] == JpegMagicBytes[0]
            && data[1] == JpegMagicBytes[1]
            && data[2] == JpegMagicBytes[2];
    }

    private static bool IsValidPng(byte[] data)
    {
        if (data.Length < PngMagicBytes.Length)
            return false;

        for (int i = 0; i < PngMagicBytes.Length; i++)
        {
            if (data[i] != PngMagicBytes[i])
                return false;
        }

        return true;
    }

    private static bool IsValidWebp(byte[] data)
    {
        if (data.Length < 12)
            return false;

        // Check "RIFF" at offset 0
        for (int i = 0; i < RiffMagicBytes.Length; i++)
        {
            if (data[i] != RiffMagicBytes[i])
                return false;
        }

        // Check "WEBP" at offset 8
        for (int i = 0; i < WebpMagicBytes.Length; i++)
        {
            if (data[8 + i] != WebpMagicBytes[i])
                return false;
        }

        return true;
    }

    /// <inheritdoc />
    public bool ValidateFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return false;
        }

        return filename.Length <= 255;
    }

    /// <inheritdoc />
    public bool ValidatePresignedUploadSize(long fileSize)
    {
        return fileSize >= 1 && fileSize <= 26_214_400;
    }

    /// <inheritdoc />
    public bool ValidateExtensionContentTypeMatch(string filename, string contentType)
    {
        if (string.IsNullOrWhiteSpace(filename) || string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var lastDotIndex = filename.LastIndexOf('.');
        if (lastDotIndex < 0 || lastDotIndex == filename.Length - 1)
        {
            return false;
        }

        var extension = filename[lastDotIndex..].ToLowerInvariant();

        if (!ExtensionToMimeType.TryGetValue(extension, out var expectedMimeType))
        {
            return false;
        }

        return string.Equals(expectedMimeType, contentType, StringComparison.OrdinalIgnoreCase);
    }
}
