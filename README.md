# SynkedUp.AwsMessaging
A library for doing fan-out messaging using SNS topics and SQS queues in AWS
according to our messaging conventions:
* Topics are named: `{environment}_{publisher}_{event}_v{version}`
  * _Example:_ `p_monolith_customer-updated_v1`
* Queues are named: `{topic}_{subscriber}_{process}`
  * _Example:_ `p_monolith_customer-updated_v1_monolith_listener`
* Queues are subscribed to the corresponding SNS topic
* Dead letter queues are created for each subscribing queue, with the name:
  `{queue}_dl`


## AWS Credentials

This library is set up to interact with AWS using the
`AWSSDK.Extensions.NETCore.Setup` package and automatically connect using configuration
values and your `~/.aws` credential configuration.
Note the call to `AddDefaultAWSOptions` in the configuration steps below.


## Publishing Messages

### Configuration

Before you can use the `IMessagePublisher`, you must first set up your DI container:

```csharp
SynkedUp.AwsMessaging.DependencyInjectionConfig.ConfigurePublisherServices(services);
services.AddDefaultAWSOptions(configuration.GetAWSOptions());
```

You must also register an instance of `IPublisherConfig` with the following fields:
* `Environment` - a short string (3 character maximum) specifying an environment
  * (e.g. "s" for staging; "p" for production; "dev" for development)

### Publishing a Message

To publish a message, use an injected `IMessagePublisher`:

```csharp
var topic = new Topic("monolith", "customer-updated", 1);
var message = new Message<CustomerUpdated>(topic, new TestData
{
    CustomerId = 123
});

await publisher.PublishAsync(message);
```

Each message is defined with by a topic and a body (a.k.a. payload).
You can also optionally add a `CorrelationId` string on the `Message<T>`.

> **NOTE:** The library does **NOT** automatically create topics.
> Message publishing will fail if the topic cannot be found.
> We recommend you create them as part of your IaC tooling (such as a terraform script).


## Throttled Publishing

When publishing a large number of messages at once, such as for a backfill,
the backlog can delay real-time messages behind it in every subscribed queue.
The `IThrottledMessagePublisher` paces publishing so the queues subscribed to
the topic stay shallow enough that a newly published real-time message is not
delayed by more than a configured maximum.
It adapts to how quickly subscribers are processing messages: it slows down
during busy periods and speeds up when subscribers have spare capacity.

> **NOTE:** Only use throttled publishing for bulk work.
> Real-time messages should continue to be published with `IMessagePublisher`.

### Configuration

In addition to the publisher configuration above, set up your DI container:

```csharp
SynkedUp.AwsMessaging.DependencyInjectionConfig.ConfigurePublisherServices(services);
SynkedUp.AwsMessaging.DependencyInjectionConfig.ConfigureThrottledPublisherServices(services);
services.AddDefaultAWSOptions(configuration.GetAWSOptions());
```

You must also register an instance of `IQueueCapacityMonitorConfig` with the following field:
* `MaximumMessageDelayMilliseconds` - the target maximum time a newly published message
  should wait in any subscribed queue (e.g. `30000`)

Throttled publishing requires these additional IAM permissions:
* `sns:ListSubscriptionsByTopic`
* `sqs:GetQueueUrl`
* `sqs:GetQueueAttributes`

### Publishing a Message

```csharp
foreach (var customer in customersToBackfill)
{
    var message = new Message<CustomerUpdated>(topic, customer);
    if (!await throttledPublisher.PublishAsync(message, cancellationToken))
    {
        break;
    }
}
```

Each call waits until there is capacity and then publishes the message, returning `true`.
If the cancellation token is canceled while waiting, the message is not published and
the call returns `false` without throwing an exception, so a backfill can be stopped gracefully.

If you need to wait without publishing through the library (for example, to bound
concurrency in your own publishing loop), use `IQueueCapacityMonitor` directly.
Each call to `WaitForCapacityAsync` that returns `true` allows exactly one publish:

```csharp
if (await queueCapacityMonitor.WaitForCapacityAsync(topic, cancellationToken))
{
    await publisher.PublishAsync(message);
}
```

### How It Works

* Every few seconds while publishing, the depth of each queue subscribed to the topic is sampled.
* The rate at which each queue is draining is estimated from those samples and the
  messages this process has published. Messages published by other processes are
  treated as part of the queue's load, so the estimate errs on the side of publishing slower.
* Publishing is paced so the slowest queue's backlog stays within `MaximumMessageDelayMilliseconds`.
* No AWS calls are made unless something is waiting for capacity.
* If the queues cannot be sampled, publishing pauses until sampling succeeds (it fails closed).
  If a subscriber stops processing messages, publishing pauses until it recovers.
  Use a cancellation token to limit how long a caller will wait; cancellation returns `false` rather than throwing.
* Multiple processes can publish to the same topic at once without extra configuration.

### Monitoring

```csharp
queueCapacityMonitor.OnWaitingForCapacity += (_, args) =>
{
    logger.debug($"Waiting {args.WaitTime} for {args.LimitingQueueName} (estimated delay {args.LimitingQueueEstimatedDelay})");
};
queueCapacityMonitor.OnException += (_, args) =>
{
    logger.warn(args.Exception, $"Unable to sample queues for {args.Topic}");
};
```


