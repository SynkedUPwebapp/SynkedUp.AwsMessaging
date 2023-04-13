using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Amazon.SQS.Model;
using Emmersion.Testing;
using Moq;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

internal class QueueUrlRetrieverTests : With_an_automocked<QueueUrlRetriever>
{
    [Test]
    public async Task When_getting_an_existing_queue_url()
    {
        var subscription = new Subscription(new Topic("test", "test-event", 1), "test", "listener");
        var environment = "env";
        var queueName = subscription.EnvironmentName(environment);
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
        var cancellationToken = new CancellationToken();
        var environment = "env";
        var queueName = subscription.EnvironmentName(environment);
        var deadLetterQueueName = subscription.EnvironmentDeadLetterName(environment);
        var deadLetterQueueArn = "dead-letter-queue-arn";
        var queueUrl = "queue-url";
        var topicArn = "topic-arn";
        GetMock<ISubscriberConfig>().Setup(x => x.Environment).Returns(environment);
        GetMock<IAmazonSQS>().Setup(x => x.GetQueueUrlAsync(queueName, cancellationToken))
            .ThrowsAsync(new QueueDoesNotExistException("test-exception"));
        GetMock<IQueueCreator>().Setup(x => x.CreateDeadLetterQueue(deadLetterQueueName, cancellationToken))
            .ReturnsAsync(deadLetterQueueArn);
        GetMock<IQueueCreator>().Setup(x => x.CreateQueue(queueName, deadLetterQueueArn, cancellationToken))
            .ReturnsAsync(queueUrl);
        GetMock<ITopicArnCache>().Setup(x => x.GetTopicArn(environment, subscription.Topic)).ReturnsAsync(topicArn);

        var result = await ClassUnderTest.GetQueueUrlAndCreateIfNecessary(subscription, cancellationToken);
        
        Assert.That(result, Is.EqualTo(queueUrl));
        GetMock<IRetryingTopicSubscriber>().Verify(x => x.SubscribeToTopic(topicArn, queueUrl));
    }
    
    [Test]
    public async Task When_getting_an_existing_dead_letter_queue_url()
    {
        var subscription = new Subscription(new Topic("test", "test-event", 1), "test", "listener");
        var environment = "env";
        var queueName = subscription.EnvironmentDeadLetterName(environment);
        var cancellationToken = new CancellationToken();
        var getQueueUrlResponse = new GetQueueUrlResponse { QueueUrl = "queue-url" };
        GetMock<ISubscriberConfig>().Setup(x => x.Environment).Returns(environment);
        GetMock<IAmazonSQS>().Setup(x => x.GetQueueUrlAsync(queueName, cancellationToken))
            .ReturnsAsync(getQueueUrlResponse);

        var result = await ClassUnderTest.GetDeadLetterQueueUrl(subscription, cancellationToken);
        
        Assert.That(result, Is.EqualTo(getQueueUrlResponse.QueueUrl));
    }
}