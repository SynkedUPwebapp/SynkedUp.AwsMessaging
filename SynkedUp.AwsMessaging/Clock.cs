namespace SynkedUp.AwsMessaging;

internal interface IClock
{
    DateTimeOffset UtcNow { get; }
}

internal class Clock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
