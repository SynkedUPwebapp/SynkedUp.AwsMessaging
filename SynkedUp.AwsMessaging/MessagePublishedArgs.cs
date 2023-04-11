namespace SynkedUp.AwsMessaging;

public delegate void OnMessagePublished(object source, MessagePublishedArgs e);

public class MessagePublishedArgs : EventArgs
{
    public MessagePublishedArgs(TimeSpan elapsed)
    {
        Elapsed = elapsed;
    }

    public TimeSpan Elapsed { get; }
}