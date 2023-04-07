using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        GetMock<IPublisherConfig>().Setup(x => x.Environment).Returns(environment);
        GetMock<ITopicArnCache>().Setup(x => x.GetTopicArn(environment, message.Topic)).ReturnsAsync(topicArn);
        GetMock<IMessageMapper>().Setup(x => x.ToSnsRequest(topicArn, message)).Returns(publishRequest);

        await ClassUnderTest.PublishAsync(message);
        
        GetMock<ISnsClientWrapper>().Verify(x => x.PublishAsync(publishRequest));
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
        GetMock<ISnsClientWrapper>()
            .Setup(x => x.PublishAsync(IsAny<PublishRequest>()))
            .Returns(Task.Run(() =>
            {
                Thread.Sleep(150);
                return new PublishResponse();
            }));

        ClassUnderTest.OnMessagePublished += (_, args) => receivedTimings.Add(args); 
        await ClassUnderTest.PublishAsync(message);

        Assert.That(receivedTimings.Count, Is.EqualTo(1));
        Assert.That(receivedTimings[0].ElapsedMilliseconds, Is.GreaterThanOrEqualTo(100));
        Assert.That(receivedTimings[0].ElapsedMilliseconds, Is.LessThan(1000));
    }
}