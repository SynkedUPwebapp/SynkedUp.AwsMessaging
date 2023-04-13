namespace SynkedUp.AwsMessaging;

public delegate void OnMessagePublished(object source, MessagePublishedArgs e);

public class MessagePublishedArgs : EventArgs
{
    public MessagePublishedArgs(Topic topic, TimeSpan elapsed)
    {
        Topic = topic;
        Elapsed = elapsed;
    }

    public Topic Topic { get; }
    public TimeSpan Elapsed { get; }
}