using Amazon.Runtime.SharedInterfaces;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace SynkedUp.AwsMessaging;

public interface ISqsClientWrapper
{
    Task<CreateQueueResponse> CreateQueue(CreateQueueRequest request, CancellationToken cancellationToken);
    Task<GetQueueUrlResponse> GetQueueUrl(string queueName, CancellationToken cancellationToken);
    Task<ReceiveMessageResponse> ReceiveMessagesAsync(ReceiveMessageRequest request, CancellationToken cancellationToken);
    ICoreAmazonSQS Client { get; }
}

public class SqsClientWrapper : ISqsClientWrapper
{
    private readonly AmazonSQSClient client;

    public SqsClientWrapper()
    {
        client = new AmazonSQSClient();
    }

    public async Task<CreateQueueResponse> CreateQueue(CreateQueueRequest request, CancellationToken cancellationToken)
    {
        return await client.CreateQueueAsync(request, cancellationToken);
    }

    public async Task<GetQueueUrlResponse> GetQueueUrl(string queueName, CancellationToken cancellationToken)
    {
        return await client.GetQueueUrlAsync(queueName, cancellationToken);
    }

    public async Task<ReceiveMessageResponse> ReceiveMessagesAsync(ReceiveMessageRequest request, CancellationToken cancellationToken)
    {
        // var request = new ReceiveMessageRequest
        // {
        //     QueueUrl = queueUrl,
        //     MaxNumberOfMessages = subscriberConfig.MaxNumberOfMessages,
        //     WaitTimeSeconds = subscriberConfig.LongPollingSeconds
        // };
        return await client.ReceiveMessageAsync(request, cancellationToken);
    }

    public ICoreAmazonSQS Client => client;
}