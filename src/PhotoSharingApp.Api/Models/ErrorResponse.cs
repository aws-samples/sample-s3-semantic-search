namespace PhotoSharingApp.Api.Models;

/// <summary>
/// Response DTO returned for error responses (400, 404, 500).
/// </summary>
public record ErrorResponse
{
    /// <summary>Human-readable error message describing what went wrong.</summary>
    public required string Error { get; init; }
}
