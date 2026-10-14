using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Emmersion.Testing;
using Moq;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

internal class TopicQueueResolverTests : With_an_automocked<TopicQueueResolver>
{
    private const string Environment = "env";
    private const string TopicArn = "topic-arn";
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly Topic topic = new("test", "test-event", 1);
    private DateTimeOffset now;

    [SetUp]
    public void SetUp()
    {
        now = Start;
        GetMock<IClock>().Setup(x => x.UtcNow).Returns(() => now);
        GetMock<IPublisherConfig>().Setup(x => x.Environment).Returns(Environment);
        GetMock<ITopicArnCache>().Setup(x => x.GetTopicArn(Environment, topic)).ReturnsAsync(TopicArn);
        GetMock<IAmazonSQS>().Setup(x => x.GetQueueUrlAsync(IsAny<GetQueueUrlRequest>(), IsAny<CancellationToken>()))
            .ReturnsAsync((GetQueueUrlRequest request, CancellationToken _) => new GetQueueUrlResponse
            {
                QueueUrl = $"https://sqs/{request.QueueOwnerAWSAccountId}/{request.QueueName}"
            });
    }

    [Test]
    public async Task When_getting_subscribed_queues_across_multiple_pages()
    {
        SetupSubscriptionPage(null, "page-2", SqsSubscription("queue-a"));
        SetupSubscriptionPage("page-2", null, SqsSubscription("queue-b"));

        var result = await ClassUnderTest.GetSubscribedQueues(topic, CancellationToken.None);

        Assert.That(result.Select(x => x.Name), Is.EqualTo(new[] { "queue-a", "queue-b" }));
        Assert.That(result.Select(x => x.Url), Is.EqualTo(new[] { "https://sqs/123456789012/queue-a", "https://sqs/123456789012/queue-b" }));
    }

    [Test]
    public async Task When_the_topic_has_subscriptions_that_are_not_sqs_queues()
    {
        SetupSubscriptionPage(null, null,
            SqsSubscription("queue-a"),
            new Amazon.SimpleNotificationService.Model.Subscription { Protocol = "lambda", Endpoint = "arn:aws:lambda:us-east-1:123456789012:function:handler" });

        var result = await ClassUnderTest.GetSubscribedQueues(topic, CancellationToken.None);

        Assert.That(result.Select(x => x.Name), Is.EqualTo(new[] { "queue-a" }));
    }

    [Test]
    public async Task When_the_cache_has_not_expired()
    {
        SetupSubscriptionPage(null, null, SqsSubscription("queue-a"));
        await ClassUnderTest.GetSubscribedQueues(topic, CancellationToken.None);
        now += TopicQueueResolver.CacheDuration - TimeSpan.FromSeconds(1);

        await ClassUnderTest.GetSubscribedQueues(topic, CancellationToken.None);

        GetMock<IAmazonSimpleNotificationService>().Verify(x => x.ListSubscriptionsByTopicAsync(IsAny<ListSubscriptionsByTopicRequest>(), IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task When_the_cache_has_expired_queue_urls_are_not_retrieved_again()
    {
        SetupSubscriptionPage(null, null, SqsSubscription("queue-a"));
        await ClassUnderTest.GetSubscribedQueues(topic, CancellationToken.None);
        now += TopicQueueResolver.CacheDuration;

        await ClassUnderTest.GetSubscribedQueues(topic, CancellationToken.None);

        GetMock<IAmazonSimpleNotificationService>().Verify(x => x.ListSubscriptionsByTopicAsync(IsAny<ListSubscriptionsByTopicRequest>(), IsAny<CancellationToken>()), Times.Exactly(2));
        GetMock<IAmazonSQS>().Verify(x => x.GetQueueUrlAsync(IsAny<GetQueueUrlRequest>(), IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task When_the_cache_is_invalidated()
    {
        SetupSubscriptionPage(null, null, SqsSubscription("queue-a"));
        await ClassUnderTest.GetSubscribedQueues(topic, CancellationToken.None);

        ClassUnderTest.Invalidate(topic);
        await ClassUnderTest.GetSubscribedQueues(topic, CancellationToken.None);

        GetMock<IAmazonSimpleNotificationService>().Verify(x => x.ListSubscriptionsByTopicAsync(IsAny<ListSubscriptionsByTopicRequest>(), IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private void SetupSubscriptionPage(string? nextToken, string? followingToken, params Amazon.SimpleNotificationService.Model.Subscription[] subscriptions)
    {
        GetMock<IAmazonSimpleNotificationService>()
            .Setup(x => x.ListSubscriptionsByTopicAsync(
                It.Is<ListSubscriptionsByTopicRequest>(request => request.TopicArn == TopicArn && request.NextToken == nextToken),
                IsAny<CancellationToken>()))
            .ReturnsAsync(new ListSubscriptionsByTopicResponse
            {
                Subscriptions = subscriptions.ToList(),
                NextToken = followingToken
            });
    }

    private static Amazon.SimpleNotificationService.Model.Subscription SqsSubscription(string queueName)
    {
        return new Amazon.SimpleNotificationService.Model.Subscription
        {
            Protocol = "sqs",
            Endpoint = $"arn:aws:sqs:us-east-1:123456789012:{queueName}"
        };
    }
}
