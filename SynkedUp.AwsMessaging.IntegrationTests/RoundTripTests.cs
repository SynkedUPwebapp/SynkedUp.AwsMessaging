﻿using System.Collections.Concurrent;
using System.Diagnostics;
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
        var cancellationTokenSource = new CancellationTokenSource();

        await subscriber.SubscribeAsync<TestData>(subscription, cancellationTokenSource.Token, x =>
        {
            receivedMessageIds.Enqueue(x.MessageId);
            return Task.CompletedTask;
        });
        
        await publisher.PublishAsync(message);

        await Task.Delay(5000);
        cancellationTokenSource.Cancel();
        
        Assert.That(receivedMessageIds.Count, Is.GreaterThan(0));
        Assert.That(receivedMessageIds, Does.Contain(message.MessageId));
    }
    
    [Test]
    public async Task When_messages_go_to_the_dead_letter_queue()
    {
        var topic = new Topic("aws-messaging", "dlq-test", 0);
        var subscription = new Subscription(topic, "aws-messaging", "integration-tests");
        var message = new Message<TestData>(topic, new TestData
        {
            IntData = 13,
            StringData = "this message should dead-letter"
        });
        var receivedMessageIds = new ConcurrentQueue<string>();
        var cancellationTokenSource = new CancellationTokenSource();

        await subscriber.SubscribeAsync<TestData>(subscription, cancellationTokenSource.Token, x =>
        {
            receivedMessageIds.Enqueue(x.MessageId);
            throw new Exception("If we cannot process the message then it should dead-letter");
        });
        
        await publisher.PublishAsync(message);

        var stopwatch = Stopwatch.StartNew();
        await Task.Run(async () =>
        {
            while (stopwatch.ElapsedMilliseconds < 30000 && receivedMessageIds.Count(x => x == message.MessageId) < 5)
            {
                await Task.Delay(100);
            }
        });
        
        Assert.That(receivedMessageIds.Where(x => x == message.MessageId).Count, Is.EqualTo(5));

        var deadLetterMessages = new ConcurrentQueue<string>();
        await subscriber.SubscribeToDeadLettersAsync(subscription, cancellationTokenSource.Token, x =>
        {
            deadLetterMessages.Enqueue(x);
            return Task.CompletedTask;
        });

        await Task.Delay(2000);
        cancellationTokenSource.Cancel();
        
        Assert.That(deadLetterMessages.Count, Is.GreaterThan(0));
        Assert.That(deadLetterMessages.Any(x => x.Contains(message.MessageId)), Is.True);
    }
    
    [Test]
    public async Task When_scheduling_and_subscribing_to_messages()
    {
        var topic = new Topic("aws-messaging", "test", 0);
        var subscription = new Subscription(topic, "aws-messaging", "integration-tests");
        var message = new Message<TestData>(topic, new TestData
        {
            IntData = 1,
            StringData = Guid.NewGuid().ToString()
        });
        var receivedMessages = new ConcurrentQueue<string>();
        var cancellationTokenSource = new CancellationTokenSource();

        await subscriber.SubscribeAsync<TestData>(subscription, cancellationTokenSource.Token, x =>
        {
            receivedMessages.Enqueue(x.Body.StringData);
            return Task.CompletedTask;
        });
        
        await publisher.ScheduleAsync(message, DateTimeOffset.UtcNow.AddSeconds(1));

        await Task.Delay(TimeSpan.FromSeconds(60));
        cancellationTokenSource.Cancel();
        
        Assert.That(receivedMessages.Count, Is.GreaterThan(0));
        Assert.That(receivedMessages, Does.Contain(message.Body.StringData));
    }
}