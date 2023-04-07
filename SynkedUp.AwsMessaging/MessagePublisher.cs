using System.Diagnostics;

namespace SynkedUp.AwsMessaging;

internal interface IMessagePublisher
{
    event OnMessagePublished? OnMessagePublished;
    Task PublishAsync<T>(Message<T> message);
}

internal class MessagePublisher : IMessagePublisher
{
    private readonly IMessageMapper mapper;
    private readonly ISnsClientWrapper snsClient;
    
    public event OnMessagePublished? OnMessagePublished;

    public MessagePublisher(IMessageMapper mapper, ISnsClientWrapper snsClient)
    {
        this.mapper = mapper;
        this.snsClient = snsClient;
    }
    
    public async Task PublishAsync<T>(Message<T> message)
    {
        var stopwatch = Stopwatch.StartNew();
        await snsClient.PublishAsync(mapper.ToSnsRequest(message));
        OnMessagePublished?.Invoke(this, new MessagePublishedArgs(stopwatch.ElapsedMilliseconds));
    }
}