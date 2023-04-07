namespace SynkedUp.AwsMessaging;

public delegate void OnMessageReceived(object source, MessageReceivedArgs args);

public class MessageReceivedArgs : EventArgs
{
    public MessageReceivedArgs(Subscription subscription,
        DateTimeOffset? publishedAt,
        DateTimeOffset receivedAt,
        TimeSpan processingTime)
    {
        Subscription = subscription;
        PublishedAt = publishedAt;
        ReceivedAt = receivedAt;
        ProcessingTime = processingTime;
    }

    public Subscription Subscription { get; }
    public DateTimeOffset? PublishedAt { get; }
    public DateTimeOffset ReceivedAt { get; }
    public TimeSpan ProcessingTime { get; }
}