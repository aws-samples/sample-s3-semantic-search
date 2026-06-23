using Amazon.CDK;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.S3;
using Constructs;

namespace PhotoSharingApp.Infra;

/// <summary>
/// CDK stack for frontend hosting: S3 bucket for static files + CloudFront distribution.
/// </summary>
public class FrontendStack : Stack
{
    /// <summary>
    /// The CloudFront distribution domain name (e.g., d1234abcdef.cloudfront.net).
    /// Used by BackendStack to configure CORS.
    /// </summary>
    public string DistributionDomainName { get; private set; }

    public FrontendStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {
        // S3 bucket for static website hosting (CloudFront uses OAI for access)
        var websiteBucket = new Bucket(this, "FrontendBucket", new BucketProps
        {
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = true
        });

        // CloudFront distribution pointing to S3 origin with Origin Access Control (OAC)
        var distribution = new Distribution(this, "FrontendDistribution", new DistributionProps
        {
            DefaultBehavior = new BehaviorOptions
            {
                Origin = S3BucketOrigin.WithOriginAccessControl(websiteBucket),
                ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS
            },
            DefaultRootObject = "index.html",
            ErrorResponses = new IErrorResponse[]
            {
                new ErrorResponse
                {
                    HttpStatus = 404,
                    ResponseHttpStatus = 200,
                    ResponsePagePath = "/index.html",
                    Ttl = Duration.Seconds(0)
                }
            }
        });

        // Expose distribution domain for cross-stack reference
        DistributionDomainName = distribution.DistributionDomainName;

        // Stack outputs
        _ = new CfnOutput(this, "FrontendBucketName", new CfnOutputProps
        {
            Value = websiteBucket.BucketName,
            Description = "S3 bucket for frontend static files"
        });

        _ = new CfnOutput(this, "DistributionDomainName", new CfnOutputProps
        {
            Value = distribution.DistributionDomainName,
            Description = "CloudFront distribution domain name"
        });

        _ = new CfnOutput(this, "DistributionId", new CfnOutputProps
        {
            Value = distribution.DistributionId,
            Description = "CloudFront distribution ID"
        });
    }
}
