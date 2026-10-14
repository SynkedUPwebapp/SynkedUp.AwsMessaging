namespace SynkedUp.AwsMessaging.IntegrationTests;

public class TestQueueCapacityMonitorConfig : IQueueCapacityMonitorConfig
{
    public int MaximumMessageDelayMilliseconds => 5000;
}
