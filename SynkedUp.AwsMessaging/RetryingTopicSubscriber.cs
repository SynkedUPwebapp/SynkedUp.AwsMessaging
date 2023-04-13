using Amazon.SimpleNotificationService;
using Amazon.SQS;

namespace SynkedUp.AwsMessaging;

internal interface IRetryingTopicSubscriber
{
    Task SubscribeToTopic(string topicArn, string queueUrl);
}

internal class RetryingTopicSubscriber : IRetryingTopicSubscriber
{
    private readonly IAmazonSimpleNotificationService snsClient;
    private readonly IAmazonSQS sqsClient;
    private readonly IDelayer delayer;

    private const int MaxAttempts = 3;
    private const int MsDelayBetweenAttempts = 1000;

    public RetryingTopicSubscriber(IAmazonSimpleNotificationService snsClient, IAmazonSQS sqsClient, IDelayer delayer)
    {
        this.snsClient = snsClient;
        this.sqsClient = sqsClient;
        this.delayer = delayer;
    }
    
    public async Task SubscribeToTopic(string topicArn, string queueUrl)
    {
        await TrySubscribeToTopic(topicArn, queueUrl, 1);
    }

    private async Task TrySubscribeToTopic(string topicArn, string queueUrl, int attempt)
    {
        try
        {
            await snsClient.SubscribeQueueAsync(topicArn, sqsClient, queueUrl);
        }
        catch (Exception)
        {
            if (attempt >= MaxAttempts)
            {
                throw;
            }

            await delayer.Delay(attempt * MsDelayBetweenAttempts);
            await TrySubscribeToTopic(topicArn, queueUrl, attempt + 1);
        }
    }
}