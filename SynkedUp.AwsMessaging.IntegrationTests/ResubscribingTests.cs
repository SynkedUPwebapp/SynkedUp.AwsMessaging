using System.Collections.Concurrent;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.IntegrationTests;

public class ResubscribingTests
{
    private const string TopicName = "dev_aws-messaging_test_v0";
    private const string QueueName = "dev_aws-messaging_test_v0_aws-messaging_integration-tests";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private IMessageSubscriber subscriber = null!;
    private IMessagePublisher publisher = null!;
    private IAmazonSQS sqsClient = null!;
    private IAmazonSimpleNotificationService snsClient = null!;

    [SetUp]
    public void Setup()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(AppContext.BaseDirectory))
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
        var services = new ServiceCollection();
        DependencyInjectionConfig.ConfigurePublisherServices(services);
        DependencyInjectionConfig.ConfigureSubscriberServices(services);
        services.AddTransient<IPublisherConfig, TestPublisherConfig>();
        services.AddTransient<ISubscriberConfig, TestSubscriberConfig>();
        services.AddDefaultAWSOptions(configuration.GetAWSOptions());

        var serviceProvider = services.BuildServiceProvider();

        publisher = serviceProvider.GetRequiredService<IMessagePublisher>();
        subscriber = serviceProvider.GetRequiredService<IMessageSubscriber>();
        sqsClient = serviceProvider.GetRequiredService<IAmazonSQS>();
        snsClient = serviceProvider.GetRequiredService<IAmazonSimpleNotificationService>();
    }

    [Test]
    public async Task When_a_removed_subscription_is_added_again()
    {
        var topic = new Topic("aws-messaging", "test", 0);
        var subscription = new Subscription(topic, "aws-messaging", "integration-tests");
        var message = new Message<TestData>(topic, new TestData
        {
            IntData = 1,
            StringData = "resubscribed"
        });
        var receivedMessageIds = new ConcurrentQueue<string>();
        var cancellationTokenSource = new CancellationTokenSource();
        var queueArn = await GetQueueArn();
        var topicArn = GetTopicArn(queueArn);

        await RemoveSubscription(topicArn, queueArn);
        Assert.That(await FindSubscriptionArn(topicArn, queueArn), Is.Null);

        await subscriber.SubscribeAsync<TestData>(subscription, cancellationTokenSource.Token, x =>
        {
            receivedMessageIds.Enqueue(x.MessageId);
            return Task.CompletedTask;
        });

        await WaitFor(async () => await FindSubscriptionArn(topicArn, queueArn) != null);
        Assert.That(await FindSubscriptionArn(topicArn, queueArn), Is.Not.Null);

        await publisher.PublishAsync(message);

        await WaitFor(() => Task.FromResult(receivedMessageIds.Contains(message.MessageId)));
        cancellationTokenSource.Cancel();

        Assert.That(receivedMessageIds, Does.Contain(message.MessageId));
    }

    private async Task<string> GetQueueArn()
    {
        var queueUrl = (await sqsClient.GetQueueUrlAsync(QueueName)).QueueUrl;
        var response = await sqsClient.GetQueueAttributesAsync(new GetQueueAttributesRequest
        {
            QueueUrl = queueUrl,
            AttributeNames = new List<string> { QueueAttributeName.QueueArn }
        });
        return response.QueueARN;
    }

    private static string GetTopicArn(string queueArn)
    {
        var queueArnParts = queueArn.Split(':');
        return $"arn:{queueArnParts[1]}:sns:{queueArnParts[3]}:{queueArnParts[4]}:{TopicName}";
    }

    private async Task RemoveSubscription(string topicArn, string queueArn)
    {
        var subscriptionArn = await FindSubscriptionArn(topicArn, queueArn);
        if (subscriptionArn == null)
        {
            return;
        }

        await snsClient.UnsubscribeAsync(new UnsubscribeRequest { SubscriptionArn = subscriptionArn });
        await WaitFor(async () => await FindSubscriptionArn(topicArn, queueArn) == null);
    }

    private async Task<string?> FindSubscriptionArn(string topicArn, string queueArn)
    {
        string? nextToken = null;
        do
        {
            var request = new ListSubscriptionsByTopicRequest { TopicArn = topicArn };
            if (!string.IsNullOrEmpty(nextToken))
            {
                request.NextToken = nextToken;
            }

            var response = await snsClient.ListSubscriptionsByTopicAsync(request);
            var match = response.Subscriptions?.FirstOrDefault(x => x.Endpoint == queueArn);
            if (match != null)
            {
                return match.SubscriptionArn;
            }

            nextToken = response.NextToken;
        } while (!string.IsNullOrEmpty(nextToken));

        return null;
    }

    private static async Task WaitFor(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow.Add(Timeout);
        while (DateTimeOffset.UtcNow < deadline && !await condition())
        {
            await Task.Delay(500);
        }
    }
}
