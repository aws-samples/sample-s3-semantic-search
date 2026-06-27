using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Notifications;
using Amazon.CDK.AWS.S3Vectors;
using Amazon.CDK.AWS.SQS;
using Amazon.CDK.AwsApigatewayv2Integrations;
using Constructs;

namespace PhotoSharingApp.Infra;

/// <summary>
/// CDK stack defining the backend infrastructure for the Photo Sharing App.
/// Single Lambda function (Minimal API) handles all routes via ASP.NET Core hosting.
/// </summary>
public class BackendStack : Stack
{
    public BackendStack(Construct scope, string id, string cloudFrontDomain, IStackProps? props = null) : base(scope, id, props)
    {
        // Allowed origin for CORS - derived from the CloudFront distribution domain
        var allowedOrigin = $"https://{cloudFrontDomain}";

        // S3 bucket for photo storage
        var photosBucket = new Bucket(this, "PhotosBucket", new BucketProps
        {
            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = true,
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            Encryption = BucketEncryption.S3_MANAGED,
            Cors = new[] {
                new CorsRule
                {
                    AllowedMethods = new[] { HttpMethods.PUT },
                    AllowedOrigins = new[] { allowedOrigin, "http://localhost:3000" },
                    AllowedHeaders = new[] { "Content-Type" },
                    ExposedHeaders = new[] { "ETag" }
                },
                new CorsRule
                {
                    AllowedMethods = new[] { HttpMethods.GET },
                    AllowedOrigins = new[] { allowedOrigin, "http://localhost:3000" },
                    AllowedHeaders = new[] { "*" }
                }
            }
        });

        // S3 Vector Bucket for metadata and embeddings
        // Use account-specific name to prevent bucket squatting
        var vectorBucketName = $"photo-sharing-vectors-{this.Account}-{this.Region}";

        var vectorBucket = new CfnVectorBucket(this, "VectorBucket", new CfnVectorBucketProps
        {
            VectorBucketName = vectorBucketName
        });

        // Vector Index for photo embeddings (1024 dimensions, cosine distance)
        var vectorIndex = new CfnIndex(this, "PhotoEmbeddingsIndex", new CfnIndexProps
        {
            VectorBucketName = vectorBucketName,
            IndexName = "photo-embeddings",
            Dimension = 1024,
            DistanceMetric = "cosine",
            DataType = "float32"
        });
        vectorIndex.AddDependency(vectorBucket);

        // Single API Lambda function (.NET 10 Minimal API + Amazon.Lambda.AspNetCoreServer)
        var apiFunction = new Function(this, "ApiFunction", new FunctionProps
        {
            Runtime = Runtime.DOTNET_10,
            Handler = "PhotoSharingApp.Api",
            Code = Code.FromAsset("../src/PhotoSharingApp.Api/bin/Release/net10.0/publish"),
            MemorySize = 1024,
            Timeout = Duration.Seconds(30),
            Tracing = Tracing.ACTIVE,
            Environment = new Dictionary<string, string>
            {
                ["PHOTOS_BUCKET_NAME"] = photosBucket.BucketName,
                ["VECTOR_BUCKET_NAME"] = vectorBucketName,
                ["VECTOR_INDEX_NAME"] = "photo-embeddings",
                ["BEDROCK_MODEL_ID"] = "amazon.nova-2-multimodal-embeddings-v1:0",
                ["ALLOWED_ORIGIN"] = allowedOrigin,
                // OpenTelemetry collector layer (ADOT) reads these
                ["OTEL_SERVICE_NAME"] = "snapfind-api",
                ["OTEL_RESOURCE_ATTRIBUTES"] = "service.namespace=SnapFind"
            },
            Description = "SnapFind API - handles upload, search, and photo retrieval"
        });

        // Grant permissions to the API Lambda
        photosBucket.GrantPut(apiFunction);
        photosBucket.GrantRead(apiFunction);

        apiFunction.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[] { "bedrock:InvokeModel" },
            Resources = new[] { $"arn:aws:bedrock:{this.Region}::foundation-model/amazon.nova-2-multimodal-embeddings-v1:0" }
        }));

        apiFunction.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[] { "s3vectors:PutVectors", "s3vectors:QueryVectors", "s3vectors:GetVectors" },
            Resources = new[]
            {
                $"arn:aws:s3vectors:{this.Region}:{this.Account}:bucket/{vectorBucket.VectorBucketName}",
                $"arn:aws:s3vectors:{this.Region}:{this.Account}:bucket/{vectorBucket.VectorBucketName}/*"
            }
        }));

        // Lambda integration
        var apiIntegration = new HttpLambdaIntegration("ApiIntegration", apiFunction);

        // API Gateway HTTP API with CORS
        var httpApi = new HttpApi(this, "PhotoSharingApi", new HttpApiProps
        {
            ApiName = "PhotoSharingApi",
            Description = "SnapFind HTTP API",
            CorsPreflight = new CorsPreflightOptions
            {
                AllowOrigins = new[] { allowedOrigin },
                AllowMethods = new[] { CorsHttpMethod.GET, CorsHttpMethod.POST, CorsHttpMethod.OPTIONS },
                AllowHeaders = new[] { "Content-Type", "Authorization" }
            }
        });

        // Catch-all route → API Lambda (ASP.NET handles routing)
        httpApi.AddRoutes(new AddRoutesOptions
        {
            Path = "/{proxy+}",
            Methods = new[] { Amazon.CDK.AWS.Apigatewayv2.HttpMethod.ANY },
            Integration = apiIntegration
        });

        // SQS Dead Letter Queue for failed processing events (14-day retention)
        var processingDlq = new Queue(this, "ProcessingDlq", new QueueProps
        {
            RetentionPeriod = Duration.Days(14)
        });

        // Processing Lambda function - triggered by S3 ObjectCreated events
        var processingFunction = new Function(this, "ProcessingFunction", new FunctionProps
        {
            Runtime = Runtime.DOTNET_10,
            Handler = "PhotoSharingApp.Processing::PhotoSharingApp.Processing.PhotoProcessingFunction::FunctionHandler",
            Code = Code.FromAsset("../src/PhotoSharingApp.Processing/bin/Release/net10.0/publish"),
            MemorySize = 1024,
            Timeout = Duration.Seconds(60),
            DeadLetterQueue = processingDlq,
            DeadLetterQueueEnabled = true,
            RetryAttempts = 2,
            Environment = new Dictionary<string, string>
            {
                ["PHOTOS_BUCKET_NAME"] = photosBucket.BucketName,
                ["VECTOR_BUCKET_NAME"] = vectorBucketName,
                ["VECTOR_INDEX_NAME"] = "photo-embeddings",
                ["BEDROCK_MODEL_ID"] = "amazon.nova-2-multimodal-embeddings-v1:0"
            },
            Description = "SnapFind Processing - generates embeddings from uploaded photos"
        });

        // Grant Processing Lambda permissions
        photosBucket.GrantRead(processingFunction);

        processingFunction.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[] { "bedrock:InvokeModel" },
            Resources = new[] { $"arn:aws:bedrock:{this.Region}::foundation-model/amazon.nova-2-multimodal-embeddings-v1:0" }
        }));

        processingFunction.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[] { "s3vectors:PutVectors", "s3vectors:GetVectors" },
            Resources = new[]
            {
                $"arn:aws:s3vectors:{this.Region}:{this.Account}:bucket/{vectorBucket.VectorBucketName}",
                $"arn:aws:s3vectors:{this.Region}:{this.Account}:bucket/{vectorBucket.VectorBucketName}/*"
            }
        }));

        // S3 event notification: trigger Processing Lambda on ObjectCreated in photos/ prefix
        photosBucket.AddEventNotification(
            EventType.OBJECT_CREATED,
            new LambdaDestination(processingFunction),
            new NotificationKeyFilter { Prefix = "photos/" }
        );

        // Stack outputs
        new CfnOutput(this, "ApiUrl", new CfnOutputProps
        {
            Value = httpApi.Url ?? "",
            Description = "API Gateway HTTP API URL"
        });

        new CfnOutput(this, "PhotosBucketName", new CfnOutputProps
        {
            Value = photosBucket.BucketName,
            Description = "S3 bucket name for photo storage"
        });

        new CfnOutput(this, "VectorBucketName", new CfnOutputProps
        {
            Value = vectorBucketName,
            Description = "S3 Vector Bucket name for embeddings"
        });
    }
}
