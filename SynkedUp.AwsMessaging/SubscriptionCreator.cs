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

    public SubscriptionCreator(ISubscriberConfig config,
        IAmazonSQS sqsClient,
        ITopicArnCache topicArnCache,
        IAmazonSimpleNotificationService snsClient)
    {
        this.config = config;
        this.sqsClient = sqsClient;
        this.topicArnCache = topicArnCache;
        this.snsClient = snsClient;
    }
    
    public async Task<string> GetQueueUrlAndCreateIfNecessary(Subscription subscription,  CancellationToken cancellationToken)
    {
        if (config.Environment?.Length > EnvironmentRestrictions.MaxLength)
        {
            throw new Exception($"Environment {config.Environment} must not exceed {EnvironmentRestrictions.MaxLength} characters");
        }
        
        var queueName = $"{config.Environment}:{subscription}";
        try
        {
            var response = await sqsClient.GetQueueUrlAsync(queueName, cancellationToken);
            return response.QueueUrl;
        }
        catch (QueueDoesNotExistException)
        {
            var request = new CreateQueueRequest
            {
                QueueName = queueName
            };
            var response = await sqsClient.CreateQueueAsync(request, cancellationToken);
            var topicArn = await topicArnCache.GetTopicArn(config.Environment!, subscription.Topic);
            await snsClient.SubscribeQueueAsync(topicArn, sqsClient, response.QueueUrl);
            return response.QueueUrl;
        }
    }
}