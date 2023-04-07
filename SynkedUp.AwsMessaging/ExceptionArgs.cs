namespace SynkedUp.AwsMessaging;

public delegate void OnException(object source, ExceptionArgs args);

public class ExceptionArgs
{
    public ExceptionArgs(Subscription subscription, Exception exception)
    {
        Subscription = subscription;
        Exception = exception;
    }

    public Subscription Subscription { get; }
        
    public Exception Exception { get; }
}