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
await subscriber.SubscribeAsync(subscription, async (Message<CustomerUpdated> message) =>
{
    await DoSomethingWith(message);
});
```

The call to `SubscribeAsync` will fail if the SNS topic does not exist.
If the SQS queue does not exist, it will be created along with a corresponding dead-letter queue.

### Reading from Dead-Letter Queues

You can also subscribe to messages from a dead-letter queue:

```csharp
await subscriber.SubscribeToDeadLettersAsync(subscription, async (string message) =>
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
