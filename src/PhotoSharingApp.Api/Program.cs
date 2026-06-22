using Amazon.BedrockRuntime;
using Amazon.S3;
using Amazon.S3Vectors;
using PhotoSharingApp.Api.Endpoints;
using PhotoSharingApp.Api.Interfaces;
using PhotoSharingApp.Api.Services;
using PhotoSharingApp.Api.Validators;

var builder = WebApplication.CreateBuilder(args);

// Aspire shared defaults: OpenTelemetry (logs/metrics/traces), health checks, service discovery, HTTP resilience
builder.AddServiceDefaults();

// AWS Lambda hosting (no-op when running locally).
// In Development, surface unhandled exception details in the HTTP response to aid debugging.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi, options =>
{
    options.IncludeUnhandledExceptionDetailInResponse = builder.Environment.IsDevelopment();
});

// AWS SDK clients
builder.Services.AddSingleton<IAmazonS3, AmazonS3Client>();
builder.Services.AddSingleton<IAmazonBedrockRuntime, AmazonBedrockRuntimeClient>();
builder.Services.AddSingleton<IAmazonS3Vectors, AmazonS3VectorsClient>();

// Application services
builder.Services.AddSingleton<IFileValidator, FileValidator>();
builder.Services.AddSingleton<IEmbeddingService, BedrockEmbeddingService>();

builder.Services.AddSingleton<IPhotoStore>(sp =>
{
    var s3 = sp.GetRequiredService<IAmazonS3>();
    var bucketName = Environment.GetEnvironmentVariable("PHOTOS_BUCKET_NAME");
    if (string.IsNullOrWhiteSpace(bucketName))
    {
        throw new InvalidOperationException("PHOTOS_BUCKET_NAME environment variable is required but not set.");
    }
    return new S3PhotoStore(s3, bucketName);
});

builder.Services.AddSingleton<IVectorStore>(sp =>
{
    var s3v = sp.GetRequiredService<IAmazonS3Vectors>();
    var vectorBucket = Environment.GetEnvironmentVariable("VECTOR_BUCKET_NAME");
    if (string.IsNullOrWhiteSpace(vectorBucket))
    {
        throw new InvalidOperationException("VECTOR_BUCKET_NAME environment variable is required but not set.");
    }
    return new S3VectorStore(s3v, vectorBucket);
});

// CORS for the static frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();

// Map default endpoints (health checks in dev)
app.MapDefaultEndpoints();

// Map photo endpoints
app.MapPhotoEndpoints();

app.Run();
