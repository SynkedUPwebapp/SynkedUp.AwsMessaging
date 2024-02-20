using System.Diagnostics;
using Amazon.Scheduler;
using Amazon.SimpleNotificationService;

namespace SynkedUp.AwsMessaging;

public interface IMessagePublisher
{
    event OnMessagePublished? OnMessagePublished;
    event OnMessageScheduled? OnMessageScheduled;
    Task PublishAsync<T>(Message<T> message);
    Task ScheduleAsync<T>(Message<T> message, DateTimeOffset publishAt);
}

internal class MessagePublisher : IMessagePublisher
{
    private readonly IMessageMapper mapper;
    private readonly IAmazonSimpleNotificationService snsClient;
    private readonly ITopicArnCache topicArnCache;
    private readonly IPublisherConfig config;
    private readonly IAmazonScheduler scheduler;

    public event OnMessagePublished? OnMessagePublished;
    public event OnMessageScheduled? OnMessageScheduled;

    public MessagePublisher(IMessageMapper mapper,
        IAmazonSimpleNotificationService snsClient,
        ITopicArnCache topicArnCache,
        IPublisherConfig config,
        IAmazonScheduler scheduler)
    {
        this.mapper = mapper;
        this.snsClient = snsClient;
        this.topicArnCache = topicArnCache;
        this.config = config;
        this.scheduler = scheduler;
    }
    
    public async Task PublishAsync<T>(Message<T> message)
    {
        var stopwatch = Stopwatch.StartNew();
        var topicArn = await topicArnCache.GetTopicArn(config.Environment, message.Topic);
        var publishRequest = mapper.ToSnsRequest(topicArn, message with { PublishedAt = DateTimeOffset.UtcNow });
        await snsClient.PublishAsync(publishRequest);
        OnMessagePublished?.Invoke(this, new MessagePublishedArgs(message.Topic, stopwatch.Elapsed));
    }

    public async Task ScheduleAsync<T>(Message<T> message, DateTimeOffset publishAt)
    {
        var stopwatch = Stopwatch.StartNew();
        var topicArn = await topicArnCache.GetTopicArn(config.Environment, message.Topic);
        var createScheduleRequest = mapper.ToCreateScheduleRequest(topicArn, message with { PublishedAt = DateTimeOffset.UtcNow }, publishAt);
        await scheduler.CreateScheduleAsync(createScheduleRequest);
        OnMessageScheduled?.Invoke(this, new MessageScheduledArgs(message.Topic, publishAt, stopwatch.Elapsed));
    }
}