## Subscribing to Messages

### Configuration

Before you can use the `IMessageSubscriber`, you must first set up your DI container:

```csharp
SynkedUp.AwsMessaging.DependencyInjectionConfig.ConfigureSubscriberServices(services);
services.AddDefaultAWSOptions(configuration.GetAWSOptions());
```

You must also register an instance of `ISubscriberConfig` with the following fields:
* `Environment` - a short string (3 character maximum) specifying an environment
  * (e.g. "s" for staging; "p" for production; "dev" for development)
* `MaxNumberOfMessages` - the maximum number of messages to receive in a long-poll batch
* `LongPollingSeconds` - the number of seconds (up to 20) to wait per batch for SQS long-polling
* `DeadLetterAfterAttempts` - how many receive attempts before a message will be moved to the dead-letter queue  
* `VisibilityTimeoutSeconds` - how many seconds before a message can be processed again

### Subscribing

Subscribe to messages from a specific queue by using an injected `IMessageSubscriber`:

```csharp
var topic = new Topic("monolith", "customer-updated", 1);
var subscription = new Subscription(topic, "monolith", "listener");
await subscriber.SubscribeAsync(subscription, cancellationToken, async (Message<CustomerUpdated> message) =>
{
    await DoSomethingWith(message);
});
```

The call to `SubscribeAsync` will fail if the SNS topic does not exist.
If the SQS queue does not exist, it will be created along with a corresponding dead-letter queue.

Messages will continue to be received until the cancellationToken is marked canceled.

Each received message has a `ReceiveCount` indicating how many times delivery has been attempted,
including the current attempt (`1` on the first delivery).
It comes from SQS's `ApproximateReceiveCount`, which is the same count used to decide when a message
is moved to the dead-letter queue, so a message is on its last attempt when
`ReceiveCount == DeadLetterAfterAttempts`:

```csharp
await subscriber.SubscribeAsync(subscription, cancellationToken, async (Message<CustomerUpdated> message) =>
{
    if (message.ReceiveCount > 1)
    {
        logger.info($"Retrying message {message.MessageId}; attempt {message.ReceiveCount}");
    }
    await DoSomethingWith(message);
});
```

### Reading from Dead-Letter Queues

You can also subscribe to messages from a dead-letter queue:

```csharp
await subscriber.SubscribeToDeadLettersAsync(subscription, cancellationToken, async (string message) =>
{
    await DoSomethingWith(message);
});
```

Note that when you subscribe to a dead-letter queue, you will receive messages
as their "raw" string values, since deserialization may have been why they failed
to be processed originally.


## Monitoring

The `IMessageSubscriber` emits events to inform you of message timings and any processing exceptions:

```csharp
subscriber.OnException += (_, args) =>
{
    logger.warn(args.Exception, "Error processing events");
};
subscriber.OnMessageReceived += (_, args) =>
{
    recordProcessingTime(args.Subscription, args.ProcessingTime);
    recordRoundTripTime(args.Subscription, args.ReceivedAt - args.PublishedAt);
};
```

When a message fails to process, the `OnException` args include the message's `ReceiveCount` (if available).
A failure where `ReceiveCount >= DeadLetterAfterAttempts` means the message will most likely be moved to the
dead-letter queue instead of being retried:

```csharp
subscriber.OnException += (_, args) =>
{
    if (args.ReceiveCount >= subscriberConfig.DeadLetterAfterAttempts)
    {
        logger.error(args.Exception, $"Message on {args.Subscription} is being dead-lettered");
    }
};
```

This is a best-effort signal: a message can also be dead-lettered without raising this event
(for example, if the process stops mid-processing on the final attempt),
so we still recommend alerting on the depth of the dead-letter queues.

The `IMessageProcessor` also emits events so you can measure how long it is taking to send a message:

```csharp
publisher.OnMessagePublished += (_, args) =>
{
    recordSendTime(args.Elapsed);
}
```

## Unit Testing

When unit testing your classes which use this library, we recommend mocking the
`IMessageProcessor` and `IMessageSubscriber` interfaces.
We also provide a `TestMessageBuilder` which allows you to create messages
with data which cannot be otherwise set on the message object:

```csharp
var message = new TestMessageBuilder<SomeClass>()
    .WithPublishedAt(publishedAt)
    .WithReceivedAt(receivedAt)
    .WithReceiveCount(receiveCount)
    .Build(body);
```

## Integration Tests
The integration tests in this repository expect to find a `[profile synkedup]` entry
in your `~/.aws/config` file.
They also require 2 topics to be set up:
* `dev_aws-messaging_test_v0`
* `dev_aws-messaging_dlq-test_v0`


## Version History

### v1.0
Initial release.

### v2.0
Updated dependencies; now .NET 8 is the lowest-supported framework.

### v2.1
Added throttled publishing (`IThrottledMessagePublisher` and `IQueueCapacityMonitor`) for bulk work such as backfills.
Added `ReceiveCount` to received messages and to subscriber exception events.
