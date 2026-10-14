namespace SynkedUp.AwsMessaging;

public delegate void OnQueueCapacityException(object source, QueueCapacityExceptionArgs args);

public class QueueCapacityExceptionArgs(Topic topic, Exception exception)
{
    public Topic Topic { get; } = topic;

    public Exception Exception { get; } = exception;
}
