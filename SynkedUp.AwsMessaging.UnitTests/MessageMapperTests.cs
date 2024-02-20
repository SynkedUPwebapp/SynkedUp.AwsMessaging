using System;
using System.Collections.Generic;
using Amazon.Scheduler;
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
    public void When_mapping_a_message_to_an_sns_request_without_correlation_id()
    {
        var topic = new Topic("publisher", "test-event", 1);
        var message = new Message<TestData>(topic, new TestData { Data = "hello there" })
        {
            PublishedAt = DateTimeOffset.UtcNow
        };
        var topicArn = "topic-arn";
        var json = "{\"example\":\"json\"}";
        GetMock<IMessageSerializer>().Setup(x => x.Serialize(message.Body)).Returns(json);
        
        var result = ClassUnderTest.ToSnsRequest(topicArn, message);
        
        Assert.That(result.TopicArn, Is.EqualTo(topicArn));
        Assert.That(result.Message, Is.EqualTo(json));
        Assert.That(result.MessageAttributes, Does.Not.ContainKey("CorrelationId"));
        Assert.That(result.MessageAttributes["MessageId"].DataType, Is.EqualTo("String"));
        Assert.That(result.MessageAttributes["MessageId"].StringValue, Is.EqualTo(message.MessageId));
        Assert.That(result.MessageAttributes["PublishedAt"].DataType, Is.EqualTo("String"));
        Assert.That(result.MessageAttributes["PublishedAt"].StringValue, Is.EqualTo(message.PublishedAt!.Value.ToString("O")));
    }

    [Test]
    public void When_mapping_from_an_sqs_message()
    {
        var topic = new Topic("test", "example", 1);
        var sqsMessage = new Message { Body = "sqs-message-body" };
        var envelope = new SqsMessageEnvelope
        {
            Message = "envelope-data",
            MessageAttributes = new Dictionary<string, SqsMessageAttribute>
            {
                ["MessageId"] = new() { Value = "message-id" },
                ["CorrelationId"] = new() { Value = "correlation-id" },
                ["PublishedAt"] = new() { Value = "2023-04-07T01:38:00-0600" }
            }
        };
        var deserialized = new TestData { Data = "test-data" };
        GetMock<IMessageSerializer>().Setup(x => x.Deserialize<TestData>(envelope.Message)).Returns(deserialized);
        GetMock<IMessageSerializer>().Setup(x => x.Deserialize<SqsMessageEnvelope>(sqsMessage.Body)).Returns(envelope);

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
        var sqsMessage = new Message { Body = "sqs-message-body" };
        var envelope = new SqsMessageEnvelope
        {
            Message = "envelope-data",
            MessageAttributes = null
        };
        var deserialized = new TestData { Data = "test-data" };
        GetMock<IMessageSerializer>().Setup(x => x.Deserialize<SqsMessageEnvelope>(sqsMessage.Body)).Returns(envelope);
        GetMock<IMessageSerializer>().Setup(x => x.Deserialize<TestData>(envelope.Message)).Returns(deserialized);

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
        var sqsMessage = new Message { Body = "sqs-message-body" };
        GetMock<IMessageSerializer>().Setup(x => x.Deserialize<TestData>(sqsMessage.Body)).ReturnsNull();

        var exception = Assert.Catch(() => ClassUnderTest.FromSqsMessage<TestData>(topic, sqsMessage));
        
        Assert.That(exception!.Message, Is.EqualTo($"Error deserializing message on topic {topic}; data: {sqsMessage.Body}"));
    }
    
    [Test]
    public void When_mapping_from_an_sqs_message_and_deserialization_throws()
    {
        var topic = new Topic("test", "example", 1);
        var sqsMessage = new Message { Body = "sqs-message-body" };
        var deserializationException = new Exception("test-exception");
        GetMock<IMessageSerializer>().Setup(x => x.Deserialize<SqsMessageEnvelope>(sqsMessage.Body)).Throws(deserializationException);

        var exception = Assert.Catch(() => ClassUnderTest.FromSqsMessage<TestData>(topic, sqsMessage));
        
        Assert.That(exception!.Message, Is.EqualTo($"Error deserializing message on topic {topic}; data: {sqsMessage.Body}"));
        Assert.That(exception.InnerException, Is.SameAs(deserializationException));
    }

    [Test]
    public void When_mapping_to_a_create_schedule_request()
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
        var publishAt = DateTimeOffset.UtcNow.AddDays(1);
        var roleArn = "role-arn";
        GetMock<IPublisherConfig>().Setup(x => x.SchedulerRoleArn).Returns(roleArn);
        GetMock<IPublisherConfig>().Setup(x => x.Environment).Returns("dev");
        
        var result = ClassUnderTest.ToCreateScheduleRequest(topicArn, message, publishAt);
        
        Assert.That(result.Name, Is.Not.Null);
        Assert.That(result.GroupName, Is.EqualTo("dev_scheduled_messages"));
        Assert.That(result.ScheduleExpression, Is.EqualTo($"at({publishAt:yyyy-MM-ddThh:mm:ss})"));
        Assert.That(result.ActionAfterCompletion, Is.EqualTo(ActionAfterCompletion.DELETE));
        Assert.That(result.Target.Arn, Is.EqualTo(topicArn));
        Assert.That(result.Target.Input, Is.EqualTo(json));
        Assert.That(result.Target.RoleArn, Is.EqualTo(roleArn));
        Assert.That(result.FlexibleTimeWindow.Mode, Is.EqualTo(FlexibleTimeWindowMode.OFF));
    }
}