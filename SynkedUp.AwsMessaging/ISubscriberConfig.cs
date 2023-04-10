namespace SynkedUp.AwsMessaging;

public interface ISubscriberConfig
{
    string Environment { get; }
    int MaxNumberOfMessages { get; }
    int LongPollingSeconds { get; }
    int DeadLetterAfterAttempts { get; }
}