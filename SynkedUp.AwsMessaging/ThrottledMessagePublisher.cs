namespace SynkedUp.AwsMessaging;

public interface IThrottledMessagePublisher
{
    Task<bool> PublishAsync<T>(Message<T> message, CancellationToken cancellationToken);
}

internal class ThrottledMessagePublisher : IThrottledMessagePublisher
{
    private readonly IQueueCapacityMonitor queueCapacityMonitor;
    private readonly IMessagePublisher messagePublisher;

    public ThrottledMessagePublisher(IQueueCapacityMonitor queueCapacityMonitor, IMessagePublisher messagePublisher)
    {
        this.queueCapacityMonitor = queueCapacityMonitor;
        this.messagePublisher = messagePublisher;
    }

    public async Task<bool> PublishAsync<T>(Message<T> message, CancellationToken cancellationToken)
    {
        if (!await queueCapacityMonitor.WaitForCapacityAsync(message.Topic, cancellationToken))
        {
            return false;
        }

        await messagePublisher.PublishAsync(message);
        return true;
    }
}
