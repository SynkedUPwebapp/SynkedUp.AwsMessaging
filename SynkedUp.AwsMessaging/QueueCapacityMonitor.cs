using System.Collections.Concurrent;
using Amazon.SQS.Model;

namespace SynkedUp.AwsMessaging;

public interface IQueueCapacityMonitor
{
    event OnWaitingForCapacity? OnWaitingForCapacity;
    event OnQueueCapacityException? OnException;
    Task<bool> WaitForCapacityAsync(Topic topic, CancellationToken cancellationToken);
}

internal class QueueCapacityMonitor : IQueueCapacityMonitor
{
    private readonly IQueueCapacityMonitorConfig config;
    private readonly ITopicQueueResolver topicQueueResolver;
    private readonly IQueueDepthSampler queueDepthSampler;
    private readonly IDelayer delayer;
    private readonly IClock clock;
    private readonly ConcurrentDictionary<string, TopicCapacityState> statesByTopic = new();

    public event OnWaitingForCapacity? OnWaitingForCapacity;
    public event OnQueueCapacityException? OnException;

    public QueueCapacityMonitor(IQueueCapacityMonitorConfig config,
        ITopicQueueResolver topicQueueResolver,
        IQueueDepthSampler queueDepthSampler,
        IDelayer delayer,
        IClock clock)
    {
        this.config = config;
        this.topicQueueResolver = topicQueueResolver;
        this.queueDepthSampler = queueDepthSampler;
        this.delayer = delayer;
        this.clock = clock;
    }

    public async Task<bool> WaitForCapacityAsync(Topic topic, CancellationToken cancellationToken)
    {
        var state = statesByTopic.GetOrAdd(topic.ToString(), _ => new TopicCapacityState(config.MaximumMessageDelayMilliseconds));
        while (!cancellationToken.IsCancellationRequested)
        {
            if (state.NeedsSample(clock.UtcNow))
            {
                await RefreshAsync(topic, state, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }
            }

            var result = state.TryTake(clock.UtcNow);
            if (result.Acquired)
            {
                return true;
            }

            OnWaitingForCapacity?.Invoke(this, new WaitingForCapacityArgs(topic,
                result.LimitingQueueName,
                result.LimitingQueueEstimatedDelay,
                result.WaitTime));
            await delayer.Delay((int)Math.Ceiling(result.WaitTime.TotalMilliseconds), cancellationToken);
        }
        return false;
    }

    private async Task RefreshAsync(Topic topic, TopicCapacityState state, CancellationToken cancellationToken)
    {
        try
        {
            await state.RefreshLock.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            if (!state.NeedsSample(clock.UtcNow))
            {
                return;
            }

            var queues = await topicQueueResolver.GetSubscribedQueues(topic, cancellationToken);
            var depths = await queueDepthSampler.SampleDepths(queues, cancellationToken);
            state.ApplySample(clock.UtcNow, depths);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (exception is QueueDoesNotExistException)
            {
                topicQueueResolver.Invalidate(topic);
            }
            state.RecordFailedSample(clock.UtcNow);
            OnException?.Invoke(this, new QueueCapacityExceptionArgs(topic, exception));
        }
        finally
        {
            state.RefreshLock.Release();
        }
    }
}
