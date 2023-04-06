using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace SynkedUp.AwsMessaging;

public interface ISnsClientWrapper
{
    Task<PublishResponse> PublishAsync(PublishRequest request);
}

public class SnsClientWrapper : ISnsClientWrapper
{
    private readonly AmazonSimpleNotificationServiceClient client;

    public SnsClientWrapper()
    {
        client = new AmazonSimpleNotificationServiceClient(RegionEndpoint.USEast1);
    }

    public async Task<PublishResponse> PublishAsync(PublishRequest request)
    {
        return await client.PublishAsync(request);
    }
}