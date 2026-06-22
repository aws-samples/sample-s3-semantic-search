using Amazon.CDK;

namespace PhotoSharingApp.Infra;

public class Program
{
    public static void Main(string[] args)
    {
        var app = new App();

        new BackendStack(app, "PhotoSharingAppBackendStack", new StackProps
        {
            Description = "Photo Sharing App - Backend Infrastructure (S3, API Gateway, Lambda)"
        });

        new FrontendStack(app, "PhotoSharingAppFrontendStack", new StackProps
        {
            Description = "Photo Sharing App - Frontend Hosting (S3, CloudFront)"
        });

        app.Synth();
    }
}
