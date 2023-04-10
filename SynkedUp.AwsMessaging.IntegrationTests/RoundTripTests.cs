using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.IntegrationTests;

public class RoundTripTests
{
    private IMessageSubscriber subscriber;
    private IMessagePublisher publisher;

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
    }
    
    [Test]
    public async Task When_publishing_and_subscribing_to_messages()
    {
        var topic = new Topic("aws-messaging", "test", 0);
        var subscription = new Subscription(topic, "aws-messaging", "integration-tests");
        var message = new Message<TestData>(topic, new TestData
        {
            IntData = 1,
            StringData = "hello world"
        });
        var receivedMessageIds = new ConcurrentQueue<string>();

        await subscriber.SubscribeAsync<TestData>(subscription, x =>
        {
            receivedMessageIds.Enqueue(x.MessageId);
            return Task.CompletedTask;
        });
        
        await publisher.PublishAsync(message);

        await Task.Delay(5000);
        subscriber.Dispose();
        
        Assert.That(receivedMessageIds.Count, Is.GreaterThan(0));
        Assert.That(receivedMessageIds, Does.Contain(message.MessageId));
    }
}