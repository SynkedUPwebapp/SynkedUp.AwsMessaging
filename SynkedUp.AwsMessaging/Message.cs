namespace SynkedUp.AwsMessaging;

public record Message<T>
{
    public string MessageId { get; } = Guid.NewGuid().ToString();
    public string CorrelationId { get; init; } = "";
    public Topic Topic { get; }
    public T Body { get; }
    public DateTimeOffset? PublishedAt { get; internal init; }
    public DateTimeOffset? ReceivedAt { get; internal init; }

    public Message(Topic topic, T body)
    {
        Topic = topic;
        Body = body;
    }

    internal Message(string messageId, Topic topic, T body)
    {
        MessageId = messageId;
        Topic = topic;
        Body = body;
    }
}