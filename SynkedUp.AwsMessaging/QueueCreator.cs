using Amazon.SQS;
using Amazon.SQS.Model;

namespace SynkedUp.AwsMessaging;

internal interface IQueueCreator
{
    Task<string> CreateQueue(string queueName, string deadLetterQueueArn, CancellationToken cancellationToken);
    Task<string> CreateDeadLetterQueue(string queueName, CancellationToken cancellationToken);
}

internal class QueueCreator : IQueueCreator
{
    private readonly IAmazonSQS sqsClient;
    private readonly IMessageSerializer messageSerializer;
    private readonly ISubscriberConfig config;

    public QueueCreator(IAmazonSQS sqsClient, IMessageSerializer messageSerializer, ISubscriberConfig config)
    {
        this.sqsClient = sqsClient;
        this.messageSerializer = messageSerializer;
        this.config = config;
    }

    public async Task<string> CreateQueue(string queueName, string deadLetterQueueArn, CancellationToken cancellationToken)
    {
        var request = new CreateQueueRequest
        {
            QueueName = queueName,
            Attributes = new Dictionary<string, string>
            {
                [QueueAttributeName.RedrivePolicy] = messageSerializer.Serialize(new RedrivePolicy
                {
                    DeadLetterTargetArn = deadLetterQueueArn,
                    MaxReceiveCount = config.DeadLetterAfterAttempts
                })
            }
        };
        var response = await sqsClient.CreateQueueAsync(request, cancellationToken);
        return response.QueueUrl;
    }
    
    public async Task<string> CreateDeadLetterQueue(string queueName, CancellationToken cancellationToken)
    {
        var request = new CreateQueueRequest
        {
            QueueName = queueName
        };
        var response = await sqsClient.CreateQueueAsync(request, cancellationToken);

        return await GetQueueArn(response.QueueUrl, cancellationToken);
    }

    private async Task<string> GetQueueArn(string queueUrl, CancellationToken cancellationToken)
    {
        var request = new GetQueueAttributesRequest
        {
            QueueUrl = queueUrl,
            AttributeNames = new List<string> {QueueAttributeName.QueueArn}
        };
        var response = await sqsClient.GetQueueAttributesAsync(request, cancellationToken);
        return response.QueueARN;
    }
}

internal class RedrivePolicy
{
    public string DeadLetterTargetArn { get; set; } = "";
    public int MaxReceiveCount { get; set; }
}