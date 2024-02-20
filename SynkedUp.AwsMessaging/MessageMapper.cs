using Amazon.Scheduler;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS.Model;
using MessageAttributeValue = Amazon.SimpleNotificationService.Model.MessageAttributeValue;
using Amazon.Scheduler.Model;

namespace SynkedUp.AwsMessaging;

internal interface IMessageMapper
{
    PublishRequest ToSnsRequest<T>(string topicArn, Message<T> message);
    Message<T> FromSqsMessage<T>(Topic topic, Message sqsMessage);
    CreateScheduleRequest ToCreateScheduleRequest<T>(string topicArn, Message<T> message, DateTimeOffset publishAt);
}

internal class MessageMapper : IMessageMapper
{
    private readonly IMessageSerializer serializer;
    private readonly IPublisherConfig publisherConfig;

    public MessageMapper(IMessageSerializer serializer, IPublisherConfig publisherConfig)
    {
        this.serializer = serializer;
        this.publisherConfig = publisherConfig;
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
        var envelope = Deserialize<SqsMessageEnvelope>(topic, sqsMessage.Body);
        var body = Deserialize<T>(topic, envelope.Message);
        var messageId = GetMessageAttribute(envelope, "MessageId", Guid.NewGuid().ToString());
        return new Message<T>(messageId, topic, body!)
        {
            CorrelationId = GetMessageAttribute(envelope, "CorrelationId", ""),
            PublishedAt = ParseTimestamp(GetMessageAttribute(envelope, "PublishedAt", "")),
            ReceivedAt = DateTimeOffset.UtcNow
        };
    }

    public CreateScheduleRequest ToCreateScheduleRequest<T>(string topicArn, Message<T> message, DateTimeOffset publishAt)
    {
        return new CreateScheduleRequest
        {
            Name = Guid.NewGuid().ToString(),
            GroupName = $"{publisherConfig.Environment}_scheduled_messages",
            ScheduleExpression = $"at({publishAt:yyyy-MM-ddThh:mm:ss})",
            ActionAfterCompletion = ActionAfterCompletion.DELETE,
            Target = new Target
            {
                Arn = topicArn,
                Input = serializer.Serialize(message.Body),
                RoleArn = publisherConfig.SchedulerRoleArn
            },
            FlexibleTimeWindow = new FlexibleTimeWindow
            {
                Mode = FlexibleTimeWindowMode.OFF
            }
        };
    }

    private T Deserialize<T>(Topic topic, string json)
    {
        Exception? innerException = null;
        try
        {
            var body = serializer.Deserialize<T>(json);
            if (body != null)
            {
                return body;
            }
        }
        catch (Exception e)
        {
            innerException = e;
        }

        throw new Exception($"Error deserializing message on topic {topic}; data: {json}", innerException);
    }

    private string GetMessageAttribute(SqsMessageEnvelope envelope, string key, string defaultValue)
    {
        return envelope.MessageAttributes?.GetValueOrDefault(key)?.Value ?? defaultValue;
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

internal class SqsMessageAttribute
{
    public string Value { get; set; } = "";
}

internal class SqsMessageEnvelope
{
    public string Message { get; set; } = "";
    public Dictionary<string, SqsMessageAttribute>? MessageAttributes { get; set; }
}