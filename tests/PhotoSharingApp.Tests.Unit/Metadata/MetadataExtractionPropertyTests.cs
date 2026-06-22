namespace PhotoSharingApp.Tests.Unit.Metadata;

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using PhotoSharingApp.Api.Services;

/// <summary>
/// Property-based tests for metadata extraction.
/// **Validates: Requirements 1.3, 3.1, 3.3, 3.4**
/// </summary>
public class MetadataExtractionPropertyTests
{
    private const int MaxFilenameLength = 255;

    // Valid content types for photo uploads
    private static readonly string[] ValidContentTypes =
        ["image/jpeg", "image/png", "image/webp"];

    // Valid extensions corresponding to content types
    private static readonly (string Extension, string ContentType)[] ExtensionContentTypePairs =
    [
        (".jpg", "image/jpeg"),
        (".jpeg", "image/jpeg"),
        (".png", "image/png"),
        (".webp", "image/webp")
    ];

    /// <summary>
    /// Generates a random filename between 1 and 255 characters with a valid extension.
    /// </summary>
    private static Gen<(string Filename, string ContentType)> ValidFilenameGen()
    {
        var charGen = Gen.Elements(
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_- ".ToCharArray());

        var pairGen = Gen.Elements(ExtensionContentTypePairs);

        // Generate prefix of 1 to 245 chars (leaving room for extension up to ~10 chars)
        var prefixGen = Gen.Select(
            Gen.Where(Gen.ArrayOf(charGen), arr => arr.Length >= 1 && arr.Length <= 245),
            chars => new string(chars));

        return Gen.Zip(prefixGen, pairGen)
            .Select(pair => (Filename: $"{pair.Item1}{pair.Item2.Extension}", ContentType: pair.Item2.ContentType));
    }

    /// <summary>
    /// Generates a random filename longer than 255 characters with a valid extension.
    /// </summary>
    private static Gen<(string Filename, string ContentType)> LongFilenameGen()
    {
        var charGen = Gen.Elements(
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-".ToCharArray());

        var pairGen = Gen.Elements(ExtensionContentTypePairs);

        // Generate prefix of 256+ chars (will exceed 255 with extension)
        var prefixGen = Gen.Select(
            Gen.Where(Gen.ArrayOf(charGen), arr => arr.Length >= 256),
            chars => new string(chars));

        return Gen.Zip(prefixGen, pairGen)
            .Select(pair => (Filename: $"{pair.Item1}{pair.Item2.Extension}", ContentType: pair.Item2.ContentType));
    }

    /// <summary>
    /// Generates a random file size (positive long value).
    /// </summary>
    private static Gen<long> FileSizeGen()
    {
        return Gen.Choose(1, int.MaxValue).Select(i => (long)i);
    }

    /// <summary>
    /// Property 3: Metadata extraction preserves all fields - Filename preservation
    /// For any valid filename (1-255 chars), the extracted metadata SHALL preserve the filename exactly.
    /// **Validates: Requirements 1.3, 3.1, 3.3, 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Filename_IsPreserved_WhenWithinMaxLength()
    {
        var gen = Gen.Zip(ValidFilenameGen(), FileSizeGen());
        var arb = Arb.From(gen);

        return Prop.ForAll(arb, pair =>
        {
            var ((filename, contentType), fileSize) = pair;

            var metadata = MetadataExtractor.Extract(filename, fileSize, contentType);

            // Filename within 255 chars should be preserved exactly
            var expectedFilename = filename.Length > MaxFilenameLength
                ? filename[..MaxFilenameLength]
                : filename;

            return Prop.Label(
                metadata.Filename == expectedFilename,
                $"Expected filename '{expectedFilename}' but got '{metadata.Filename}'");
        });
    }

    /// <summary>
    /// Property 3: Metadata extraction preserves all fields - Filename truncation
    /// For any filename longer than 255 characters, the extracted metadata SHALL truncate to 255 characters.
    /// **Validates: Requirements 1.3, 3.1, 3.3, 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Filename_IsTruncatedTo255Chars_WhenExceedingMaxLength()
    {
        var gen = Gen.Zip(LongFilenameGen(), FileSizeGen());
        var arb = Arb.From(gen);

        return Prop.ForAll(arb, pair =>
        {
            var ((filename, contentType), fileSize) = pair;

            var metadata = MetadataExtractor.Extract(filename, fileSize, contentType);

            return (Prop.Label(
                metadata.Filename.Length == MaxFilenameLength,
                $"Expected filename length {MaxFilenameLength} but got {metadata.Filename.Length}"))
                .And(Prop.Label(
                    metadata.Filename == filename[..MaxFilenameLength],
                    "Truncated filename should be the first 255 characters of the original"));
        });
    }

    /// <summary>
    /// Property 3: Metadata extraction preserves all fields - FileSize preservation
    /// For any file size value, the extracted metadata SHALL preserve the file size exactly.
    /// **Validates: Requirements 1.3, 3.1, 3.3, 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FileSize_IsPreservedExactly()
    {
        var gen = Gen.Zip(ValidFilenameGen(), FileSizeGen());
        var arb = Arb.From(gen);

        return Prop.ForAll(arb, pair =>
        {
            var ((filename, contentType), fileSize) = pair;

            var metadata = MetadataExtractor.Extract(filename, fileSize, contentType);

            return Prop.Label(
                metadata.FileSize == fileSize,
                $"Expected file size {fileSize} but got {metadata.FileSize}");
        });
    }

    /// <summary>
    /// Property 3: Metadata extraction preserves all fields - ContentType preservation
    /// For any valid content type, the extracted metadata SHALL preserve the content type exactly.
    /// **Validates: Requirements 1.3, 3.1, 3.3, 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ContentType_IsPreservedExactly()
    {
        var gen = Gen.Zip(ValidFilenameGen(), FileSizeGen());
        var arb = Arb.From(gen);

        return Prop.ForAll(arb, pair =>
        {
            var ((filename, contentType), fileSize) = pair;

            var metadata = MetadataExtractor.Extract(filename, fileSize, contentType);

            return Prop.Label(
                metadata.ContentType == contentType,
                $"Expected content type '{contentType}' but got '{metadata.ContentType}'");
        });
    }

    /// <summary>
    /// Property 3: Metadata extraction preserves all fields - S3Key format
    /// For any valid input, the S3Key SHALL follow the format photos/{guid}.{extension}.
    /// **Validates: Requirements 1.3, 3.1, 3.3, 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property S3Key_FollowsExpectedFormat()
    {
        var gen = Gen.Zip(ValidFilenameGen(), FileSizeGen());
        var arb = Arb.From(gen);

        return Prop.ForAll(arb, pair =>
        {
            var ((filename, contentType), fileSize) = pair;

            var metadata = MetadataExtractor.Extract(filename, fileSize, contentType);

            // S3Key should start with "photos/"
            var startsCorrectly = metadata.S3Key.StartsWith("photos/");

            // S3Key should contain the PhotoId
            var containsPhotoId = metadata.S3Key.Contains(metadata.PhotoId);

            // S3Key should end with the correct extension
            var originalExtension = Path.GetExtension(filename).TrimStart('.').ToLowerInvariant();
            var endsCorrectly = metadata.S3Key.EndsWith($".{originalExtension}");

            // S3Key should match the exact format: photos/{photoId}.{extension}
            var expectedS3Key = $"photos/{metadata.PhotoId}.{originalExtension}";
            var matchesFormat = metadata.S3Key == expectedS3Key;

            return Prop.Label(startsCorrectly, "S3Key should start with 'photos/'")
                .And(Prop.Label(containsPhotoId, "S3Key should contain the PhotoId"))
                .And(Prop.Label(endsCorrectly, $"S3Key should end with '.{originalExtension}'"))
                .And(Prop.Label(matchesFormat, $"S3Key should be '{expectedS3Key}' but got '{metadata.S3Key}'"));
        });
    }

    /// <summary>
    /// Property 3: Metadata extraction preserves all fields - PhotoId is valid GUID
    /// For any valid input, the PhotoId SHALL be a valid GUID string.
    /// **Validates: Requirements 1.3, 3.1, 3.3, 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PhotoId_IsValidGuid()
    {
        var gen = Gen.Zip(ValidFilenameGen(), FileSizeGen());
        var arb = Arb.From(gen);

        return Prop.ForAll(arb, pair =>
        {
            var ((filename, contentType), fileSize) = pair;

            var metadata = MetadataExtractor.Extract(filename, fileSize, contentType);

            var isValidGuid = Guid.TryParse(metadata.PhotoId, out _);

            return Prop.Label(isValidGuid, $"PhotoId '{metadata.PhotoId}' is not a valid GUID");
        });
    }
}
