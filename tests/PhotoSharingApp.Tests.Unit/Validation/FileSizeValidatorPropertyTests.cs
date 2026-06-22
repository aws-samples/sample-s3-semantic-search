namespace PhotoSharingApp.Tests.Unit.Validation;

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using PhotoSharingApp.Api.Validators;

/// <summary>
/// Property-based tests for file size validation correctness.
/// **Validates: Requirements 2.3, 2.4**
/// </summary>
public class FileSizeValidatorPropertyTests
{
    private const long MaxFileSizeBytes = 6_291_456; // 6 MB

    private readonly FileValidator _validator = new();

    /// <summary>
    /// Property 2: Size validation correctness
    /// For any file size value, the validation function SHALL accept the file if and only if
    /// the size is less than or equal to 6,291,456 bytes. Files exceeding this threshold SHALL be rejected.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property FilesAtOrBelowMaxSize_AreAccepted()
    {
        var validSizeGen = Gen.Select(Gen.Choose(0, (int)MaxFileSizeBytes), i => (long)i);
        var arb = Arb.From(validSizeGen);

        return Prop.ForAll(arb, size =>
        {
            var result = _validator.ValidateSize(size);
            return Prop.Label(result, $"Expected size {size} to be accepted (≤ {MaxFileSizeBytes})");
        });
    }

    [Property(MaxTest = 200)]
    public Property FilesAboveMaxSize_AreRejected()
    {
        var invalidSizeGen = Gen.Select(Gen.Choose((int)MaxFileSizeBytes + 1, int.MaxValue), i => (long)i);
        var arb = Arb.From(invalidSizeGen);

        return Prop.ForAll(arb, size =>
        {
            var result = _validator.ValidateSize(size);
            return Prop.Label(!result, $"Expected size {size} to be rejected (> {MaxFileSizeBytes})");
        });
    }

    [Property(MaxTest = 200)]
    public Property SizesAroundBoundary_AreCorrectlyClassified()
    {
        var boundaryGen = Gen.Select(
            Gen.Choose((int)MaxFileSizeBytes - 1_000_000, (int)MaxFileSizeBytes + 1_000_000),
            i => (long)i);
        var arb = Arb.From(boundaryGen);

        return Prop.ForAll(arb, size =>
        {
            var result = _validator.ValidateSize(size);
            var expected = size <= MaxFileSizeBytes;
            return Prop.Label(result == expected,
                $"Size {size}: expected {(expected ? "accepted" : "rejected")}, got {(result ? "accepted" : "rejected")}");
        });
    }

    [Property(MaxTest = 100)]
    public Property NegativeSizes_AreRejected()
    {
        var negativeSizeGen = Gen.Select(Gen.Choose(int.MinValue, -1), i => (long)i);
        var arb = Arb.From(negativeSizeGen);

        return Prop.ForAll(arb, size =>
        {
            var result = _validator.ValidateSize(size);
            return Prop.Label(!result, $"Expected negative size {size} to be rejected");
        });
    }

    [Property(MaxTest = 100)]
    public Property LargeSizesBeyondIntMax_AreRejected()
    {
        var largeSizeGen = Gen.Select(Gen.Choose(1, 1_000_000), i => (long)int.MaxValue + i);
        var arb = Arb.From(largeSizeGen);

        return Prop.ForAll(arb, size =>
        {
            var result = _validator.ValidateSize(size);
            return Prop.Label(!result, $"Expected very large size {size} to be rejected");
        });
    }
}
