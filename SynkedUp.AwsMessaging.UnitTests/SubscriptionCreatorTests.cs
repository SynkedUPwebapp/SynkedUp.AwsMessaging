using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Amazon.SQS.Model;
using Emmersion.Testing;
using Moq;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

internal class SubscriptionCreatorTests : With_an_automocked<SubscriptionCreator>
{
    [Test]
    public async Task When_getting_an_existing_queue_url()
    {
        var subscription = new Subscription(new Topic("test", "test-event", 1), "test", "listener");
        var environment = "env";
        var queueName = $"{environment}_{subscription}";
        var cancellationToken = new CancellationToken();
        var getQueueUrlResponse = new GetQueueUrlResponse { QueueUrl = "queue-url" };
        GetMock<ISubscriberConfig>().Setup(x => x.Environment).Returns(environment);
        GetMock<IAmazonSQS>().Setup(x => x.GetQueueUrlAsync(queueName, cancellationToken))
            .ReturnsAsync(getQueueUrlResponse);

        var result = await ClassUnderTest.GetQueueUrlAndCreateIfNecessary(subscription, cancellationToken);
        
        Assert.That(result, Is.EqualTo(getQueueUrlResponse.QueueUrl));
    }
    
    [Test]
    public async Task When_creating_a_new_queue()
    {
        var subscription = new Subscription(new Topic("test", "test-event", 1), "test", "listener");
        var environment = "env";
        var queueName = $"{environment}_{subscription}";
        var cancellationToken = new CancellationToken();
        CreateQueueRequest? createQueueRequest = null;
        var queueUrl = "queue-url";
        var createQueueResponse = new CreateQueueResponse { QueueUrl = queueUrl };
        var topicArn = "topic-arn";
        var sqsClient = GetMock<IAmazonSQS>().Object;
        GetMock<ISubscriberConfig>().Setup(x => x.Environment).Returns(environment);
        GetMock<IAmazonSQS>().Setup(x => x.GetQueueUrlAsync(queueName, cancellationToken))
            .ThrowsAsync(new QueueDoesNotExistException("test-exception"));
        GetMock<IAmazonSQS>().Setup(x => x.CreateQueueAsync(IsAny<CreateQueueRequest>(), cancellationToken))
            .Callback<CreateQueueRequest, CancellationToken>((x, _) => createQueueRequest = x)
            .ReturnsAsync(createQueueResponse);
        GetMock<ITopicArnCache>().Setup(x => x.GetTopicArn(environment, subscription.Topic)).ReturnsAsync(topicArn);

        var result = await ClassUnderTest.GetQueueUrlAndCreateIfNecessary(subscription, cancellationToken);
        
        Assert.That(result, Is.EqualTo(queueUrl));
        Assert.That(createQueueRequest!.QueueName, Is.EqualTo(queueName));
        GetMock<IAmazonSimpleNotificationService>().Verify(x => x.SubscribeQueueAsync(topicArn, sqsClient, queueUrl));
    }
    
    [Test]
    public void When_getting_an_existing_queue_url_but_the_environment_is_too_long()
    {
        var subscription = new Subscription(new Topic("test", "test-event", 1), "test", "listener");
        var environment = "1234";
        var cancellationToken = new CancellationToken();
        GetMock<ISubscriberConfig>().Setup(x => x.Environment).Returns(environment);

        var exception = Assert.CatchAsync(async () => await ClassUnderTest.GetQueueUrlAndCreateIfNecessary(subscription, cancellationToken));
        
        Assert.That(exception!.Message, Is.EqualTo($"Environment {environment} must not exceed {EnvironmentRestrictions.MaxLength} characters"));
    }
}