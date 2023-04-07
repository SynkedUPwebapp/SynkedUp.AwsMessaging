using System.Diagnostics;

namespace SynkedUp.AwsMessaging;

public interface IMessagePublisher
{
    event OnMessagePublished? OnMessagePublished;
    Task PublishAsync<T>(Message<T> message);
}

internal class MessagePublisher : IMessagePublisher
{
    private readonly IMessageMapper mapper;
    private readonly ISnsClientWrapper snsClient;
    private readonly ITopicArnCache topicArnCache;
    private readonly IPublisherConfig config;

    public event OnMessagePublished? OnMessagePublished;

    public MessagePublisher(IMessageMapper mapper,
        ISnsClientWrapper snsClient,
        ITopicArnCache topicArnCache,
        IPublisherConfig config)
    {
        this.mapper = mapper;
        this.snsClient = snsClient;
        this.topicArnCache = topicArnCache;
        this.config = config;
    }
    
    public async Task PublishAsync<T>(Message<T> message)
    {
        var stopwatch = Stopwatch.StartNew();
        var topicArn = await topicArnCache.GetTopicArn(config.Environment, message.Topic);
        var publishRequest = mapper.ToSnsRequest(topicArn, message);
        await snsClient.PublishAsync(publishRequest);
        OnMessagePublished?.Invoke(this, new MessagePublishedArgs(stopwatch.ElapsedMilliseconds));
    }
}