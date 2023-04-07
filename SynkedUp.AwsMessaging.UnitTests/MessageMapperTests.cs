using System;
using Emmersion.Testing;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

internal class MessageMapperTests : With_an_automocked<MessageMapper>
{
    [Test]
    public void When_mapping_a_message_to_an_sns_request()
    {
        var topic = new Topic("publisher", "test-event", 1);
        var message = new Message<TestData>(topic, new TestData { Data = "hello there" })
        {
            CorrelationId = "correlation-id",
            PublishedAt = DateTimeOffset.UtcNow
        };
        var topicArn = "topic-arn";
        var json = "{\"example\":\"json\"}";
        GetMock<IMessageSerializer>().Setup(x => x.Serialize(message.Body)).Returns(json);
        
        var result = ClassUnderTest.ToSnsRequest(topicArn, message);
        
        Assert.That(result.TopicArn, Is.EqualTo(topicArn));
        Assert.That(result.Message, Is.EqualTo(json));
        Assert.That(result.MessageAttributes["CorrelationId"].DataType, Is.EqualTo("String"));
        Assert.That(result.MessageAttributes["CorrelationId"].StringValue, Is.EqualTo(message.CorrelationId));
        Assert.That(result.MessageAttributes["PublishedAt"].DataType, Is.EqualTo("String"));
        Assert.That(result.MessageAttributes["PublishedAt"].StringValue, Is.EqualTo(message.PublishedAt!.Value.ToString("O")));
    }
}