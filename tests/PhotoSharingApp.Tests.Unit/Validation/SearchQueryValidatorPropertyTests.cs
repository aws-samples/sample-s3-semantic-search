using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using PhotoSharingApp.Api.Validators;
using Xunit;

namespace PhotoSharingApp.Tests.Unit.Validation;

/// <summary>
/// Property-based tests for SearchQueryValidator.
/// Validates: Requirements 4.6
/// </summary>
public class SearchQueryValidatorPropertyTests
{
    /// <summary>
    /// Property 4: Whitespace-only queries are rejected.
    /// For any string composed entirely of whitespace characters (spaces, tabs, newlines),
    /// the search validation function SHALL reject the query and not perform a search.
    /// **Validates: Requirements 4.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WhitespaceOnly_Queries_Are_Rejected()
    {
        var whitespaceGen = Gen.ArrayOf(Gen.Elements(' ', '\t', '\n', '\r', '\f', '\v'));
        var filteredGen = Gen.Where(whitespaceGen, arr => arr.Length > 0);
        var stringGen = Gen.Select(filteredGen, arr => new string(arr));
        var arb = Arb.From(stringGen);

        return Prop.ForAll(arb, whitespaceQuery =>
        {
            var result = SearchQueryValidator.IsValid(whitespaceQuery);
            return Prop.Label(!result, $"Expected rejection for whitespace-only query: \"{EscapeForDisplay(whitespaceQuery)}\"");
        });
    }

    /// <summary>
    /// Property 4 (complement): Non-whitespace strings are accepted.
    /// For any string that contains at least one non-whitespace character,
    /// the search validation function SHALL accept the query.
    /// **Validates: Requirements 4.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonWhitespace_Queries_Are_Accepted()
    {
        // Generate strings that contain at least one non-whitespace character
        var charGen = Gen.Elements(
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%".ToCharArray());
        var nonWhitespaceGen = Gen.Select(
            Gen.Where(Gen.ArrayOf(charGen), arr => arr.Length > 0),
            chars => new string(chars));
        var arb = Arb.From(nonWhitespaceGen);

        return Prop.ForAll(arb, query =>
        {
            var result = SearchQueryValidator.IsValid(query);
            return Prop.Label(result, $"Expected acceptance for non-whitespace query: \"{EscapeForDisplay(query)}\"");
        });
    }

    /// <summary>
    /// Null and empty strings are also rejected (edge cases supporting Property 4).
    /// **Validates: Requirements 4.6**
    /// </summary>
    [Fact]
    public void Null_Query_Is_Rejected()
    {
        Assert.False(SearchQueryValidator.IsValid(null));
    }

    [Fact]
    public void Empty_Query_Is_Rejected()
    {
        Assert.False(SearchQueryValidator.IsValid(string.Empty));
    }

    private static string EscapeForDisplay(string input)
    {
        return input
            .Replace("\t", "\\t")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\f", "\\f")
            .Replace("\v", "\\v");
    }
}
