using Amazon.CDK;

namespace PhotoSharingApp.Infra;

public class Program
{
    public static void Main(string[] args)
    {
        var app = new App();

        // Frontend first — BackendStack needs the CloudFront domain for CORS
        var frontendStack = new FrontendStack(app, "PhotoSharingAppFrontendStack", new StackProps
        {
            Description = "Photo Sharing App - Frontend Hosting (S3, CloudFront)"
        });

        new BackendStack(app, "PhotoSharingAppBackendStack", frontendStack.DistributionDomainName, new StackProps
        {
            Description = "Photo Sharing App - Backend Infrastructure (S3, API Gateway, Lambda)"
        });

        app.Synth();
    }
}
