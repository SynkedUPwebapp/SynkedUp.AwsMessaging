using System;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

public class TestMessageBuilderTests
{
    [Test]
    public void When_building_a_message_for_testing_with_defaults()
    {
        var message = new TestMessageBuilder<int>().Build(13);

        Assert.That(message.Body, Is.EqualTo(13));
        Assert.That(message.Topic.ToString(), Is.EqualTo("example.test-event.v1"));
        Assert.That(message.MessageId, Is.Not.Null);
        Assert.That(message.CorrelationId, Is.Empty);
        Assert.That(message.PublishedAt, Is.Null);
        Assert.That(message.ReceivedAt, Is.Null);
    }

    [Test]
    public void When_building_a_message_for_testing_with_specified_values()
    {
        var topic = new Topic("unit-tests", "message-built", 4);
        var body = new TestData { Data = "this is a test" };
        var messageId = "test-message-id";
        var correlationId = Guid.NewGuid().ToString();
        var publishedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var receivedAt = DateTimeOffset.UtcNow;

        var message = new TestMessageBuilder<TestData>()
            .WithTopic(topic)
            .WithMessageId(messageId)
            .WithCorrelationId(correlationId)
            .WithPublishedAt(publishedAt)
            .WithReceivedAt(receivedAt)
            .Build(body);

        Assert.That(message.Body, Is.SameAs(body));
        Assert.That(message.Topic.ToString(), Is.EqualTo(topic.ToString()));
        Assert.That(message.MessageId, Is.EqualTo(messageId));
        Assert.That(message.CorrelationId, Is.EqualTo(correlationId));
        Assert.That(message.PublishedAt, Is.EqualTo(publishedAt));
        Assert.That(message.ReceivedAt, Is.EqualTo(receivedAt));
    }
}