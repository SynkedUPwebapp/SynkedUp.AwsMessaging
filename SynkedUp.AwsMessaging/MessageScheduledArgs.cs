namespace SynkedUp.AwsMessaging;

public delegate void OnMessageScheduled(object source, MessageScheduledArgs e);

public class MessageScheduledArgs : EventArgs
{

    public MessageScheduledArgs(Topic topic, DateTimeOffset publishAt, TimeSpan elapsed)
    {
        Topic = topic;
        PublishAt = publishAt;
        Elapsed = elapsed;
    }
    
    public Topic Topic { get; }
    public DateTimeOffset PublishAt { get; }
    public TimeSpan Elapsed { get; }
}