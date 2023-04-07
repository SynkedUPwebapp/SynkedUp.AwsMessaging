using Amazon.Runtime.SharedInterfaces;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace SynkedUp.AwsMessaging;

internal interface ISnsClientWrapper
{
    Task<PublishResponse> PublishAsync(PublishRequest request);
    Task SubscribeQueueAsync(string topicArn, ICoreAmazonSQS sqsClient, string queueUrl);
    Task<Amazon.SimpleNotificationService.Model.Topic?> FindTopicAsync(string topicName);
}

internal class SnsClientWrapper : ISnsClientWrapper
{
    private readonly AmazonSimpleNotificationServiceClient client;

    public SnsClientWrapper()
    {
        client = new AmazonSimpleNotificationServiceClient();
    }

    public async Task<PublishResponse> PublishAsync(PublishRequest request)
    {
        return await client.PublishAsync(request);
    }

    public async Task SubscribeQueueAsync(string topicArn, ICoreAmazonSQS sqsClient, string queueUrl)
    {
        await client.SubscribeQueueAsync(topicArn, sqsClient, queueUrl);
    }

    public async Task<Amazon.SimpleNotificationService.Model.Topic?> FindTopicAsync(string topicName)
    {
        return await client.FindTopicAsync(topicName);
    }
}