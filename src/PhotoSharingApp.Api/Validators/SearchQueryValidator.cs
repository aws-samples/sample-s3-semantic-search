namespace PhotoSharingApp.Api.Validators;

/// <summary>
/// Validates search query input to ensure non-empty, non-whitespace queries.
/// </summary>
public static class SearchQueryValidator
{
    /// <summary>
    /// Validates that the search query is non-null, non-empty, and not composed entirely of whitespace.
    /// </summary>
    /// <param name="query">The search query string to validate.</param>
    /// <returns>True if the query is valid (non-null, non-empty, contains non-whitespace characters); otherwise false.</returns>
    public static bool IsValid(string? query)
    {
        return !string.IsNullOrWhiteSpace(query);
    }
}
