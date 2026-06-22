namespace PhotoSharingApp.Api.Models;

/// <summary>
/// Request DTO for initiating multiple presigned uploads in a single API call.
/// </summary>
public record BatchInitiateUploadRequest
{
    /// <summary>Array of file entries to generate presigned URLs for (1 to 10 files).</summary>
    public required InitiateUploadRequest[] Files { get; init; }
}
