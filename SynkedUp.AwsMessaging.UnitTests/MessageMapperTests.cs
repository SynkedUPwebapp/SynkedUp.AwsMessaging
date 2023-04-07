using System;
using System.Collections.Generic;
using Amazon.SQS.Model;
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
        Assert.That(result.MessageAttributes["MessageId"].DataType, Is.EqualTo("String"));
        Assert.That(result.MessageAttributes["MessageId"].StringValue, Is.EqualTo(message.MessageId));
        Assert.That(result.MessageAttributes["CorrelationId"].DataType, Is.EqualTo("String"));
        Assert.That(result.MessageAttributes["CorrelationId"].StringValue, Is.EqualTo(message.CorrelationId));
        Assert.That(result.MessageAttributes["PublishedAt"].DataType, Is.EqualTo("String"));
        Assert.That(result.MessageAttributes["PublishedAt"].StringValue, Is.EqualTo(message.PublishedAt!.Value.ToString("O")));
    }

    [Test]
    public void When_mapping_from_an_sqs_message()
    {
        var topic = new Topic("test", "example", 1);
        var sqsMessage = new Message
        {
            Body = "sqs-message-body",
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["MessageId"] = new() { StringValue = "message-id" },
                ["CorrelationId"] = new() { StringValue = "correlation-id" },
                ["PublishedAt"] = new() { StringValue = "2023-04-07T01:38:00-0600" }
            }
        };
        var deserialized = new TestData { Data = "test-data" };
        GetMock<IMessageSerializer>().Setup(x => x.Deserialize<TestData>(sqsMessage.Body)).Returns(deserialized);

        var result = ClassUnderTest.FromSqsMessage<TestData>(topic, sqsMessage);
        
        Assert.That(result.Topic, Is.SameAs(topic));
        Assert.That(result.Body, Is.SameAs(deserialized));
        Assert.That(result.MessageId, Is.EqualTo("message-id"));
        Assert.That(result.CorrelationId, Is.EqualTo("correlation-id"));
        Assert.That(result.PublishedAt, Is.EqualTo(DateTimeOffset.Parse("2023-04-07T01:38:00-0600")));
        Assert.That(result.ReceivedAt.HasValue, Is.True);
        Assert.That(result.ReceivedAt, Is.EqualTo(DateTimeOffset.UtcNow).Within(Seconds(1)));
    }
    
    [Test]
    public void When_mapping_from_an_sqs_message_and_there_are_no_message_attributes()
    {
        var topic = new Topic("test", "example", 1);
        var sqsMessage = new Message
        {
            Body = "sqs-message-body",
            MessageAttributes = null
        };
        var deserialized = new TestData { Data = "test-data" };
        GetMock<IMessageSerializer>().Setup(x => x.Deserialize<TestData>(sqsMessage.Body)).Returns(deserialized);

        var result = ClassUnderTest.FromSqsMessage<TestData>(topic, sqsMessage);
        
        Assert.That(result.Topic, Is.SameAs(topic));
        Assert.That(result.Body, Is.SameAs(deserialized));
        Assert.That(result.MessageId, Is.Not.Empty);
        Assert.That(result.CorrelationId, Is.Empty);
        Assert.That(result.PublishedAt, Is.Null);
        Assert.That(result.ReceivedAt.HasValue, Is.True);
        Assert.That(result.ReceivedAt, Is.EqualTo(DateTimeOffset.UtcNow).Within(Seconds(1)));
    }
    
    [Test]
    public void When_mapping_from_an_sqs_message_and_deserialization_returns_null()
    {
        var topic = new Topic("test", "example", 1);
        var sqsMessage = new Message
        {
            Body = "sqs-message-body",
            MessageAttributes = null
        };
        GetMock<IMessageSerializer>().Setup(x => x.Deserialize<TestData>(sqsMessage.Body)).ReturnsNull();

        var exception = Assert.Catch(() => ClassUnderTest.FromSqsMessage<TestData>(topic, sqsMessage));
        
        Assert.That(exception!.Message, Is.EqualTo($"Error deserializing message on topic: {topic}"));
    }
    
    [Test]
    public void When_mapping_from_an_sqs_message_and_deserialization_throws()
    {
        var topic = new Topic("test", "example", 1);
        var sqsMessage = new Message
        {
            Body = "sqs-message-body",
            MessageAttributes = null
        };
        var deserializationException = new Exception("test-exception");
        GetMock<IMessageSerializer>().Setup(x => x.Deserialize<TestData>(sqsMessage.Body)).Throws(deserializationException);

        var exception = Assert.Catch(() => ClassUnderTest.FromSqsMessage<TestData>(topic, sqsMessage));
        
        Assert.That(exception!.Message, Is.EqualTo($"Error deserializing message on topic: {topic}"));
        Assert.That(exception.InnerException, Is.SameAs(deserializationException));
    }
}