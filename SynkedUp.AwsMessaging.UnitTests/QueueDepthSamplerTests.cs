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

internal class QueueDepthSamplerTests : With_an_automocked<QueueDepthSampler>
{
    [Test]
    public async Task When_sampling_queue_depths()
    {
        var queues = new List<SubscribedQueue>
        {
            new("queue-a", "url-a"),
            new("queue-b", "url-b")
        };
        var cancellationToken = CancellationToken.None;
        SetupAttributes("url-a", "10", "5");
        SetupAttributes("url-b", "0", "0");

        var result = await ClassUnderTest.SampleDepths(queues, cancellationToken);

        Assert.That(result, Is.EqualTo(new[] { new QueueDepth("queue-a", 15), new QueueDepth("queue-b", 0) }));
        GetMock<IAmazonSQS>().Verify(x => x.GetQueueAttributesAsync(
            It.Is<GetQueueAttributesRequest>(request => request.QueueUrl == "url-a"
                && request.AttributeNames.Contains(QueueAttributeName.ApproximateNumberOfMessages)
                && request.AttributeNames.Contains(QueueAttributeName.ApproximateNumberOfMessagesDelayed)),
            cancellationToken));
    }

    [Test]
    public async Task When_attributes_are_missing()
    {
        var queues = new List<SubscribedQueue> { new("queue-a", "url-a") };
        GetMock<IAmazonSQS>().Setup(x => x.GetQueueAttributesAsync(IsAny<GetQueueAttributesRequest>(), IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueAttributesResponse());

        var result = await ClassUnderTest.SampleDepths(queues, CancellationToken.None);

        Assert.That(result.Single().Depth, Is.EqualTo(0));
    }

    private void SetupAttributes(string queueUrl, string visible, string delayed)
    {
        GetMock<IAmazonSQS>()
            .Setup(x => x.GetQueueAttributesAsync(It.Is<GetQueueAttributesRequest>(request => request.QueueUrl == queueUrl), IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueAttributesResponse
            {
                Attributes = new Dictionary<string, string>
                {
                    [QueueAttributeName.ApproximateNumberOfMessages] = visible,
                    [QueueAttributeName.ApproximateNumberOfMessagesDelayed] = delayed
                }
            });
    }
}
