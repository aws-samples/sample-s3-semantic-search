var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.PhotoSharingApp_Api>("api")
    .WithEnvironment("PHOTOS_BUCKET_NAME", builder.Configuration["AWS:PhotosBucketName"] ?? "")
    .WithEnvironment("VECTOR_BUCKET_NAME", string.IsNullOrWhiteSpace(builder.Configuration["AWS:VectorBucketName"])
        ? "photo-sharing-vectors"
        : builder.Configuration["AWS:VectorBucketName"]!)
    .WithEnvironment("AWS_REGION", builder.Configuration["AWS:Region"] ?? "us-east-1")
    .WithEnvironment("ALLOWED_ORIGIN", "http://localhost:3000")
    .WithExternalHttpEndpoints();

// Write the API URL to frontend/env-config.js so the static site picks it up
var frontendPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "frontend"));
api.OnResourceEndpointsAllocated((evt, _, _) =>
{
    var apiUrl = api.GetEndpoint("http").Url;
    var envConfig = $"window.SNAPFIND_API_URL = '{apiUrl}';\n";
    File.WriteAllText(Path.Combine(frontendPath, "env-config.js"), envConfig);
    return Task.CompletedTask;
});

builder.AddNpmApp("frontend", frontendPath, "start")
    .WithHttpEndpoint(env: "PORT", port: 3000)
    .WithExternalHttpEndpoints()
    .WaitFor(api);

builder.Build().Run();
