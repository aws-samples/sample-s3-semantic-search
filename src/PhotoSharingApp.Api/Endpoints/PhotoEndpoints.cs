using System.Text.Json;
using PhotoSharingApp.Api.Interfaces;
using PhotoSharingApp.Api.Models;
using PhotoSharingApp.Api.Services;
using PhotoSharingApp.Api.Validators;

namespace PhotoSharingApp.Api.Endpoints;

public static class PhotoEndpoints
{
    public static void MapPhotoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/photos");

        // POST /photos - Upload a photo
        group.MapPost("/", UploadPhotoAsync)
             .DisableAntiforgery();

        // POST /photos/initiate-upload - Get presigned URL(s) for direct S3 upload
        group.MapPost("/initiate-upload", InitiateUploadAsync);

        // GET /photos/search?q=... - Search photos by text
        group.MapGet("/search", SearchPhotosAsync);

        // POST /photos/search-by-image - Search photos by image similarity
        group.MapPost("/search-by-image", SearchByImageAsync)
             .DisableAntiforgery();

        // GET /photos/{id} - Redirect to a presigned URL for a photo
        group.MapGet("/{id}", GetPhotoAsync);
    }

    private static async Task<IResult> InitiateUploadAsync(
        HttpContext httpContext,
        IFileValidator validator,
        IPhotoStore photoStore,
        ILogger<Program> logger)
    {
        try
        {
            JsonElement body;
            try
            {
                body = await httpContext.Request.ReadFromJsonAsync<JsonElement>();
            }
            catch (JsonException)
            {
                return Results.BadRequest(new ErrorResponse { Error = "Invalid JSON request body." });
            }

            // Detect batch vs single-file request by checking for "files" property
            if (body.TryGetProperty("files", out var filesElement) ||
                body.TryGetProperty("Files", out filesElement))
            {
                return await HandleBatchInitiateUploadAsync(filesElement, validator, photoStore, logger);
            }

            return await HandleSingleInitiateUploadAsync(body, validator, photoStore, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error during initiate-upload");
            return Results.Problem("Upload initiation failed. Please try again.", statusCode: 500);
        }
    }

    private static async Task<IResult> HandleSingleInitiateUploadAsync(
        JsonElement body,
        IFileValidator validator,
        IPhotoStore photoStore,
        ILogger<Program> logger)
    {
        var request = DeserializeSingleRequest(body);
        if (request is null)
        {
            return Results.BadRequest(new ErrorResponse { Error = "Invalid request. Provide filename, contentType, and fileSize." });
        }

        var validationError = ValidateSingleFile(request, validator);
        if (validationError is not null)
        {
            return validationError;
        }

        var metadata = MetadataExtractor.Extract(request.Filename, request.FileSize, request.ContentType);
        var presignedUrl = await photoStore.GeneratePresignedPutUrlAsync(metadata.S3Key, request.ContentType, request.FileSize);

        logger.LogInformation("Initiated upload for photo {PhotoId} ({Filename})", metadata.PhotoId, metadata.Filename);

        return Results.Ok(new InitiateUploadResponse
        {
            PhotoId = metadata.PhotoId,
            UploadUrl = presignedUrl.Url,
            ExpiresAt = presignedUrl.ExpiresAt,
            S3Key = metadata.S3Key
        });
    }

    private static async Task<IResult> HandleBatchInitiateUploadAsync(
        JsonElement filesElement,
        IFileValidator validator,
        IPhotoStore photoStore,
        ILogger<Program> logger)
    {
        if (filesElement.ValueKind != JsonValueKind.Array)
        {
            return Results.BadRequest(new ErrorResponse { Error = "The 'files' field must be an array." });
        }

        var fileEntries = new List<InitiateUploadRequest>();
        foreach (var item in filesElement.EnumerateArray())
        {
            var req = DeserializeSingleRequest(item);
            if (req is null)
            {
                return Results.BadRequest(new ErrorResponse { Error = "Invalid file entry. Each file must have filename, contentType, and fileSize." });
            }
            fileEntries.Add(req);
        }

        // Validate batch size: must be 1-10 files
        if (fileEntries.Count == 0)
        {
            return Results.BadRequest(new ErrorResponse { Error = "At least 1 file is required." });
        }

        if (fileEntries.Count > 10)
        {
            return Results.BadRequest(new ErrorResponse { Error = "Maximum batch size of 10 files exceeded." });
        }

        // All-or-nothing validation: validate all files before generating any presigned URLs
        foreach (var file in fileEntries)
        {
            var validationError = ValidateSingleFile(file, validator);
            if (validationError is not null)
            {
                return validationError;
            }
        }

        // All files passed validation; generate presigned URLs
        var uploads = new List<InitiateUploadResponse>();
        foreach (var file in fileEntries)
        {
            var metadata = MetadataExtractor.Extract(file.Filename, file.FileSize, file.ContentType);
            var presignedUrl = await photoStore.GeneratePresignedPutUrlAsync(metadata.S3Key, file.ContentType, file.FileSize);

            logger.LogInformation("Initiated batch upload for photo {PhotoId} ({Filename})", metadata.PhotoId, metadata.Filename);

            uploads.Add(new InitiateUploadResponse
            {
                PhotoId = metadata.PhotoId,
                UploadUrl = presignedUrl.Url,
                ExpiresAt = presignedUrl.ExpiresAt,
                S3Key = metadata.S3Key
            });
        }

        return Results.Ok(new BatchInitiateUploadResponse
        {
            Uploads = uploads.ToArray()
        });
    }

    private static InitiateUploadRequest? DeserializeSingleRequest(JsonElement element)
    {
        string? filename = null;
        string? contentType = null;
        long? fileSize = null;

        if (element.TryGetProperty("filename", out var fProp) || element.TryGetProperty("Filename", out fProp))
        {
            filename = fProp.GetString();
        }

        if (element.TryGetProperty("contentType", out var ctProp) || element.TryGetProperty("ContentType", out ctProp))
        {
            contentType = ctProp.GetString();
        }

        if (element.TryGetProperty("fileSize", out var fsProp) || element.TryGetProperty("FileSize", out fsProp))
        {
            if (fsProp.ValueKind == JsonValueKind.Number && fsProp.TryGetInt64(out var size))
            {
                fileSize = size;
            }
        }

        if (filename is null || contentType is null || fileSize is null)
        {
            return null;
        }

        return new InitiateUploadRequest
        {
            Filename = filename,
            ContentType = contentType,
            FileSize = fileSize.Value
        };
    }

    private static IResult? ValidateSingleFile(InitiateUploadRequest request, IFileValidator validator)
    {
        // Validate filename (non-empty, ≤255 chars)
        if (!validator.ValidateFilename(request.Filename))
        {
            return Results.BadRequest(new ErrorResponse
            {
                Error = request.Filename.Length > 255
                    ? "Filename must be 255 characters or fewer."
                    : "Filename is required."
            });
        }

        // Validate extension-contentType match
        if (!validator.ValidateExtensionContentTypeMatch(request.Filename, request.ContentType))
        {
            return Results.BadRequest(new ErrorResponse
            {
                Error = $"Unsupported format. File extension must match content type. Supported: .jpg/.jpeg (image/jpeg), .png (image/png), .webp (image/webp)."
            });
        }

        // Validate fileSize (1–26,214,400)
        if (!validator.ValidatePresignedUploadSize(request.FileSize))
        {
            return Results.BadRequest(new ErrorResponse
            {
                Error = request.FileSize < 1
                    ? "File size must be at least 1 byte."
                    : "File size exceeds the 25 MB limit."
            });
        }

        return null;
    }

    private static async Task<IResult> UploadPhotoAsync(
        HttpRequest request,
        IFileValidator validator,
        IPhotoStore photoStore,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        ILogger<Program> logger)
    {
        try
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new ErrorResponse { Error = "Expected multipart/form-data." });
            }

            var form = await request.ReadFormAsync();
            var files = form.Files;
            if (files.Count == 0)
            {
                return Results.BadRequest(new ErrorResponse { Error = "No file provided." });
            }

            if (files.Count > 10)
            {
                return Results.BadRequest(new ErrorResponse { Error = "Maximum 10 photos per upload." });
            }

            // Validate all files first before processing any
            var validatedFiles = new List<(IFormFile File, byte[] Data)>();
            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                if (!validator.ValidateFormat(file.FileName, file.ContentType))
                {
                    logger.LogWarning("Rejected upload due to unsupported format: {Filename}", file.FileName);
                    return Results.BadRequest(new ErrorResponse { Error = $"'{file.FileName}' is not a supported format. Please select JPEG, PNG, or WebP images." });
                }

                if (!validator.ValidateSize(file.Length))
                {
                    logger.LogWarning("Rejected upload due to file size: {Filename} {Size}", file.FileName, file.Length);
                    return Results.BadRequest(new ErrorResponse { Error = $"'{file.FileName}' exceeds the 6 MB limit." });
                }

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var fileData = ms.ToArray();

                if (!validator.ValidateImageData(fileData, file.ContentType))
                {
                    logger.LogWarning("Rejected upload due to invalid image data: {Filename}", file.FileName);
                    return Results.BadRequest(new ErrorResponse { Error = $"'{file.FileName}' is not a valid image." });
                }

                validatedFiles.Add((file, fileData));
            }

            if (validatedFiles.Count == 0)
            {
                return Results.BadRequest(new ErrorResponse { Error = "No valid files provided." });
            }

            // Process all files: store in S3, generate embeddings in parallel, index in S3 Vectors
            var uploadTasks = validatedFiles.Select(async vf =>
            {
                var metadata = MetadataExtractor.Extract(vf.File.FileName, vf.Data.Length, vf.File.ContentType);
                logger.LogInformation("Uploading photo {PhotoId} ({Filename})", metadata.PhotoId, metadata.Filename);

                // Store photo in S3
                await photoStore.PutObjectAsync(metadata.S3Key, vf.Data, vf.File.ContentType);

                // Generate image embedding (parallel across files)
                var imageBase64 = Convert.ToBase64String(vf.Data);
                var embedding = await embeddingService.GenerateImageEmbeddingAsync(imageBase64, vf.File.ContentType);

                // Store vector with metadata
                await vectorStore.PutVectorsAsync(metadata, embedding);

                logger.LogInformation("Photo uploaded successfully: {PhotoId}", metadata.PhotoId);

                return new UploadResponse
                {
                    PhotoId = metadata.PhotoId,
                    Filename = metadata.Filename,
                    ContentType = metadata.ContentType,
                    FileSize = metadata.FileSize,
                    UploadedAt = metadata.UploadedAt,
                    S3Key = metadata.S3Key
                };
            });

            var results = await Task.WhenAll(uploadTasks);

            // Return single result for single file (backward compatible), array for multiple
            if (results.Length == 1)
            {
                return Results.Created($"/photos/{results[0].PhotoId}", results[0]);
            }

            return Results.Created("/photos", results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error during photo upload");
            return Results.Problem("Upload failed. Please try again.", statusCode: 500);
        }
    }

    private static async Task<IResult> SearchPhotosAsync(
        string? q,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        IPhotoStore photoStore,
        ILogger<Program> logger)
    {
        try
        {
            if (!SearchQueryValidator.IsValid(q))
            {
                return Results.BadRequest(new ErrorResponse { Error = "Please enter a search term." });
            }

            logger.LogInformation("Searching photos with query: {Query}", q);

            var embedding = await embeddingService.GenerateTextEmbeddingAsync(q!);
            var vectorResults = await vectorStore.QueryVectorsAsync(embedding);

            var results = new List<SearchResult>();
            foreach (var r in vectorResults.OrderByDescending(x => x.SimilarityScore))
            {
                var thumbnailUrl = await photoStore.GeneratePresignedUrlAsync(r.Metadata.S3Key);
                results.Add(new SearchResult
                {
                    PhotoId = r.Metadata.PhotoId,
                    Filename = r.Metadata.Filename,
                    ContentType = r.Metadata.ContentType,
                    FileSize = r.Metadata.FileSize,
                    UploadedAt = r.Metadata.UploadedAt,
                    SimilarityScore = r.SimilarityScore,
                    ThumbnailUrl = thumbnailUrl
                });
            }

            logger.LogInformation("Search returned {Count} results", results.Count);

            return Results.Ok(new SearchResponse
            {
                Results = results.ToArray(),
                Count = results.Count
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search failed");
            return Results.Problem("Search could not be completed. Please try again.", statusCode: 500);
        }
    }

    private static async Task<IResult> SearchByImageAsync(
        HttpRequest request,
        IFileValidator validator,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        IPhotoStore photoStore,
        ILogger<Program> logger)
    {
        try
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new ErrorResponse { Error = "Expected multipart/form-data." });
            }

            var form = await request.ReadFormAsync();
            var file = form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new ErrorResponse { Error = "Please select an image to search with." });
            }

            if (!validator.ValidateFormat(file.FileName, file.ContentType))
            {
                return Results.BadRequest(new ErrorResponse { Error = "Please select a JPEG, PNG, or WebP image." });
            }

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileData = ms.ToArray();

            logger.LogInformation("Searching by image: {Filename} ({Size} bytes)", file.FileName, fileData.Length);

            // Generate image embedding for the query image
            var imageBase64 = Convert.ToBase64String(fileData);
            var embedding = await embeddingService.GenerateImageEmbeddingAsync(imageBase64, file.ContentType);

            // Query S3 Vector Bucket for similar photos
            var vectorResults = await vectorStore.QueryVectorsAsync(embedding);

            var results = new List<SearchResult>();
            foreach (var r in vectorResults.OrderByDescending(x => x.SimilarityScore))
            {
                var thumbnailUrl = await photoStore.GeneratePresignedUrlAsync(r.Metadata.S3Key);
                results.Add(new SearchResult
                {
                    PhotoId = r.Metadata.PhotoId,
                    Filename = r.Metadata.Filename,
                    ContentType = r.Metadata.ContentType,
                    FileSize = r.Metadata.FileSize,
                    UploadedAt = r.Metadata.UploadedAt,
                    SimilarityScore = r.SimilarityScore,
                    ThumbnailUrl = thumbnailUrl
                });
            }

            logger.LogInformation("Image search returned {Count} results", results.Count);

            return Results.Ok(new SearchResponse
            {
                Results = results.ToArray(),
                Count = results.Count
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Image search failed");
            return Results.Problem("Image search could not be completed. Please try again.", statusCode: 500);
        }
    }

    private static async Task<IResult> GetPhotoAsync(
        string id,
        IVectorStore vectorStore,
        IPhotoStore photoStore,
        ILogger<Program> logger)
    {
        try
        {
            var metadata = await vectorStore.GetVectorsAsync(id);
            if (metadata is null)
            {
                logger.LogWarning("Photo not found: {PhotoId}", id);
                return Results.NotFound(new ErrorResponse { Error = "Photo not found." });
            }

            var presignedUrl = await photoStore.GeneratePresignedUrlAsync(metadata.S3Key);
            return Results.Redirect(presignedUrl, permanent: false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving photo: {PhotoId}", id);
            return Results.Problem("Could not load the photo. Please try again.", statusCode: 500);
        }
    }
}
