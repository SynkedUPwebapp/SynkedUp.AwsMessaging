using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Emmersion.Testing;
using Moq;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

internal class ThrottledMessagePublisherTests : With_an_automocked<ThrottledMessagePublisher>
{
    [Test]
    public async Task When_publishing_it_waits_for_capacity_before_publishing()
    {
        var message = new Message<string>(new Topic("tests", "example-event", 1), "message-data");
        var cancellationToken = new CancellationToken();
        var calls = new List<string>();
        GetMock<IQueueCapacityMonitor>().Setup(x => x.WaitForCapacityAsync(message.Topic, cancellationToken))
            .Callback(() => calls.Add("wait"))
            .ReturnsAsync(true);
        GetMock<IMessagePublisher>().Setup(x => x.PublishAsync(message))
            .Callback(() => calls.Add("publish"))
            .Returns(Task.CompletedTask);

        var result = await ClassUnderTest.PublishAsync(message, cancellationToken);

        Assert.That(result, Is.True);
        Assert.That(calls, Is.EqualTo(new[] { "wait", "publish" }));
    }

    [Test]
    public async Task When_waiting_is_cancelled_the_message_is_not_published()
    {
        var message = new Message<string>(new Topic("tests", "example-event", 1), "message-data");
        var cancellationToken = new CancellationToken();
        GetMock<IQueueCapacityMonitor>().Setup(x => x.WaitForCapacityAsync(message.Topic, cancellationToken))
            .ReturnsAsync(false);

        var result = await ClassUnderTest.PublishAsync(message, cancellationToken);

        Assert.That(result, Is.False);
        GetMock<IMessagePublisher>().VerifyNever(x => x.PublishAsync(IsAny<Message<string>>()));
    }
}
