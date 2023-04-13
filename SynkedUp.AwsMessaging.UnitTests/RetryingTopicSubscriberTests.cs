using System;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Emmersion.Testing;
using Moq;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

internal class RetryingTopicSubscriberTests : With_an_automocked<RetryingTopicSubscriber>
{
    private const string TopicArn = "topic-arn";
    private const string QueueUrl = "queue-url";
    
    [Test]
    public async Task When_subscribing_successfully_the_first_time()
    {
        await ClassUnderTest.SubscribeToTopic(TopicArn, QueueUrl);
        
        GetMock<IAmazonSimpleNotificationService>().Verify(x => x.SubscribeQueueAsync(TopicArn, GetMock<IAmazonSQS>().Object, QueueUrl), Times.Exactly(1));
        GetMock<IDelayer>().VerifyNever(x => x.Delay(IsAny<int>()));
    }
    
    [Test]
    public async Task When_subscribing_successfully_after_one_failure()
    {
        GetMock<IAmazonSimpleNotificationService>()
            .SetupSequence(x => x.SubscribeQueueAsync(IsAny<string>(), IsAny<IAmazonSQS>(), IsAny<string>()))
            .ThrowsAsync(new Exception("Failure 1"))
            .ReturnsAsync("success");

        await ClassUnderTest.SubscribeToTopic(TopicArn, QueueUrl);
        
        GetMock<IAmazonSimpleNotificationService>().Verify(x => x.SubscribeQueueAsync(TopicArn, GetMock<IAmazonSQS>().Object, QueueUrl), Times.Exactly(2));
        GetMock<IDelayer>().Verify(x => x.Delay(1000));
        GetMock<IDelayer>().VerifyNever(x => x.Delay(2000));
        GetMock<IDelayer>().VerifyNever(x => x.Delay(3000));
    }
    
    [Test]
    public async Task When_subscribing_successfully_after_two_failures()
    {
        GetMock<IAmazonSimpleNotificationService>()
            .SetupSequence(x => x.SubscribeQueueAsync(IsAny<string>(), IsAny<IAmazonSQS>(), IsAny<string>()))
            .ThrowsAsync(new Exception("Failure 1"))
            .ThrowsAsync(new Exception("Failure 2"))
            .ReturnsAsync("success");

        await ClassUnderTest.SubscribeToTopic(TopicArn, QueueUrl);
        
        GetMock<IAmazonSimpleNotificationService>().Verify(x => x.SubscribeQueueAsync(TopicArn, GetMock<IAmazonSQS>().Object, QueueUrl), Times.Exactly(3));
        GetMock<IDelayer>().Verify(x => x.Delay(1000));
        GetMock<IDelayer>().Verify(x => x.Delay(2000));
        GetMock<IDelayer>().VerifyNever(x => x.Delay(3000));
    }
    
    [Test]
    public async Task When_subscribing_fails_all_three_times()
    {
        GetMock<IAmazonSimpleNotificationService>()
            .SetupSequence(x => x.SubscribeQueueAsync(IsAny<string>(), IsAny<IAmazonSQS>(), IsAny<string>()))
            .ThrowsAsync(new Exception("Failure 1"))
            .ThrowsAsync(new Exception("Failure 2"))
            .ThrowsAsync(new Exception("Failure 3"));

        var exception = Assert.CatchAsync(async () => await ClassUnderTest.SubscribeToTopic(TopicArn, QueueUrl));
        
        Assert.That(exception!.Message, Is.EqualTo("Failure 3"));
        GetMock<IAmazonSimpleNotificationService>().Verify(x => x.SubscribeQueueAsync(TopicArn, GetMock<IAmazonSQS>().Object, QueueUrl), Times.Exactly(3));
        GetMock<IDelayer>().Verify(x => x.Delay(1000));
        GetMock<IDelayer>().Verify(x => x.Delay(2000));
        GetMock<IDelayer>().VerifyNever(x => x.Delay(3000));
    }
}