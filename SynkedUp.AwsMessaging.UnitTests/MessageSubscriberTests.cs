using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
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
        var cancellationTokenSource = new CancellationTokenSource();
        GetMock<IQueueUrlRetriever>().Setup(x => x.GetQueueUrlAndCreateIfNecessary(subscription, cancellationTokenSource.Token))
            .ReturnsAsync(queueUrl);
        GetMock<ISubscriberConfig>().Setup(x => x.MaxNumberOfMessages).Returns(maxNumberOfMessages);
        GetMock<ISubscriberConfig>().Setup(x => x.LongPollingSeconds).Returns(waitTimeSeconds);
        GetMock<IAmazonSQS>().Setup(x => x.ReceiveMessageAsync(IsAny<ReceiveMessageRequest>(), cancellationTokenSource.Token))
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

        await ClassUnderTest.SubscribeAsync<TestData>(subscription, cancellationTokenSource.Token, message =>
        {
            messagesReceived.Add(message);
            return Task.CompletedTask;
        });
        
        GetMock<IQueueUrlRetriever>().Verify(x => x.GetQueueUrlAndCreateIfNecessary(subscription, cancellationTokenSource.Token));
        
        await Task.Delay(100);
        
        Assert.That(request!.QueueUrl, Is.EqualTo(queueUrl));
        Assert.That(request.MaxNumberOfMessages, Is.EqualTo(maxNumberOfMessages));
        Assert.That(request.WaitTimeSeconds, Is.EqualTo(waitTimeSeconds));
        
        Assert.That(messagesReceived.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(messagesReceived[0].Body.Data, Is.EqualTo("m1"));
        Assert.That(messagesReceived[1].Body.Data, Is.EqualTo("m2"));
        Assert.That(messagesReceived[2].Body.Data, Is.EqualTo("m3"));
        
        cancellationTokenSource.Cancel();
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

    [Test]
    public async Task When_getting_a_message_batch()
    {
        var request = new ReceiveMessageRequest
        {
            QueueUrl = "queue-url"
        };
        var cancellationToken = new CancellationToken();
        var messages = new ReceiveMessageResponse()
        {
            Messages = new List<Message>
            {
                new() { ReceiptHandle = "receipt-handle-1" },
                new() { ReceiptHandle = "receipt-handle-2" },
                new() { ReceiptHandle = "receipt-handle-3" }
            }
        };
        DeleteMessageBatchRequest? deleteRequest = null;
        GetMock<IAmazonSQS>().Setup(x => x.ReceiveMessageAsync(request, cancellationToken))
            .ReturnsAsync(messages);
        GetMock<IAmazonSQS>()
            .Setup(x => x.DeleteMessageBatchAsync(IsAny<DeleteMessageBatchRequest>(), cancellationToken))
            .Callback<DeleteMessageBatchRequest, CancellationToken>((x, _) => deleteRequest = x);

        await ClassUnderTest.GetMessageBatch(request, x => Task.FromResult(x.ReceiptHandle != "receipt-handle-2"), cancellationToken);
        
        GetMock<IAmazonSQS>().Verify(x => x.DeleteMessageBatchAsync(IsAny<DeleteMessageBatchRequest>(), cancellationToken));
        Assert.That(deleteRequest!.QueueUrl, Is.EqualTo(request.QueueUrl));
        Assert.That(deleteRequest.Entries.Select(x => x.ReceiptHandle), Is.EqualTo(new [] { "receipt-handle-1", "receipt-handle-3" }));
    }
    
    [Test]
    public async Task When_subscribing_to_a_dead_letter_queue_successfully()
    {
        var subscription = new Subscription(new Topic("test", "event", 1), "test", "listener");
        var queueUrl = "queue-url";
        var messagesReceived = new List<string>();
        var messagesResponse = new ReceiveMessageResponse
        {
            Messages = new List<Message>
            {
                new() { Body = "body-1" },
                new() { Body = "body-2" },
                new() { Body = "body-3" }
            }
        };
        ReceiveMessageRequest? request = null;
        var maxNumberOfMessages = 10;
        var waitTimeSeconds = 20;
        var cancellationTokenSource = new CancellationTokenSource();
        GetMock<IQueueUrlRetriever>().Setup(x => x.GetDeadLetterQueueUrl(subscription, cancellationTokenSource.Token))
            .ReturnsAsync(queueUrl);
        GetMock<ISubscriberConfig>().Setup(x => x.MaxNumberOfMessages).Returns(maxNumberOfMessages);
        GetMock<ISubscriberConfig>().Setup(x => x.LongPollingSeconds).Returns(waitTimeSeconds);
        GetMock<IAmazonSQS>().Setup(x => x.ReceiveMessageAsync(IsAny<ReceiveMessageRequest>(), cancellationTokenSource.Token))
            .Callback<ReceiveMessageRequest, CancellationToken>((x, _) => request = x)
            .Returns(async () =>
            {
                await Task.Delay(50);
                return messagesResponse;
            });

        await ClassUnderTest.SubscribeToDeadLettersAsync(subscription, cancellationTokenSource.Token, message =>
        {
            messagesReceived.Add(message);
            return Task.CompletedTask;
        });
        
        GetMock<IQueueUrlRetriever>().Verify(x => x.GetDeadLetterQueueUrl(subscription, cancellationTokenSource.Token));
        
        await Task.Delay(100);
        
        Assert.That(request!.QueueUrl, Is.EqualTo(queueUrl));
        Assert.That(request.MaxNumberOfMessages, Is.EqualTo(maxNumberOfMessages));
        Assert.That(request.WaitTimeSeconds, Is.EqualTo(waitTimeSeconds));
        
        Assert.That(messagesReceived.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(messagesReceived[0], Is.EqualTo("body-1"));
        Assert.That(messagesReceived[1], Is.EqualTo("body-2"));
        Assert.That(messagesReceived[2], Is.EqualTo("body-3"));
        
        cancellationTokenSource.Cancel();
    }
}