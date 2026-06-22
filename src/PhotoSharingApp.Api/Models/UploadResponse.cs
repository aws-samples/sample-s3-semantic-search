namespace PhotoSharingApp.Api.Models;

/// <summary>
/// Response DTO returned after a successful photo upload (201 Created).
/// </summary>
public record UploadResponse
{
    public required string PhotoId { get; init; }
    public required string Filename { get; init; }
    public required string ContentType { get; init; }
    public required long FileSize { get; init; }
    public required DateTime UploadedAt { get; init; }
    public required string S3Key { get; init; }
}
