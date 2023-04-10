using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace SynkedUp.AwsMessaging;

internal interface ISubscriptionCreator
{
    Task<string> GetQueueUrlAndCreateIfNecessary(Subscription subscription,  CancellationToken cancellationToken);
}

internal class SubscriptionCreator : ISubscriptionCreator
{
    private readonly ISubscriberConfig config;
    private readonly IAmazonSQS sqsClient;
    private readonly ITopicArnCache topicArnCache;
    private readonly IAmazonSimpleNotificationService snsClient;
    private readonly IQueueCreator queueCreator;

    public SubscriptionCreator(ISubscriberConfig config,
        IAmazonSQS sqsClient,
        ITopicArnCache topicArnCache,
        IAmazonSimpleNotificationService snsClient,
        IQueueCreator queueCreator)
    {
        this.config = config;
        this.sqsClient = sqsClient;
        this.topicArnCache = topicArnCache;
        this.snsClient = snsClient;
        this.queueCreator = queueCreator;
    }
    
    public async Task<string> GetQueueUrlAndCreateIfNecessary(Subscription subscription,  CancellationToken cancellationToken)
    {
        var queueName = subscription.EnvironmentName(config.Environment);
        try
        {
            var response = await sqsClient.GetQueueUrlAsync(queueName, cancellationToken);
            return response.QueueUrl;
        }
        catch (QueueDoesNotExistException)
        {
            var deadLetterQueueArn = await queueCreator.CreateDeadLetterQueue($"{queueName}_dl", cancellationToken);
            var queueUrl = await queueCreator.CreateQueue(queueName, deadLetterQueueArn, cancellationToken);
            var topicArn = await topicArnCache.GetTopicArn(config.Environment!, subscription.Topic);
            await snsClient.SubscribeQueueAsync(topicArn, sqsClient, queueUrl);
            return queueUrl;
        }
    }
}