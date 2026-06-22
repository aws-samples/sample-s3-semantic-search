namespace PhotoSharingApp.Api.Models;

/// <summary>
/// Response DTO returned after successfully initiating a batch of presigned uploads.
/// </summary>
public record BatchInitiateUploadResponse
{
    /// <summary>Array of presigned upload entries, one per requested file.</summary>
    public required InitiateUploadResponse[] Uploads { get; init; }
}
