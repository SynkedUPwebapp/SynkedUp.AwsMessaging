namespace SynkedUp.AwsMessaging;

public delegate void OnMessagePublished(object source, MessagePublishedArgs e);

public class MessagePublishedArgs : EventArgs
{
    public MessagePublishedArgs(long elapsedMilliseconds)
    {
        ElapsedMilliseconds = elapsedMilliseconds;
    }

    public long ElapsedMilliseconds { get; }
}