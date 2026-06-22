namespace PhotoSharingApp.Tests.Unit.Validation;

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using PhotoSharingApp.Api.Validators;

/// <summary>
/// Property-based tests for file format validation.
/// **Validates: Requirements 2.1, 2.2**
/// </summary>
public class FileFormatValidatorPropertyTests
{
    private readonly FileValidator _validator = new();

    // Valid extension/MIME type pairs that should be accepted
    private static readonly (string Extension, string MimeType)[] ValidPairs =
    [
        (".jpg", "image/jpeg"),
        (".jpeg", "image/jpeg"),
        (".png", "image/png"),
        (".webp", "image/webp")
    ];

    // Invalid extensions
    private static readonly string[] InvalidExtensions =
        [".gif", ".bmp", ".tiff", ".svg", ".ico", ".raw", ".pdf", ".txt", ".exe", ".doc"];

    // Invalid MIME types
    private static readonly string[] InvalidMimeTypes =
        ["image/gif", "image/bmp", "image/tiff", "application/pdf", "text/plain", "application/octet-stream"];

    // Mismatched pairs: valid extension + valid MIME but they don't correspond
    private static readonly (string Extension, string MimeType)[] MismatchedPairs =
    [
        (".jpg", "image/png"),
        (".jpg", "image/webp"),
        (".jpeg", "image/png"),
        (".jpeg", "image/webp"),
        (".png", "image/jpeg"),
        (".png", "image/webp"),
        (".webp", "image/jpeg"),
        (".webp", "image/png")
    ];

    /// <summary>
    /// Property 1: Format validation correctness
    /// For any valid extension/MIME type combination (JPEG, PNG, WebP), the validator SHALL accept the file.
    /// **Validates: Requirements 2.1, 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidCombinations_AreAlwaysAccepted()
    {
        var gen = Gen.Elements(ValidPairs);
        var arb = Arb.From(gen);

        return Prop.ForAll(arb, combo =>
        {
            var filename = $"photo{combo.Extension}";
            var result = _validator.ValidateFormat(filename, combo.MimeType);
            return Prop.Label(result, $"Expected valid combo ({filename}, {combo.MimeType}) to be accepted");
        });
    }

    /// <summary>
    /// Property 1: Format validation correctness
    /// For any file extension that is NOT one of the supported formats, the validator SHALL reject the file
    /// regardless of the MIME type provided.
    /// **Validates: Requirements 2.1, 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidExtensions_AreAlwaysRejected()
    {
        var allMimeTypes = new[] { "image/jpeg", "image/png", "image/webp" }
            .Concat(InvalidMimeTypes).ToArray();

        var gen = Gen.Zip(Gen.Elements(InvalidExtensions), Gen.Elements(allMimeTypes));
        var arb = Arb.From(gen);

        return Prop.ForAll(arb, pair =>
        {
            var (ext, mime) = pair;
            var filename = $"file{ext}";
            var result = _validator.ValidateFormat(filename, mime);
            return Prop.Label(!result, $"Expected invalid extension ({filename}, {mime}) to be rejected");
        });
    }

    /// <summary>
    /// Property 1: Format validation correctness
    /// For any MIME type that is NOT one of the supported formats, the validator SHALL reject the file
    /// regardless of the file extension provided.
    /// **Validates: Requirements 2.1, 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidMimeTypes_AreAlwaysRejected()
    {
        var allExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" }
            .Concat(InvalidExtensions).ToArray();

        var gen = Gen.Zip(Gen.Elements(allExtensions), Gen.Elements(InvalidMimeTypes));
        var arb = Arb.From(gen);

        return Prop.ForAll(arb, pair =>
        {
            var (ext, mime) = pair;
            var filename = $"file{ext}";
            var result = _validator.ValidateFormat(filename, mime);
            return Prop.Label(!result, $"Expected invalid MIME ({filename}, {mime}) to be rejected");
        });
    }

    /// <summary>
    /// Property 1: Format validation correctness
    /// For any mismatched extension/MIME type pair (e.g., .png with image/jpeg), the validator SHALL reject.
    /// **Validates: Requirements 2.1, 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MismatchedExtensionAndMimeType_AreRejected()
    {
        var gen = Gen.Elements(MismatchedPairs);
        var arb = Arb.From(gen);

        return Prop.ForAll(arb, combo =>
        {
            var filename = $"photo{combo.Extension}";
            var result = _validator.ValidateFormat(filename, combo.MimeType);
            return Prop.Label(!result, $"Expected mismatched pair ({filename}, {combo.MimeType}) to be rejected");
        });
    }

    /// <summary>
    /// Property 1: Format validation correctness
    /// For any randomly generated filename prefix combined with a valid extension/MIME pair,
    /// the validator SHALL accept the file (filename prefix does not affect validation).
    /// **Validates: Requirements 2.1, 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AnyFilenamePrefix_WithValidCombo_IsAccepted()
    {
        // Generate random alphanumeric prefixes (1-50 chars) paired with valid combos
        var prefixGen = Gen.Elements(
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-".ToCharArray());
        var prefixStringGen = Gen.ArrayOf(prefixGen);
        var filteredPrefixGen = Gen.Where(prefixStringGen, arr => arr.Length > 0);
        var prefixAsStringGen = Gen.Select(filteredPrefixGen, chars => new string(chars));

        var gen = Gen.Zip(prefixAsStringGen, Gen.Elements(ValidPairs));
        var arb = Arb.From(gen);

        return Prop.ForAll(arb, pair =>
        {
            var (prefix, combo) = pair;
            var filename = $"{prefix}{combo.Extension}";
            var result = _validator.ValidateFormat(filename, combo.MimeType);
            return Prop.Label(result, $"Expected ({filename}, {combo.MimeType}) to be accepted regardless of prefix");
        });
    }
}
