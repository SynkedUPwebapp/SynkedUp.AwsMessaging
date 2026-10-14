using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Emmersion.Testing;
using Moq;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

internal class QueueCapacityMonitorTests : With_an_automocked<QueueCapacityMonitor>
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly Topic topic = new("test", "test-event", 1);
    private long nowTicks;

    [SetUp]
    public void SetUp()
    {
        nowTicks = Start.UtcTicks;
        GetMock<IClock>().Setup(x => x.UtcNow).Returns(() => new DateTimeOffset(Interlocked.Read(ref nowTicks), TimeSpan.Zero));
        GetMock<IDelayer>().Setup(x => x.Delay(IsAny<int>(), IsAny<CancellationToken>()))
            .Callback<int, CancellationToken>((milliseconds, _) => Interlocked.Add(ref nowTicks, TimeSpan.FromMilliseconds(milliseconds).Ticks))
            .Returns(Task.CompletedTask);
        GetMock<IQueueCapacityMonitorConfig>().Setup(x => x.MaximumMessageDelayMilliseconds).Returns(30000);
        GetMock<ITopicQueueResolver>().Setup(x => x.GetSubscribedQueues(topic, IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SubscribedQueue> { new("queue-a", "url-a") });
        GetMock<IQueueDepthSampler>().Setup(x => x.SampleDepths(IsAny<IReadOnlyList<SubscribedQueue>>(), IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QueueDepth> { new("queue-a", 0) });
    }

    [Test]
    public async Task When_waiting_for_capacity_the_queues_are_sampled_and_the_caller_waits_for_a_token()
    {
        var waitingEvents = new List<WaitingForCapacityArgs>();
        ClassUnderTest.OnWaitingForCapacity += (_, args) => waitingEvents.Add(args);

        var result = await ClassUnderTest.WaitForCapacityAsync(topic, CancellationToken.None);

        Assert.That(result, Is.True);
        GetMock<IQueueDepthSampler>().Verify(x => x.SampleDepths(IsAny<IReadOnlyList<SubscribedQueue>>(), IsAny<CancellationToken>()), Times.Once);
        Assert.That(waitingEvents, Is.Not.Empty);
        Assert.That(waitingEvents[0].Topic, Is.EqualTo(topic));
        Assert.That(waitingEvents[0].LimitingQueueName, Is.EqualTo("queue-a"));
        Assert.That(waitingEvents[0].WaitTime, Is.GreaterThan(TimeSpan.Zero));
    }

    [Test]
    public async Task When_the_sample_interval_passes_the_queues_are_sampled_again()
    {
        await ClassUnderTest.WaitForCapacityAsync(topic, CancellationToken.None);
        Interlocked.Add(ref nowTicks, TopicCapacityState.SampleInterval.Ticks);

        await ClassUnderTest.WaitForCapacityAsync(topic, CancellationToken.None);

        GetMock<IQueueDepthSampler>().Verify(x => x.SampleDepths(IsAny<IReadOnlyList<SubscribedQueue>>(), IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task When_many_callers_arrive_concurrently_only_one_sample_is_taken()
    {
        GetMock<ITopicQueueResolver>().Setup(x => x.GetSubscribedQueues(topic, IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(50);
                return new List<SubscribedQueue>();
            });
        GetMock<IQueueDepthSampler>().Setup(x => x.SampleDepths(IsAny<IReadOnlyList<SubscribedQueue>>(), IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QueueDepth>());
        var monitor = ClassUnderTest;
        var exceptionEvents = new List<QueueCapacityExceptionArgs>();
        monitor.OnException += (_, args) => exceptionEvents.Add(args);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => monitor.WaitForCapacityAsync(topic, CancellationToken.None))));

        Assert.That(exceptionEvents.Select(x => x.Exception.ToString()), Is.Empty);
        GetMock<ITopicQueueResolver>().Verify(x => x.GetSubscribedQueues(topic, IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task When_the_caller_has_already_cancelled()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var result = await ClassUnderTest.WaitForCapacityAsync(topic, cancellationTokenSource.Token);

        Assert.That(result, Is.False);
        GetMock<IQueueDepthSampler>().VerifyNever(x => x.SampleDepths(IsAny<IReadOnlyList<SubscribedQueue>>(), IsAny<CancellationToken>()));
    }

    [Test]
    public async Task When_the_caller_cancels_while_waiting()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        GetMock<IDelayer>().Setup(x => x.Delay(IsAny<int>(), IsAny<CancellationToken>()))
            .Callback(() => cancellationTokenSource.Cancel())
            .Returns(Task.CompletedTask);

        var result = await ClassUnderTest.WaitForCapacityAsync(topic, cancellationTokenSource.Token);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task When_the_caller_cancels_while_sampling()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var exceptionEvents = new List<QueueCapacityExceptionArgs>();
        ClassUnderTest.OnException += (_, args) => exceptionEvents.Add(args);
        GetMock<IQueueDepthSampler>().Setup(x => x.SampleDepths(IsAny<IReadOnlyList<SubscribedQueue>>(), IsAny<CancellationToken>()))
            .Callback(() => cancellationTokenSource.Cancel())
            .ThrowsAsync(new TaskCanceledException());

        var result = await ClassUnderTest.WaitForCapacityAsync(topic, cancellationTokenSource.Token);

        Assert.That(result, Is.False);
        Assert.That(exceptionEvents, Is.Empty);
    }

    [Test]
    public async Task When_sampling_fails_the_exception_is_reported_and_sampling_is_retried_after_the_sample_interval()
    {
        var exception = new Exception("sampling failed");
        var exceptionEvents = new List<QueueCapacityExceptionArgs>();
        ClassUnderTest.OnException += (_, args) => exceptionEvents.Add(args);
        GetMock<IQueueDepthSampler>().SetupSequence(x => x.SampleDepths(IsAny<IReadOnlyList<SubscribedQueue>>(), IsAny<CancellationToken>()))
            .ThrowsAsync(exception)
            .ReturnsAsync(new List<QueueDepth> { new("queue-a", 0) });

        await ClassUnderTest.WaitForCapacityAsync(topic, CancellationToken.None);

        Assert.That(exceptionEvents.Count, Is.EqualTo(1));
        Assert.That(exceptionEvents[0].Topic, Is.EqualTo(topic));
        Assert.That(exceptionEvents[0].Exception, Is.SameAs(exception));
        GetMock<IDelayer>().Verify(x => x.Delay((int)TopicCapacityState.SampleInterval.TotalMilliseconds, IsAny<CancellationToken>()));
        GetMock<IQueueDepthSampler>().Verify(x => x.SampleDepths(IsAny<IReadOnlyList<SubscribedQueue>>(), IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task When_a_queue_no_longer_exists_the_topic_queues_are_invalidated()
    {
        GetMock<IQueueDepthSampler>().SetupSequence(x => x.SampleDepths(IsAny<IReadOnlyList<SubscribedQueue>>(), IsAny<CancellationToken>()))
            .ThrowsAsync(new QueueDoesNotExistException("queue deleted"))
            .ReturnsAsync(new List<QueueDepth> { new("queue-a", 0) });

        await ClassUnderTest.WaitForCapacityAsync(topic, CancellationToken.None);

        GetMock<ITopicQueueResolver>().Verify(x => x.Invalidate(topic));
    }
}
