using System.Diagnostics;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace SynkedUp.AwsMessaging;

public interface IMessageSubscriber
{
    Task Subscribe<T>(Subscription subscription, Func<Message<T>, Task> messageHandler);
    event OnMessageReceived? OnMessageReceived;
    event OnException? OnException;
}

internal class MessageSubscriber : IMessageSubscriber, IDisposable
{
    private readonly ISubscriptionCreator subscriptionCreator;
    private readonly ISubscriberConfig config;
    private readonly IAmazonSQS sqsClient;
    private readonly IMessageMapper messageMapper;
    private readonly CancellationTokenSource cancellationTokenSource;

    public MessageSubscriber(ISubscriptionCreator subscriptionCreator,
        ISubscriberConfig config,
        IAmazonSQS sqsClient,
        IMessageMapper messageMapper)
    {
        this.subscriptionCreator = subscriptionCreator;
        this.config = config;
        this.sqsClient = sqsClient;
        this.messageMapper = messageMapper;
        cancellationTokenSource = new CancellationTokenSource();
    }
    
    public event OnMessageReceived? OnMessageReceived;
    public event OnException? OnException;
    
    public async Task Subscribe<T>(Subscription subscription, Func<Message<T>, Task> messageHandler)
    {
        var cancellationToken = cancellationTokenSource.Token;
        var queueUrl = await subscriptionCreator.GetQueueUrlAndCreateIfNecessary(subscription, cancellationToken);
        var request = new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = config.MaxNumberOfMessages,
            WaitTimeSeconds = config.LongPollingSeconds
        };
        
#pragma warning disable CS4014
        Task.Run(() =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                GetMessageBatch(subscription, request, messageHandler, cancellationToken);
            }
        }, cancellationTokenSource.Token);
#pragma warning restore CS4014
    }

    public async Task GetMessageBatch<T>(Subscription subscription, ReceiveMessageRequest request, Func<Message<T>, Task> messageHandler, CancellationToken cancellationToken)
    {
        var response = await sqsClient.ReceiveMessageAsync(request, cancellationToken);
        var batchDeleteRequest = new DeleteMessageBatchRequest
        {
            QueueUrl = request.QueueUrl,
            Entries = new List<DeleteMessageBatchRequestEntry>()
        };
        foreach (var sqsMessage in response.Messages)
        {
            if (await ProcessMessage(subscription, sqsMessage, messageHandler))
            {
                batchDeleteRequest.Entries.Add(new DeleteMessageBatchRequestEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    ReceiptHandle = sqsMessage.ReceiptHandle
                });
            }
        }

        if (batchDeleteRequest.Entries.Any())
        {
            await sqsClient.DeleteMessageBatchAsync(batchDeleteRequest, cancellationToken);
        }
    }

    public async Task<bool> ProcessMessage<T>(Subscription subscription, Message sqsMessage, Func<Message<T>, Task> messageHandler)
    {
        var stopwatch = Stopwatch.StartNew();
        DateTimeOffset? publishedAt = null;
        var receivedAt = DateTimeOffset.UtcNow;
        
        try
        {
            var message = messageMapper.FromSqsMessage<T>(subscription.Topic, sqsMessage);
            publishedAt = message.PublishedAt;
            receivedAt = message.ReceivedAt ?? receivedAt;
            await messageHandler(message);
            return true;
        }
        catch (Exception e)
        {
            OnException?.Invoke(this, new ExceptionArgs(subscription, e));
            return false;
        }
        finally
        {
            OnMessageReceived?.Invoke(this, new MessageReceivedArgs(subscription, publishedAt, receivedAt, stopwatch.Elapsed));    
        }
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
    }
}