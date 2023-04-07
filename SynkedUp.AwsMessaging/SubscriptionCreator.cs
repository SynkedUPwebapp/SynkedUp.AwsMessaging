using Amazon.SQS.Model;

namespace SynkedUp.AwsMessaging;

public interface ISubscriptionCreator
{
    Task<string> GetQueueUrlAndCreateIfNecessary(Subscription subscription,  CancellationToken cancellationToken);
}

public class SubscriptionCreator : ISubscriptionCreator
{
    private readonly ISubscriberConfig config;
    private readonly ISqsClientWrapper sqsClient;
    private readonly ITopicArnCache topicArnCache;
    private readonly ISnsClientWrapper snsClient;

    public SubscriptionCreator(ISubscriberConfig config,
        ISqsClientWrapper sqsClient,
        ITopicArnCache topicArnCache,
        ISnsClientWrapper snsClient)
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
            var response = await sqsClient.GetQueueUrl(queueName, cancellationToken);
            return response.QueueUrl;
        }
        catch (QueueDoesNotExistException)
        {
            var request = new CreateQueueRequest
            {
                QueueName = queueName
            };
            var response = await sqsClient.CreateQueue(request, cancellationToken);
            var topicArn = await topicArnCache.GetTopicArn(config.Environment!, subscription.Topic);
            await snsClient.SubscribeQueueAsync(topicArn, sqsClient.Client, response.QueueUrl);
            return response.QueueUrl;
        }
    }
}