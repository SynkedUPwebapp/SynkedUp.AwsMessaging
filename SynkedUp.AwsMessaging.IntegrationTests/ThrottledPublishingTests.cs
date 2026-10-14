using System.Collections.Concurrent;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.IntegrationTests;

public class ThrottledPublishingTests
{
    private const string QueueName = "dev_aws-messaging_test_v0_aws-messaging_integration-tests";
    private IMessageSubscriber subscriber = null!;
    private IThrottledMessagePublisher throttledPublisher = null!;
    private IQueueCapacityMonitor queueCapacityMonitor = null!;
    private IAmazonSQS sqsClient = null!;

    [SetUp]
    public void Setup()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(AppContext.BaseDirectory))
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
        var services = new ServiceCollection();
        DependencyInjectionConfig.ConfigurePublisherServices(services);
        DependencyInjectionConfig.ConfigureThrottledPublisherServices(services);
        DependencyInjectionConfig.ConfigureSubscriberServices(services);
        services.AddTransient<IPublisherConfig, TestPublisherConfig>();
        services.AddTransient<ISubscriberConfig, TestSubscriberConfig>();
        services.AddTransient<IQueueCapacityMonitorConfig, TestQueueCapacityMonitorConfig>();
        services.AddDefaultAWSOptions(configuration.GetAWSOptions());

        var serviceProvider = services.BuildServiceProvider();

        subscriber = serviceProvider.GetRequiredService<IMessageSubscriber>();
        throttledPublisher = serviceProvider.GetRequiredService<IThrottledMessagePublisher>();
        queueCapacityMonitor = serviceProvider.GetRequiredService<IQueueCapacityMonitor>();
        sqsClient = serviceProvider.GetRequiredService<IAmazonSQS>();
    }

    [Test]
    public async Task When_publishing_faster_than_a_slow_subscriber_can_process()
    {
        var topic = new Topic("aws-messaging", "test", 0);
        var subscription = new Subscription(topic, "aws-messaging", "integration-tests");
        var subscriberCancellationTokenSource = new CancellationTokenSource();
        var publishCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var receivedMessageIds = new ConcurrentQueue<string>();
        var exceptions = new ConcurrentQueue<Exception>();
        var waitingEvents = new ConcurrentQueue<WaitingForCapacityArgs>();
        queueCapacityMonitor.OnException += (_, args) => exceptions.Enqueue(args.Exception);
        queueCapacityMonitor.OnWaitingForCapacity += (_, args) => waitingEvents.Enqueue(args);

        await subscriber.SubscribeAsync<TestData>(subscription, subscriberCancellationTokenSource.Token, async x =>
        {
            await Task.Delay(100);
            receivedMessageIds.Enqueue(x.MessageId);
        });
        var queueUrl = (await sqsClient.GetQueueUrlAsync(QueueName)).QueueUrl;
        await WaitForEmptyQueue(queueUrl);

        var maximumDepth = 0;
        var depthMonitorCancellationTokenSource = new CancellationTokenSource();
        var depthMonitor = Task.Run(async () =>
        {
            while (!depthMonitorCancellationTokenSource.IsCancellationRequested)
            {
                maximumDepth = Math.Max(maximumDepth, await GetDepth(queueUrl));
                await Task.Delay(1000);
            }
        });

        var publishedMessageIds = new List<string>();
        for (var i = 0; i < 300; i++)
        {
            var message = new Message<TestData>(topic, new TestData { IntData = i, StringData = "throttled" });
            var published = await throttledPublisher.PublishAsync(message, publishCancellationTokenSource.Token);
            Assert.That(published, Is.True, "Publishing was cancelled by the timeout");
            publishedMessageIds.Add(message.MessageId);
        }

        await depthMonitorCancellationTokenSource.CancelAsync();
        await depthMonitor;
        await WaitForEmptyQueue(queueUrl);
        await subscriberCancellationTokenSource.CancelAsync();

        Assert.That(exceptions, Is.Empty);
        Assert.That(waitingEvents, Is.Not.Empty);
        Assert.That(maximumDepth, Is.LessThan(150));
        Assert.That(receivedMessageIds, Is.SupersetOf(publishedMessageIds));
    }

    private async Task WaitForEmptyQueue(string queueUrl)
    {
        var timeout = DateTimeOffset.UtcNow.AddMinutes(2);
        while (DateTimeOffset.UtcNow < timeout && await GetDepth(queueUrl) > 0)
        {
            await Task.Delay(1000);
        }
    }

    private async Task<int> GetDepth(string queueUrl)
    {
        var response = await sqsClient.GetQueueAttributesAsync(new GetQueueAttributesRequest
        {
            QueueUrl = queueUrl,
            AttributeNames = new List<string>
            {
                QueueAttributeName.ApproximateNumberOfMessages,
                QueueAttributeName.ApproximateNumberOfMessagesNotVisible
            }
        });
        var attributes = response.Attributes ?? new Dictionary<string, string>();
        return attributes.Values.Sum(x => int.TryParse(x, out var number) ? number : 0);
    }
}
