using Amazon.SimpleNotificationService.Model;
using Amazon.SQS.Model;
using MessageAttributeValue = Amazon.SimpleNotificationService.Model.MessageAttributeValue;

namespace SynkedUp.AwsMessaging;

internal interface IMessageMapper
{
    PublishRequest ToSnsRequest<T>(string topicArn, Message<T> message);
    Message<T> FromSqsMessage<T>(Topic topic, Message sqsMessage);
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
        var messageAttributes = new Dictionary<string, MessageAttributeValue>
        {
            ["MessageId"] = new() { DataType = "String", StringValue = message.MessageId },
            ["PublishedAt"] = new() { DataType = "String", StringValue = message.PublishedAt!.Value.ToString("O") }
        };
        if (!string.IsNullOrEmpty(message.CorrelationId))
        {
            messageAttributes["CorrelationId"] = new MessageAttributeValue { DataType = "String", StringValue = message.CorrelationId };
        }
        
        return new PublishRequest(topicArn, serializer.Serialize(message.Body))
        {
            MessageAttributes = messageAttributes
        };
    }

    public Message<T> FromSqsMessage<T>(Topic topic, Message sqsMessage)
    {
        var body = DeserializeMessageBody<T>(topic, sqsMessage);
        var messageId = GetMessageAttribute(sqsMessage, "MessageId", Guid.NewGuid().ToString());
        return new Message<T>(messageId, topic, body!)
        {
            CorrelationId = GetMessageAttribute(sqsMessage, "CorrelationId", ""),
            PublishedAt = ParseTimestamp(GetMessageAttribute(sqsMessage, "PublishedAt", "")),
            ReceivedAt = DateTimeOffset.UtcNow
        };
    }

    private T DeserializeMessageBody<T>(Topic topic, Message sqsMessage)
    {
        Exception? innerException = null;
        try
        {
            var body = serializer.Deserialize<T>(sqsMessage.Body);
            if (body != null)
            {
                return body;
            }
        }
        catch (Exception e)
        {
            innerException = e;
        }

        throw new Exception($"Error deserializing message on topic {topic}; message body: {sqsMessage.Body}", innerException);
    }

    public string GetMessageAttribute(Message sqsMessage, string key, string defaultValue)
    {
        return sqsMessage.MessageAttributes?.GetValueOrDefault(key)?.StringValue ?? defaultValue;
    }

    private DateTimeOffset? ParseTimestamp(string? timestamp)
    {
        if (string.IsNullOrEmpty(timestamp))
        {
            return null;
        }
        return DateTimeOffset.Parse(timestamp);
    }
}