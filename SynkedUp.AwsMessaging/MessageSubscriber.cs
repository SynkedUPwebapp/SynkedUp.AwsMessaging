using System.Diagnostics;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace SynkedUp.AwsMessaging;

public interface IMessageSubscriber : IDisposable
{
    Task SubscribeAsync<T>(Subscription subscription, Func<Message<T>, Task> messageHandler);
    Task SubscribeToDeadLettersAsync(Subscription subscription, Func<string, Task> messageHandler);
    event OnMessageReceived? OnMessageReceived;
    event OnException? OnException;
}

internal class MessageSubscriber : IMessageSubscriber
{
    private readonly IQueueUrlRetriever queueUrlRetriever;
    private readonly ISubscriberConfig config;
    private readonly IAmazonSQS sqsClient;
    private readonly IMessageMapper messageMapper;
    private readonly CancellationTokenSource cancellationTokenSource;

    public MessageSubscriber(IQueueUrlRetriever queueUrlRetriever,
        ISubscriberConfig config,
        IAmazonSQS sqsClient,
        IMessageMapper messageMapper)
    {
        this.queueUrlRetriever = queueUrlRetriever;
        this.config = config;
        this.sqsClient = sqsClient;
        this.messageMapper = messageMapper;
        cancellationTokenSource = new CancellationTokenSource();
    }
    
    public event OnMessageReceived? OnMessageReceived;
    public event OnException? OnException;
    
    public async Task SubscribeAsync<T>(Subscription subscription, Func<Message<T>, Task> messageHandler)
    {
        var cancellationToken = cancellationTokenSource.Token;
        var queueUrl = await queueUrlRetriever.GetQueueUrlAndCreateIfNecessary(subscription, cancellationToken);
        var request = new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = config.MaxNumberOfMessages,
            WaitTimeSeconds = config.LongPollingSeconds
        };
        
#pragma warning disable CS4014
        Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await GetMessageBatch(request, x => ProcessMessage(subscription, x, messageHandler), cancellationToken);
            }
        }, cancellationTokenSource.Token);
#pragma warning restore CS4014
    }
    
    public async Task SubscribeToDeadLettersAsync(Subscription subscription, Func<string, Task> messageHandler)
    {
        var cancellationToken = cancellationTokenSource.Token;
        var queueUrl = await queueUrlRetriever.GetDeadLetterQueueUrl(subscription, cancellationToken);
        var request = new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = config.MaxNumberOfMessages,
            WaitTimeSeconds = config.LongPollingSeconds
        };
        
#pragma warning disable CS4014
        Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await GetMessageBatch(request, x => ProcessDeadLetterMessage(x, messageHandler), cancellationToken);
            }
        }, cancellationTokenSource.Token);
#pragma warning restore CS4014
    }

    public async Task GetMessageBatch(ReceiveMessageRequest request, Func<Message, Task<bool>> messageProcessor, CancellationToken cancellationToken)
    {
        var response = await sqsClient.ReceiveMessageAsync(request, cancellationToken);
        var batchDeleteRequest = new DeleteMessageBatchRequest
        {
            QueueUrl = request.QueueUrl,
            Entries = new List<DeleteMessageBatchRequestEntry>()
        };
        foreach (var sqsMessage in response.Messages)
        {
            if (await messageProcessor(sqsMessage))
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
    
    public async Task<bool> ProcessDeadLetterMessage(Message sqsMessage, Func<string, Task> messageHandler)
    {
        try
        {
            await messageHandler(sqsMessage.Body);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
    }
}