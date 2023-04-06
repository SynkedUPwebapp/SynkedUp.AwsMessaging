namespace SynkedUp.AwsMessaging;

public class TestMessageBuilder<T>
{
    private string messageId = Guid.NewGuid().ToString();
    private Topic topic = new("example", "test-event", 1);
    private string correlationId = "";
    private DateTimeOffset? publishedAt;
    private DateTimeOffset? receivedAt;

    public TestMessageBuilder<T> WithMessageId(string messageId)
    {
        this.messageId = messageId;
        return this;
    }

    public TestMessageBuilder<T> WithTopic(Topic topic)
    {
        this.topic = topic;
        return this;
    }
        
    public TestMessageBuilder<T> WithCorrelationId(string correlationId)
    {
        this.correlationId = correlationId;
        return this;
    }
        
    public TestMessageBuilder<T> WithPublishedAt(DateTimeOffset? publishedAt)
    {
        this.publishedAt = publishedAt;
        return this;
    }
        
    public TestMessageBuilder<T> WithReceivedAt(DateTimeOffset? receivedAt)
    {
        this.receivedAt = receivedAt;
        return this;
    }

    public Message<T> Build(T body)
    {
        return new Message<T>(messageId, topic, body)
        {
            CorrelationId = correlationId,
            PublishedAt = publishedAt,
            ReceivedAt = receivedAt,
        };
    }
}