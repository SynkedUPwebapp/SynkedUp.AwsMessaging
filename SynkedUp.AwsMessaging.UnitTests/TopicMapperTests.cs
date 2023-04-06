using Emmersion.Testing;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

public class TopicMapperTests : With_an_automocked<TopicMapper>
{
    [Test]
    public void When_mapping_to_topic_arn()
    {
        var topic = new Topic("publisher", "example-event", 2);
        var arnPrefix = "arn-prefix:";
        GetMock<IPublisherConfig>().Setup(x => x.TopicArnPrefix).Returns(arnPrefix);

        var result = ClassUnderTest.ToArn(topic);
        
        Assert.That(result, Is.EqualTo($"{arnPrefix}{topic}"));
    }
}