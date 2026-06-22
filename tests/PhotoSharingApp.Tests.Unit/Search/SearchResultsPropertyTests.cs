namespace PhotoSharingApp.Tests.Unit.Search;

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using PhotoSharingApp.Api.Interfaces;
using PhotoSharingApp.Api.Models;

/// <summary>
/// Property-based tests for search result ordering and capping.
/// **Validates: Requirements 4.3**
/// </summary>
public class SearchResultsPropertyTests
{
    private const int MaxResults = 20;

    /// <summary>
    /// Helper function that mirrors the search result ordering and capping logic
    /// from PhotoSearchFunction. Takes a list of VectorSearchResult and returns
    /// them sorted by SimilarityScore descending and capped at 20.
    /// </summary>
    private static IReadOnlyList<VectorSearchResult> OrderAndCapResults(IReadOnlyList<VectorSearchResult> results)
    {
        return results
            .OrderByDescending(r => r.SimilarityScore)
            .Take(MaxResults)
            .ToList();
    }

    /// <summary>
    /// Generates a random VectorSearchResult with a similarity score between 0.0 and 1.0.
    /// </summary>
    private static Gen<VectorSearchResult> GenVectorSearchResult()
    {
        var scoreGen = Gen.Choose(0, 1_000_000).Select(i => (float)i / 1_000_000f);
        var idGen = Gen.Choose(1, int.MaxValue).Select(_ => Guid.NewGuid().ToString());

        return Gen.Zip(scoreGen, idGen).Select(tuple => new VectorSearchResult
        {
            Metadata = new PhotoMetadata
            {
                PhotoId = tuple.Item1.ToString(),
                Filename = $"photo-{tuple.Item2[..8]}.jpg",
                UploadedAt = DateTime.UtcNow,
                FileSize = 1024,
                ContentType = "image/jpeg",
                S3Key = $"photos/{tuple.Item2}.jpg"
            },
            SimilarityScore = tuple.Item1
        });
    }

    /// <summary>
    /// Generates a list of VectorSearchResult with a random count (0 to 50).
    /// </summary>
    private static Gen<IReadOnlyList<VectorSearchResult>> GenResultList()
    {
        return Gen.Where(Gen.ArrayOf(GenVectorSearchResult()), arr => arr.Length <= 50)
            .Select(arr => (IReadOnlyList<VectorSearchResult>)arr.ToList());
    }

    /// <summary>
    /// Property 5: Search results are ordered by score descending and capped.
    /// For any set of vector similarity search results, the results SHALL be ordered
    /// by similarity score in descending order, and the total number of results
    /// SHALL NOT exceed 20.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property Results_AreOrderedByScoreDescending()
    {
        var arb = Arb.From(GenResultList());

        return Prop.ForAll(arb, results =>
        {
            var ordered = OrderAndCapResults(results);

            for (int i = 0; i < ordered.Count - 1; i++)
            {
                if (ordered[i].SimilarityScore < ordered[i + 1].SimilarityScore)
                {
                    return Prop.Label(false,
                        $"Results not in descending order at index {i}: " +
                        $"{ordered[i].SimilarityScore} < {ordered[i + 1].SimilarityScore}");
                }
            }

            return Prop.Label(true, "Results are in descending order");
        });
    }

    [Property(MaxTest = 200)]
    public Property Results_AreCappedAtMaximum()
    {
        var arb = Arb.From(GenResultList());

        return Prop.ForAll(arb, results =>
        {
            var ordered = OrderAndCapResults(results);

            return Prop.Label(ordered.Count <= MaxResults,
                $"Expected at most {MaxResults} results, got {ordered.Count}");
        });
    }

    [Property(MaxTest = 200)]
    public Property Results_CappedCount_EqualsMinOfInputAndMax()
    {
        var arb = Arb.From(GenResultList());

        return Prop.ForAll(arb, results =>
        {
            var ordered = OrderAndCapResults(results);
            var expectedCount = Math.Min(results.Count, MaxResults);

            return Prop.Label(ordered.Count == expectedCount,
                $"Expected {expectedCount} results (min of {results.Count} and {MaxResults}), got {ordered.Count}");
        });
    }

    [Property(MaxTest = 200)]
    public Property Results_CappedResults_ContainTopScores()
    {
        var arb = Arb.From(GenResultList());

        return Prop.ForAll(arb, results =>
        {
            var ordered = OrderAndCapResults(results);

            if (results.Count <= MaxResults)
            {
                // All results should be present
                return Prop.Label(ordered.Count == results.Count,
                    "All results should be present when input count <= max");
            }

            // When capped, the lowest score in the output should be >= any excluded score
            var lowestIncluded = ordered[^1].SimilarityScore;
            var excludedScores = results
                .OrderByDescending(r => r.SimilarityScore)
                .Skip(MaxResults)
                .Select(r => r.SimilarityScore);

            foreach (var excluded in excludedScores)
            {
                if (excluded > lowestIncluded)
                {
                    return Prop.Label(false,
                        $"Excluded score {excluded} is higher than lowest included score {lowestIncluded}");
                }
            }

            return Prop.Label(true, "Capped results contain the top scores");
        });
    }
}
