using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace SynkedUp.AwsMessaging;

internal interface IQueueUrlRetriever
{
    Task<string> GetQueueUrlAndCreateIfNecessary(Subscription subscription,  CancellationToken cancellationToken);
    Task<string> GetDeadLetterQueueUrl(Subscription subscription, CancellationToken cancellationToken);
}

internal class QueueUrlRetriever : IQueueUrlRetriever
{
    private readonly ISubscriberConfig config;
    private readonly IAmazonSQS sqsClient;
    private readonly ITopicArnCache topicArnCache;
    private readonly IQueueCreator queueCreator;
    private readonly IRetryingTopicSubscriber retryingTopicSubscriber;

    public QueueUrlRetriever(ISubscriberConfig config,
        IAmazonSQS sqsClient,
        ITopicArnCache topicArnCache,
        IQueueCreator queueCreator,
        IRetryingTopicSubscriber retryingTopicSubscriber)
    {
        this.config = config;
        this.sqsClient = sqsClient;
        this.topicArnCache = topicArnCache;
        this.queueCreator = queueCreator;
        this.retryingTopicSubscriber = retryingTopicSubscriber;
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
            var topicArn = await topicArnCache.GetTopicArn(config.Environment, subscription.Topic);
            var deadLetterQueueName = subscription.EnvironmentDeadLetterName(config.Environment);
            var deadLetterQueueArn = await queueCreator.CreateDeadLetterQueue(deadLetterQueueName, cancellationToken);
            var queueUrl = await queueCreator.CreateQueue(queueName, deadLetterQueueArn, cancellationToken);
            await retryingTopicSubscriber.SubscribeToTopic(topicArn, queueUrl);
            return queueUrl;
        }
    }

    public async Task<string> GetDeadLetterQueueUrl(Subscription subscription, CancellationToken cancellationToken)
    {
        var deadLetterQueueName = subscription.EnvironmentDeadLetterName(config.Environment);
        var response = await sqsClient.GetQueueUrlAsync(deadLetterQueueName, cancellationToken);
        return response.QueueUrl;
    }
}