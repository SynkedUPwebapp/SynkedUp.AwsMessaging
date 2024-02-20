using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Scheduler;
using Amazon.Scheduler.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Emmersion.Testing;
using Moq;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

internal class MessagePublisherTests : With_an_automocked<MessagePublisher>
{
    [Test]
    public async Task When_publishing_a_message()
    {
        var message = new Message<string>(new Topic("tests", "example-event", 1), "message-data");
        var environment = "env";
        var topicArn = "topic-arn";
        var publishRequest = new PublishRequest();
        Message<string>? publishedMessage = null;
        GetMock<IPublisherConfig>().Setup(x => x.Environment).Returns(environment);
        GetMock<ITopicArnCache>().Setup(x => x.GetTopicArn(environment, message.Topic)).ReturnsAsync(topicArn);
        GetMock<IMessageMapper>().Setup(x => x.ToSnsRequest(topicArn, IsAny<Message<string>>()))
            .Callback<string, Message<string>>((_, x) => publishedMessage = x)
            .Returns(publishRequest);

        await ClassUnderTest.PublishAsync(message);
        
        GetMock<IAmazonSimpleNotificationService>().Verify(x => x.PublishAsync(publishRequest, IsAny<CancellationToken>()));
        Assert.That(publishedMessage!.PublishedAt.HasValue, Is.True);
        Assert.That(publishedMessage.PublishedAt, Is.EqualTo(DateTimeOffset.UtcNow).Within(Seconds(1)));
        Assert.That(publishedMessage, Is.EqualTo(message with { PublishedAt = publishedMessage.PublishedAt }));
    }
    
    [Test]
    public async Task When_publishing_a_message_timing_data_is_emitted()
    {
        var message = new Message<string>(new Topic("tests", "example-event", 1), "message-data");
        var environment = "env";
        var topicArn = "topic-arn";
        var publishRequest = new PublishRequest();
        var receivedTimings = new List<MessagePublishedArgs>();
        GetMock<IPublisherConfig>().Setup(x => x.Environment).Returns(environment);
        GetMock<ITopicArnCache>().Setup(x => x.GetTopicArn(environment, message.Topic)).ReturnsAsync(topicArn);
        GetMock<IMessageMapper>().Setup(x => x.ToSnsRequest("", message)).Returns(publishRequest);
        GetMock<IAmazonSimpleNotificationService>()
            .Setup(x => x.PublishAsync(IsAny<PublishRequest>(), IsAny<CancellationToken>()))
            .Returns(Task.Run(() =>
            {
                Thread.Sleep(150);
                return new PublishResponse();
            }));

        ClassUnderTest.OnMessagePublished += (_, args) => receivedTimings.Add(args); 
        await ClassUnderTest.PublishAsync(message);

        Assert.That(receivedTimings.Count, Is.EqualTo(1));
        Assert.That(receivedTimings[0].Topic, Is.EqualTo(message.Topic));
        Assert.That(receivedTimings[0].Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(100)));
        Assert.That(receivedTimings[0].Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(1000)));
    }

    [Test]
    public async Task When_scheduling_a_message()
    {
        var message = new Message<string>(new Topic("tests", "example-event", 1), "message-data");
        var environment = "env";
        var topicArn = "topic-arn";
        var createScheduleRequest = new CreateScheduleRequest();
        Message<string>? publishedMessage = null;
        var publishAt = DateTimeOffset.UtcNow.AddDays(1);
        GetMock<IPublisherConfig>().Setup(x => x.Environment).Returns(environment);
        GetMock<ITopicArnCache>().Setup(x => x.GetTopicArn(environment, message.Topic)).ReturnsAsync(topicArn);
        GetMock<IMessageMapper>().Setup(x => x.ToCreateScheduleRequest(topicArn, IsAny<Message<string>>(), publishAt))
            .Callback<string, Message<string>, DateTimeOffset>((_, x, y) => publishedMessage = x)
            .Returns(createScheduleRequest);

        await ClassUnderTest.ScheduleAsync(message, publishAt);
        
        GetMock<IAmazonScheduler>().Verify(x => x.CreateScheduleAsync(createScheduleRequest, IsAny<CancellationToken>()));
        Assert.That(publishedMessage!.PublishedAt.HasValue, Is.True);
        Assert.That(publishedMessage.PublishedAt, Is.EqualTo(DateTimeOffset.UtcNow).Within(Seconds(1)));
        Assert.That(publishedMessage, Is.EqualTo(message with { PublishedAt = publishedMessage.PublishedAt }));
    }
}