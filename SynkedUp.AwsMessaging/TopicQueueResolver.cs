using System.Collections.Concurrent;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace SynkedUp.AwsMessaging;

internal interface ITopicQueueResolver
{
    Task<IReadOnlyList<SubscribedQueue>> GetSubscribedQueues(Topic topic, CancellationToken cancellationToken);
    void Invalidate(Topic topic);
}

internal record SubscribedQueue(string Name, string Url);

internal class TopicQueueResolver : ITopicQueueResolver
{
    internal static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string SqsProtocol = "sqs";

    private readonly IPublisherConfig config;
    private readonly ITopicArnCache topicArnCache;
    private readonly IAmazonSimpleNotificationService snsClient;
    private readonly IAmazonSQS sqsClient;
    private readonly IClock clock;
    private readonly ConcurrentDictionary<string, CachedQueues> cachedQueuesByTopic = new();
    private readonly ConcurrentDictionary<string, string> queueUrlsByArn = new();

    public TopicQueueResolver(IPublisherConfig config,
        ITopicArnCache topicArnCache,
        IAmazonSimpleNotificationService snsClient,
        IAmazonSQS sqsClient,
        IClock clock)
    {
        this.config = config;
        this.topicArnCache = topicArnCache;
        this.snsClient = snsClient;
        this.sqsClient = sqsClient;
        this.clock = clock;
    }

    public async Task<IReadOnlyList<SubscribedQueue>> GetSubscribedQueues(Topic topic, CancellationToken cancellationToken)
    {
        var topicName = topic.ToString();
        if (cachedQueuesByTopic.TryGetValue(topicName, out var cachedQueues)
            && clock.UtcNow - cachedQueues.RetrievedAt < CacheDuration)
        {
            return cachedQueues.Queues;
        }

        var topicArn = await topicArnCache.GetTopicArn(config.Environment, topic);
        var queueArns = await GetSubscribedQueueArns(topicArn, cancellationToken);
        var queues = new List<SubscribedQueue>();
        foreach (var queueArn in queueArns)
        {
            var queueUrl = await GetQueueUrl(queueArn, cancellationToken);
            queues.Add(new SubscribedQueue(GetQueueName(queueArn), queueUrl));
        }

        cachedQueuesByTopic[topicName] = new CachedQueues(clock.UtcNow, queues);
        return queues;
    }

    public void Invalidate(Topic topic)
    {
        cachedQueuesByTopic.TryRemove(topic.ToString(), out _);
    }

    private async Task<List<string>> GetSubscribedQueueArns(string topicArn, CancellationToken cancellationToken)
    {
        var queueArns = new List<string>();
        string? nextToken = null;
        do
        {
            var response = await snsClient.ListSubscriptionsByTopicAsync(new ListSubscriptionsByTopicRequest
            {
                TopicArn = topicArn,
                NextToken = nextToken
            }, cancellationToken);

            var subscriptions = response.Subscriptions ?? new List<Amazon.SimpleNotificationService.Model.Subscription>();
            queueArns.AddRange(subscriptions
                .Where(x => string.Equals(x.Protocol, SqsProtocol, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Endpoint));
            nextToken = response.NextToken;
        } while (!string.IsNullOrEmpty(nextToken));

        return queueArns.Distinct().ToList();
    }

    private async Task<string> GetQueueUrl(string queueArn, CancellationToken cancellationToken)
    {
        if (queueUrlsByArn.TryGetValue(queueArn, out var queueUrl))
        {
            return queueUrl;
        }

        var response = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
        {
            QueueName = GetQueueName(queueArn),
            QueueOwnerAWSAccountId = GetAccountId(queueArn)
        }, cancellationToken);
        queueUrlsByArn[queueArn] = response.QueueUrl;
        return response.QueueUrl;
    }

    private static string GetQueueName(string queueArn) => GetArnPart(queueArn, 5);

    private static string GetAccountId(string queueArn) => GetArnPart(queueArn, 4);

    private static string GetArnPart(string queueArn, int index)
    {
        var parts = queueArn.Split(':');
        if (parts.Length != 6)
        {
            throw new Exception($"Unable to parse SQS queue ARN: {queueArn}");
        }
        return parts[index];
    }

    private record CachedQueues(DateTimeOffset RetrievedAt, IReadOnlyList<SubscribedQueue> Queues);
}
