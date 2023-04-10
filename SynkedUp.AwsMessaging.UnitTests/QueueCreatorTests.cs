using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Emmersion.Testing;
using Moq;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

internal class QueueCreatorTests : With_an_automocked<QueueCreator>
{
    [Test]
    public async Task When_creating_a_queue()
    {
        var queueName = "queue-name";
        var deadLetterQueueArn = "dl-queue-arn";
        var cancellationToken = new CancellationToken();
        var deadLetterAfterAttempts = 7;
        var timeoutVisibilitySeconds = 15;
        RedrivePolicy? redrivePolicy = null;
        var serializedRedrivePolicy = "redrive-policy";
        CreateQueueRequest? request = null;
        var response = new CreateQueueResponse { QueueUrl = "queue-url" };
        GetMock<ISubscriberConfig>().Setup(x => x.DeadLetterAfterAttempts).Returns(deadLetterAfterAttempts);
        GetMock<ISubscriberConfig>().Setup(x => x.VisibilityTimeoutSeconds).Returns(timeoutVisibilitySeconds);
        GetMock<IMessageSerializer>().Setup(x => x.Serialize(IsAny<RedrivePolicy>()))
            .Callback<RedrivePolicy>(x => redrivePolicy = x)
            .Returns(serializedRedrivePolicy);

        GetMock<IAmazonSQS>().Setup(x => x.CreateQueueAsync(IsAny<CreateQueueRequest>(), cancellationToken))
            .Callback<CreateQueueRequest, CancellationToken>((x, _) => request = x)
            .ReturnsAsync(response);

        var result = await ClassUnderTest.CreateQueue(queueName, deadLetterQueueArn, cancellationToken);
        
        Assert.That(result, Is.EqualTo(response.QueueUrl));

        Assert.That(redrivePolicy!.MaxReceiveCount, Is.EqualTo(deadLetterAfterAttempts));
        Assert.That(redrivePolicy.DeadLetterTargetArn, Is.EqualTo(deadLetterQueueArn));
        
        Assert.That(request!.QueueName, Is.EqualTo(queueName));
        Assert.That(request.Attributes[QueueAttributeName.RedrivePolicy], Is.EqualTo(serializedRedrivePolicy));
        Assert.That(request.Attributes[QueueAttributeName.VisibilityTimeout], Is.EqualTo($"{timeoutVisibilitySeconds}"));
    }

    [Test]
    public async Task When_creating_a_dead_letter_queue()
    {
        var queueName = "queue-name_dl";
        var cancellationToken = new CancellationToken();
        var queueUrl = "dl-queue-url";
        var queueArn = "dl-queue-arn";
        CreateQueueRequest? createRequest = null;
        var createResponse = new CreateQueueResponse { QueueUrl = queueUrl };
        GetQueueAttributesRequest? attribtuesRequest = null;
        var attributesResponse = new GetQueueAttributesResponse
        {
            Attributes = new Dictionary<string, string>
            {
                [QueueAttributeName.QueueArn] = queueArn
            }
        };
        GetMock<IAmazonSQS>().Setup(x => x.CreateQueueAsync(IsAny<CreateQueueRequest>(), cancellationToken))
            .Callback<CreateQueueRequest, CancellationToken>((x, _) => createRequest = x)
            .ReturnsAsync(createResponse);
        GetMock<IAmazonSQS>()
            .Setup(x => x.GetQueueAttributesAsync(IsAny<GetQueueAttributesRequest>(), cancellationToken))
            .Callback<GetQueueAttributesRequest, CancellationToken>((x, _) => attribtuesRequest = x)
            .ReturnsAsync(attributesResponse);

        var result = await ClassUnderTest.CreateDeadLetterQueue(queueName, cancellationToken);
        
        Assert.That(result, Is.EqualTo(queueArn));
        
        Assert.That(createRequest!.QueueName, Is.EqualTo(queueName));
        Assert.That(attribtuesRequest!.QueueUrl, Is.EqualTo(queueUrl));
        Assert.That(attribtuesRequest.AttributeNames.Single(), Is.EqualTo(QueueAttributeName.QueueArn));
    }
}