using Amazon.SQS;
using Amazon.SQS.Model;

namespace SynkedUp.AwsMessaging;

internal interface IQueueDepthSampler
{
    Task<IReadOnlyList<QueueDepth>> SampleDepths(IReadOnlyList<SubscribedQueue> queues, CancellationToken cancellationToken);
}

internal record QueueDepth(string QueueName, int Depth);

internal class QueueDepthSampler : IQueueDepthSampler
{
    private readonly IAmazonSQS sqsClient;

    public QueueDepthSampler(IAmazonSQS sqsClient)
    {
        this.sqsClient = sqsClient;
    }

    public async Task<IReadOnlyList<QueueDepth>> SampleDepths(IReadOnlyList<SubscribedQueue> queues, CancellationToken cancellationToken)
    {
        return await Task.WhenAll(queues.Select(x => SampleDepth(x, cancellationToken)));
    }

    private async Task<QueueDepth> SampleDepth(SubscribedQueue queue, CancellationToken cancellationToken)
    {
        var response = await sqsClient.GetQueueAttributesAsync(new GetQueueAttributesRequest
        {
            QueueUrl = queue.Url,
            AttributeNames = new List<string>
            {
                QueueAttributeName.ApproximateNumberOfMessages,
                QueueAttributeName.ApproximateNumberOfMessagesDelayed
            }
        }, cancellationToken);

        var attributes = response.Attributes ?? new Dictionary<string, string>();
        var depth = GetAttribute(attributes, QueueAttributeName.ApproximateNumberOfMessages)
                    + GetAttribute(attributes, QueueAttributeName.ApproximateNumberOfMessagesDelayed);
        return new QueueDepth(queue.Name, depth);
    }

    private static int GetAttribute(Dictionary<string, string> attributes, string name)
    {
        return attributes.TryGetValue(name, out var value) && int.TryParse(value, out var number) ? number : 0;
    }
}
