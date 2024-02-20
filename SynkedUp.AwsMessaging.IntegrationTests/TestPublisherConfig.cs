namespace SynkedUp.AwsMessaging.IntegrationTests;

public class TestPublisherConfig : IPublisherConfig
{
    public string Environment => "dev";
    public string SchedulerRoleArn => "arn:aws:iam::519677560816:role/dev-scheduler-role";
}
