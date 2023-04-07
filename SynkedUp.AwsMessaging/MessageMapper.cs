using Amazon.SimpleNotificationService.Model;

namespace SynkedUp.AwsMessaging;

internal interface IMessageMapper
{
    PublishRequest ToSnsRequest<T>(string topicArn, Message<T> message);
}

internal class MessageMapper : IMessageMapper
{
    private readonly IMessageSerializer serializer;

    public MessageMapper(IMessageSerializer serializer)
    {
        this.serializer = serializer;
    }
    
    public PublishRequest ToSnsRequest<T>(string topicArn, Message<T> message)
    {
        return new PublishRequest(topicArn, serializer.Serialize(message.Body))
        {
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["CorrelationId"] = new() { DataType = "String", StringValue = message.CorrelationId },
                ["PublishedAt"] = new() { DataType = "String", StringValue = message.PublishedAt!.Value.ToString("O") }
            }
        };
    }
}