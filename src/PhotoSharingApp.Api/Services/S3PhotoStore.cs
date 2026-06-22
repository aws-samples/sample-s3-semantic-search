using Amazon.S3;
using Amazon.S3.Model;
using PhotoSharingApp.Api.Interfaces;
using PhotoSharingApp.Api.Models;

namespace PhotoSharingApp.Api.Services;

/// <summary>
/// S3-backed implementation of <see cref="IPhotoStore"/> for storing and serving photos.
/// </summary>
public class S3PhotoStore : IPhotoStore
{
    private static readonly TimeSpan PresignedUrlExpiration = TimeSpan.FromHours(1);

    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public S3PhotoStore(IAmazonS3 s3Client, string bucketName)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
    }

    /// <inheritdoc />
    public async Task PutObjectAsync(string s3Key, byte[] fileData, string contentType)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = s3Key,
            ContentType = contentType,
            InputStream = new MemoryStream(fileData)
        };

        await _s3Client.PutObjectAsync(request);
    }

    /// <inheritdoc />
    public Task<string> GeneratePresignedUrlAsync(string s3Key)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = s3Key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(PresignedUrlExpiration)
        };

        return Task.FromResult(_s3Client.GetPreSignedURL(request));
    }

    /// <inheritdoc />
    public Task<PresignedUploadUrl> GeneratePresignedPutUrlAsync(string s3Key, string contentType, long fileSize)
    {
        var expiresAt = DateTime.UtcNow.AddSeconds(300);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = s3Key,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = expiresAt
        };

        // Note: content-length-range cannot be enforced via presigned PUT URLs.
        // Size validation is handled server-side at the initiate-upload endpoint.
        // For stricter enforcement, use presigned POST with policy conditions.

        var url = _s3Client.GetPreSignedURL(request);

        return Task.FromResult(new PresignedUploadUrl
        {
            Url = url,
            ExpiresAt = expiresAt
        });
    }

    /// <inheritdoc />
    public async Task<byte[]> GetObjectAsync(string s3Key)
    {
        var request = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = s3Key
        };

        using var response = await _s3Client.GetObjectAsync(request);
        using var memoryStream = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }
}
