namespace SynkedUp.AwsMessaging;

public interface IQueueCapacityMonitorConfig
{
    int MaximumMessageDelayMilliseconds { get; }
}
