using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using PhotoSharingApp.Api.Models;
using Xunit;

namespace PhotoSharingApp.Tests.Unit.Search;

/// <summary>
/// Property-based tests for search result rendering completeness.
/// Validates: Requirements 4.4, 6.3
/// </summary>
public class ResultRenderingPropertyTests
{
    private static readonly string[] ValidContentTypes = { "image/jpeg", "image/png", "image/webp" };

    /// <summary>
    /// Property 6: Search result rendering includes all required fields.
    /// For any search result, the rendered output SHALL include a thumbnail URL and all
    /// associated metadata fields (filename, content type, file size, upload timestamp)
    /// and the similarity score.
    /// **Validates: Requirements 4.4, 6.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SearchResult_Contains_All_Required_Fields()
    {
        var searchResultGen = CreateSearchResultGenerator();
        var arb = Arb.From(searchResultGen);

        return Prop.ForAll(arb, result =>
        {
            // Verify PhotoId is non-null and non-empty
            var photoIdValid = !string.IsNullOrEmpty(result.PhotoId);

            // Verify Filename is non-null and non-empty
            var filenameValid = !string.IsNullOrEmpty(result.Filename);

            // Verify ContentType is non-null and non-empty
            var contentTypeValid = !string.IsNullOrEmpty(result.ContentType);

            // Verify FileSize is positive
            var fileSizeValid = result.FileSize > 0;

            // Verify UploadedAt is set (not default)
            var uploadedAtValid = result.UploadedAt != default;

            // Verify SimilarityScore is in valid range [0, 1]
            var similarityScoreValid = result.SimilarityScore >= 0.0 && result.SimilarityScore <= 1.0;

            // Verify ThumbnailUrl is non-null and non-empty
            var thumbnailUrlValid = !string.IsNullOrEmpty(result.ThumbnailUrl);

            return Prop.Label(photoIdValid, "PhotoId must be non-null and non-empty")
                .And(Prop.Label(filenameValid, "Filename must be non-null and non-empty"))
                .And(Prop.Label(contentTypeValid, "ContentType must be non-null and non-empty"))
                .And(Prop.Label(fileSizeValid, "FileSize must be positive"))
                .And(Prop.Label(uploadedAtValid, "UploadedAt must not be default"))
                .And(Prop.Label(similarityScoreValid, $"SimilarityScore must be in [0,1], was {result.SimilarityScore}"))
                .And(Prop.Label(thumbnailUrlValid, "ThumbnailUrl must be non-null and non-empty"));
        });
    }

    /// <summary>
    /// Property 6 (SearchResponse level): For any SearchResponse, all results contain required fields
    /// and the count matches the results array length.
    /// **Validates: Requirements 4.4, 6.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SearchResponse_Results_All_Have_Required_Fields()
    {
        var searchResponseGen = CreateSearchResponseGenerator();
        var arb = Arb.From(searchResponseGen);

        return Prop.ForAll(arb, response =>
        {
            // Count must match results length
            var countMatches = response.Count == response.Results.Length;

            // Every result must have all required fields populated
            var allResultsValid = response.Results.All(r =>
                !string.IsNullOrEmpty(r.PhotoId) &&
                !string.IsNullOrEmpty(r.Filename) &&
                !string.IsNullOrEmpty(r.ContentType) &&
                r.FileSize > 0 &&
                r.UploadedAt != default &&
                r.SimilarityScore >= 0.0 && r.SimilarityScore <= 1.0 &&
                !string.IsNullOrEmpty(r.ThumbnailUrl));

            return Prop.Label(countMatches, $"Count ({response.Count}) must match Results.Length ({response.Results.Length})")
                .And(Prop.Label(allResultsValid, "All results must have all required fields populated"));
        });
    }

    private static Gen<SearchResult> CreateSearchResultGenerator()
    {
        var filenameGen = Gen.Select(
            Gen.Where(
                Gen.ArrayOf(Gen.Elements("abcdefghijklmnopqrstuvwxyz0123456789_-.".ToCharArray())),
                arr => arr.Length >= 1 && arr.Length <= 50),
            chars => new string(chars) + ".jpg");

        var contentTypeGen = Gen.Elements(ValidContentTypes);

        var fileSizeGen = Gen.Select(
            Gen.Choose(1, 6_291_456),
            size => (long)size);

        var uploadedAtGen = Gen.Select(
            Gen.Choose(1_600_000_000, 1_750_000_000),
            unixSeconds => DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime);

        var similarityScoreGen = Gen.Select(
            Gen.Choose(0, 1000),
            n => n / 1000.0);

        // Combine generators using nested Zip (2-arg version returns tuple)
        var part1Gen = Gen.Zip(filenameGen, contentTypeGen);
        var part2Gen = Gen.Zip(fileSizeGen, uploadedAtGen);
        var combinedGen = Gen.Zip(Gen.Zip(part1Gen, part2Gen), similarityScoreGen);

        return combinedGen.Select(parts =>
        {
            var combined = parts.Item1;
            var score = parts.Item2;
            var namePart = combined.Item1;
            var sizePart = combined.Item2;
            var filename = namePart.Item1;
            var contentType = namePart.Item2;
            var fileSize = sizePart.Item1;
            var uploadedAt = sizePart.Item2;
            return new SearchResult
            {
                PhotoId = Guid.NewGuid().ToString(),
                Filename = filename,
                ContentType = contentType,
                FileSize = fileSize,
                UploadedAt = uploadedAt,
                SimilarityScore = score,
                ThumbnailUrl = $"https://photos-bucket.s3.amazonaws.com/photos/{Guid.NewGuid()}.jpg?X-Amz-Signature=abc123"
            };
        });
    }

    private static Gen<SearchResponse> CreateSearchResponseGenerator()
    {
        var resultGen = CreateSearchResultGenerator();

        return Gen.Select(
            Gen.Where(Gen.ArrayOf(resultGen), arr => arr.Length >= 0 && arr.Length <= 20),
            results => new SearchResponse
            {
                Results = results,
                Count = results.Length
            });
    }
}
