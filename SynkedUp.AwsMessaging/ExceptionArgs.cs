namespace SynkedUp.AwsMessaging;

public delegate void OnException(object source, ExceptionArgs args);

public class ExceptionArgs
{
    public ExceptionArgs(Subscription subscription, Exception exception)
    {
        Subscription = subscription;
        Exception = exception;
    }

    public ExceptionArgs(Subscription subscription, Exception exception, int? receiveCount)
        : this(subscription, exception)
    {
        ReceiveCount = receiveCount;
    }

    public Subscription Subscription { get; }

    public Exception Exception { get; }

    public int? ReceiveCount { get; }
}
