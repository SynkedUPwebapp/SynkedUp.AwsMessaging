namespace SynkedUp.AwsMessaging;

public interface ITopicMapper
{
    string ToArn(Topic topic);
}

public class TopicMapper : ITopicMapper
{
    private readonly IPublisherConfig config;

    public TopicMapper(IPublisherConfig config)
    {
        this.config = config;
    }
    
    public string ToArn(Topic topic)
    {
        return $"{config.TopicArnPrefix}{topic}";
    }
}