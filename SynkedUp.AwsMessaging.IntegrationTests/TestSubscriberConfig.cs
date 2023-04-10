namespace SynkedUp.AwsMessaging.IntegrationTests;

public class TestSubscriberConfig : ISubscriberConfig
{
    public string Environment => "dev";
    public int MaxNumberOfMessages => 10;
    public int LongPollingSeconds => 1;
    public int DeadLetterAfterAttempts => 5;
    public int VisibilityTimeoutSeconds => 2;
}