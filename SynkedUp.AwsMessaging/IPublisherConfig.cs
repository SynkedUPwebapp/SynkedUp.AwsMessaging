namespace SynkedUp.AwsMessaging;

public interface IPublisherConfig
{
    string Environment { get; }
    string SchedulerRoleArn { get; }
}