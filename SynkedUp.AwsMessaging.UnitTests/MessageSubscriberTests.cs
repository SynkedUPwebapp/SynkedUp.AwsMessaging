using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Emmersion.Testing;
using Moq;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

internal class MessageSubscriberTests : With_an_automocked<MessageSubscriber>
{
    [Test]
    public async Task When_subscribing_successfully()
    {
        var subscription = new Subscription(new Topic("test", "event", 1), "test", "listener");
        var queueUrl = "queue-url";
        var messagesReceived = new List<Message<TestData>>();
        var messagesResponse = new ReceiveMessageResponse
        {
            Messages = new List<Message>
            {
                new(),
                new(),
                new()
            }
        };
        ReceiveMessageRequest? request = null;
        var maxNumberOfMessages = 10;
        var waitTimeSeconds = 20;
        GetMock<ISubscriptionCreator>().Setup(x => x.GetQueueUrlAndCreateIfNecessary(subscription, IsAny<CancellationToken>()))
            .ReturnsAsync(queueUrl);
        GetMock<ISubscriberConfig>().Setup(x => x.MaxNumberOfMessages).Returns(maxNumberOfMessages);
        GetMock<ISubscriberConfig>().Setup(x => x.LongPollingSeconds).Returns(waitTimeSeconds);
        GetMock<ISqsClientWrapper>().Setup(x => x.ReceiveMessagesAsync(IsAny<ReceiveMessageRequest>(), IsAny<CancellationToken>()))
            .Callback<ReceiveMessageRequest, CancellationToken>((x, _) => request = x)
            .Returns(async () =>
            {
                await Task.Delay(50);
                return messagesResponse;
            });
        GetMock<IMessageMapper>().SetupSequence(x => x.FromSqsMessage<TestData>(subscription.Topic, IsAny<Message>()))
            .Returns(new Message<TestData>(subscription.Topic, new TestData { Data = "m1" }))
            .Returns(new Message<TestData>(subscription.Topic, new TestData { Data = "m2" }))
            .Returns(new Message<TestData>(subscription.Topic, new TestData { Data = "m3" }));

        await ClassUnderTest.Subscribe<TestData>(subscription, message =>
        {
            messagesReceived.Add(message);
            return Task.CompletedTask;
        });
        
        GetMock<ISubscriptionCreator>().Verify(x => x.GetQueueUrlAndCreateIfNecessary(subscription, IsAny<CancellationToken>()));
        Assert.That(request!.QueueUrl, Is.EqualTo(queueUrl));
        Assert.That(request.MaxNumberOfMessages, Is.EqualTo(maxNumberOfMessages));
        Assert.That(request.WaitTimeSeconds, Is.EqualTo(waitTimeSeconds));
        
        await Task.Delay(100);
        
        Assert.That(messagesReceived.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(messagesReceived[0].Body.Data, Is.EqualTo("m1"));
        Assert.That(messagesReceived[1].Body.Data, Is.EqualTo("m2"));
        Assert.That(messagesReceived[2].Body.Data, Is.EqualTo("m3"));
    }

    [Test]
    public async Task When_processing_a_message_successfully()
    {
        var subscription = new Subscription(new Topic("test", "example", 1), "test", "listener");
        var sqsMessage = new Message();
        var mapped = new Message<TestData>(subscription.Topic, new TestData { Data = "hi" })
        {
            PublishedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            ReceivedAt = DateTimeOffset.UtcNow.AddSeconds(-5)
        };
        string processedData = "";
        MessageReceivedArgs? receivedArgs = null;
        GetMock<IMessageMapper>().Setup(x => x.FromSqsMessage<TestData>(subscription.Topic, sqsMessage)).Returns(mapped);

        ClassUnderTest.OnMessageReceived += (_, args) => receivedArgs = args;
        
        var result = await ClassUnderTest.ProcessMessage<TestData>(subscription, sqsMessage, x =>
        {
            processedData = x.Body.Data;
            return Task.CompletedTask;
        });
        
        Assert.That(result, Is.True);
        Assert.That(processedData, Is.EqualTo(mapped.Body.Data));
        
        Assert.That(receivedArgs, Is.Not.Null);
        Assert.That(receivedArgs!.Subscription, Is.EqualTo(subscription));
        Assert.That(receivedArgs.PublishedAt, Is.EqualTo(mapped.PublishedAt));
        Assert.That(receivedArgs.ReceivedAt, Is.EqualTo(mapped.ReceivedAt));
        Assert.That(receivedArgs.ProcessingTime, Is.GreaterThan(TimeSpan.Zero));
    }
    
    [Test]
    public async Task When_processing_a_message_and_the_mapper_throws()
    {
        var subscription = new Subscription(new Topic("test", "example", 1), "test", "listener");
        var sqsMessage = new Message();
        var exception = new Exception("Test exception");
        ExceptionArgs? exceptionArgs = null;
        MessageReceivedArgs? receivedArgs = null;
        GetMock<IMessageMapper>().Setup(x => x.FromSqsMessage<TestData>(subscription.Topic, sqsMessage)).Throws(exception);

        ClassUnderTest.OnException += (_, args) => exceptionArgs = args;
        ClassUnderTest.OnMessageReceived += (_, args) => receivedArgs = args;

        var result = await ClassUnderTest.ProcessMessage<TestData>(subscription, sqsMessage, _ => throw exception);
        
        Assert.That(result, Is.False);
        
        Assert.That(exceptionArgs, Is.Not.Null);
        Assert.That(exceptionArgs!.Subscription, Is.EqualTo(subscription));
        Assert.That(exceptionArgs.Exception, Is.EqualTo(exception));
        
        Assert.That(receivedArgs, Is.Not.Null);
        Assert.That(receivedArgs!.Subscription, Is.EqualTo(subscription));
        Assert.That(receivedArgs.PublishedAt, Is.Null);
        Assert.That(receivedArgs.ReceivedAt, Is.EqualTo(DateTimeOffset.UtcNow).Within(Seconds(1)));
        Assert.That(receivedArgs.ProcessingTime, Is.GreaterThan(TimeSpan.Zero));
    }
    
    [Test]
    public async Task When_processing_a_message_and_the_handler_throws()
    {
        var subscription = new Subscription(new Topic("test", "example", 1), "test", "listener");
        var sqsMessage = new Message();
        var mapped = new Message<TestData>(subscription.Topic, new TestData { Data = "hi" })
        {
            PublishedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            ReceivedAt = DateTimeOffset.UtcNow.AddSeconds(-5)
        };
        var exception = new Exception("Test exception");
        ExceptionArgs? exceptionArgs = null;
        MessageReceivedArgs? receivedArgs = null;
        GetMock<IMessageMapper>().Setup(x => x.FromSqsMessage<TestData>(subscription.Topic, sqsMessage)).Returns(mapped);

        ClassUnderTest.OnException += (_, args) => exceptionArgs = args;
        ClassUnderTest.OnMessageReceived += (_, args) => receivedArgs = args;

        var result = await ClassUnderTest.ProcessMessage<TestData>(subscription, sqsMessage, _ => throw exception);
        
        Assert.That(result, Is.False);
        
        Assert.That(exceptionArgs, Is.Not.Null);
        Assert.That(exceptionArgs!.Subscription, Is.EqualTo(subscription));
        Assert.That(exceptionArgs.Exception, Is.EqualTo(exception));
        
        Assert.That(receivedArgs, Is.Not.Null);
        Assert.That(receivedArgs!.Subscription, Is.EqualTo(subscription));
        Assert.That(receivedArgs.PublishedAt, Is.EqualTo(mapped.PublishedAt));
        Assert.That(receivedArgs.ReceivedAt, Is.EqualTo(mapped.ReceivedAt));
        Assert.That(receivedArgs.ProcessingTime, Is.GreaterThan(TimeSpan.Zero));
    }
}