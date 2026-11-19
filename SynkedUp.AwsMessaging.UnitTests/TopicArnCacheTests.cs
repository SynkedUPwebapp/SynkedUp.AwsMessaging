using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Emmersion.Testing;
using Moq;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

internal class TopicArnCacheTests : With_an_automocked<TopicArnCache>
{
    private readonly List<string?> requestedNextTokens = new();

    [SetUp]
    public void SetUp()
    {
        requestedNextTokens.Clear();
    }

    [Test]
    public async Task When_there_is_a_cache_miss_and_the_topic_is_on_the_first_page()
    {
        var topic = new Topic("test", "event", 0);
        var environment = "env";
        var topicName = topic.EnvironmentName(environment);
        var topicArn = $"arn:aws:sns:us-east-1:123456789012:{topicName}";
        SetupListTopicsPages(new ListTopicsResponse
        {
            Topics = new List<Amazon.SimpleNotificationService.Model.Topic>
            {
                new() { TopicArn = $"arn:aws:sns:us-east-1:123456789012:other_{topicName}" },
                new() { TopicArn = $"arn:aws:sns:us-east-1:123456789012:{topicName}_other" },
                new() { TopicArn = topicArn }
            },
            NextToken = "unused-token"
        });

        var result = await ClassUnderTest.GetTopicArn(environment, topic);

        Assert.That(result, Is.EqualTo(topicArn));
        Assert.That(ClassUnderTest.IsArnSet(topicName), Is.True);
        Assert.That(requestedNextTokens, Is.EqualTo(new string?[] { null }));
    }

    [Test]
    public async Task When_there_is_a_cache_miss_and_the_topic_is_on_a_later_page()
    {
        var topic = new Topic("test", "event", 0);
        var environment = "env";
        var topicName = topic.EnvironmentName(environment);
        var topicArn = $"arn:aws:sns:us-east-1:123456789012:{topicName}";
        SetupListTopicsPages(
            new ListTopicsResponse
            {
                Topics = new List<Amazon.SimpleNotificationService.Model.Topic>
                {
                    new() { TopicArn = "arn:aws:sns:us-east-1:123456789012:some-other-topic" }
                },
                NextToken = "page-2-token"
            },
            new ListTopicsResponse
            {
                Topics = null,
                NextToken = "page-3-token"
            },
            new ListTopicsResponse
            {
                Topics = new List<Amazon.SimpleNotificationService.Model.Topic>
                {
                    new() { TopicArn = topicArn }
                }
            });

        var result = await ClassUnderTest.GetTopicArn(environment, topic);

        Assert.That(result, Is.EqualTo(topicArn));
        Assert.That(ClassUnderTest.IsArnSet(topicName), Is.True);
        Assert.That(requestedNextTokens, Is.EqualTo(new[] { null, "page-2-token", "page-3-token" }));
    }

    [Test]
    public void When_there_is_a_cache_miss_and_the_topic_could_not_be_found()
    {
        var topic = new Topic("test", "event", 0);
        var environment = "env";
        var topicName = topic.EnvironmentName(environment);
        SetupListTopicsPages(
            new ListTopicsResponse
            {
                Topics = new List<Amazon.SimpleNotificationService.Model.Topic>
                {
                    new() { TopicArn = "arn:aws:sns:us-east-1:123456789012:some-other-topic" }
                },
                NextToken = "page-2-token"
            },
            new ListTopicsResponse
            {
                Topics = null,
                NextToken = ""
            });

        var exception = Assert.CatchAsync(async () => await ClassUnderTest.GetTopicArn(environment, topic));

        Assert.That(exception!.Message, Is.EqualTo($"Unable to find topic: {topicName}"));
        Assert.That(ClassUnderTest.IsArnSet(topicName), Is.False);
        Assert.That(requestedNextTokens, Is.EqualTo(new[] { null, "page-2-token" }));
    }

    [Test]
    public async Task When_there_is_a_cache_hit()
    {
        var topic = new Topic("test", "event", 0);
        var environment = "env";
        var topicName = topic.EnvironmentName(environment);
        var topicArn = "topic-arn";
        ClassUnderTest.SetArn(topicName, topicArn);
        Assert.That(ClassUnderTest.IsArnSet(topicName), Is.True);

        var result = await ClassUnderTest.GetTopicArn(environment, topic);

        Assert.That(result, Is.EqualTo(topicArn));
        GetMock<IAmazonSimpleNotificationService>().VerifyNever(x => x.ListTopicsAsync(IsAny<ListTopicsRequest>(), IsAny<CancellationToken>()));
    }

    [Test]
    public async Task When_a_found_topic_is_requested_again()
    {
        var topic = new Topic("test", "event", 0);
        var environment = "env";
        var topicName = topic.EnvironmentName(environment);
        var topicArn = $"arn:aws:sns:us-east-1:123456789012:{topicName}";
        SetupListTopicsPages(new ListTopicsResponse
        {
            Topics = new List<Amazon.SimpleNotificationService.Model.Topic>
            {
                new() { TopicArn = topicArn }
            }
        });

        await ClassUnderTest.GetTopicArn(environment, topic);
        var result = await ClassUnderTest.GetTopicArn(environment, topic);

        Assert.That(result, Is.EqualTo(topicArn));
        GetMock<IAmazonSimpleNotificationService>().Verify(x => x.ListTopicsAsync(IsAny<ListTopicsRequest>(), IsAny<CancellationToken>()), Times.Exactly(1));
    }

    private void SetupListTopicsPages(params ListTopicsResponse[] pages)
    {
        var remainingPages = new Queue<ListTopicsResponse>(pages);
        GetMock<IAmazonSimpleNotificationService>()
            .Setup(x => x.ListTopicsAsync(IsAny<ListTopicsRequest>(), IsAny<CancellationToken>()))
            .Callback<ListTopicsRequest, CancellationToken>((request, _) => requestedNextTokens.Add(request.NextToken))
            .ReturnsAsync(() => remainingPages.Dequeue());
    }
}
