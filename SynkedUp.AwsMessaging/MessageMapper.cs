using Amazon.SimpleNotificationService.Model;

namespace SynkedUp.AwsMessaging;

internal interface IMessageMapper
{
    PublishRequest ToSnsRequest<T>(Message<T> message);
}

internal class MessageMapper : IMessageMapper
{
    private readonly IMessageSerializer serializer;
    private readonly ITopicMapper topicMapper;

    public MessageMapper(IMessageSerializer serializer, ITopicMapper topicMapper)
    {
        this.serializer = serializer;
        this.topicMapper = topicMapper;
    }
    
    public PublishRequest ToSnsRequest<T>(Message<T> message)
    {
        return new PublishRequest(topicMapper.ToArn(message.Topic), serializer.Serialize(message))
        {
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["CorrelationId"] = new() { DataType = "String", StringValue = message.CorrelationId },
                ["PublishedAt"] = new() { DataType = "String", StringValue = message.PublishedAt!.Value.ToString("O") }
            }
        };
    }
}