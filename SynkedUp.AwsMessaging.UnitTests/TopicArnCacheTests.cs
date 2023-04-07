using System.Threading.Tasks;
using Emmersion.Testing;
using Moq;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

public class TopicArnCacheTests : With_an_automocked<TopicArnCache>
{
    [Test]
    public async Task When_there_is_a_cache_miss()
    {
        var topic = new Topic("test", "event", 0);
        var environment = "env";
        var topicName = $"{environment}:{topic}";
        var topicArn = "topic-arn";
        var awsTopic = new Amazon.SimpleNotificationService.Model.Topic
        {
            TopicArn = topicArn
        };
        GetMock<ISnsClientWrapper>().Setup(x => x.FindTopicAsync(topicName))
            .ReturnsAsync(awsTopic);

        var result = await ClassUnderTest.GetTopicArn(environment, topic);
        
        Assert.That(result, Is.EqualTo(topicArn));
        Assert.That(ClassUnderTest.IsArnSet(topicName), Is.True);
        GetMock<ISnsClientWrapper>().Verify(x => x.FindTopicAsync(topicName), Times.Exactly(1));
    }
    
    [Test]
    public async Task When_there_is_a_cache_miss_and_the_topic_could_not_be_found()
    {
        var topic = new Topic("test", "event", 0);
        var environment = "env";
        var topicName = $"{environment}:{topic}";
        GetMock<ISnsClientWrapper>().Setup(x => x.FindTopicAsync(topicName))
            .ReturnsNullAsync();

        var exception = Assert.CatchAsync(async () => await ClassUnderTest.GetTopicArn(environment, topic));
        
        Assert.That(exception!.Message, Is.EqualTo($"Unable to find topic: {topicName}"));
        Assert.That(ClassUnderTest.IsArnSet(topicName), Is.False);
        GetMock<ISnsClientWrapper>().Verify(x => x.FindTopicAsync(topicName), Times.Exactly(1));
    }
    
    [Test]
    public async Task When_there_is_a_cache_hit()
    {
        var topic = new Topic("test", "event", 0);
        var environment = "env";
        var topicName = $"{environment}:{topic}";
        var topicArn = "topic-arn";
        ClassUnderTest.SetArn(topicName, topicArn);
        Assert.That(ClassUnderTest.IsArnSet(topicName), Is.True);

        var result = await ClassUnderTest.GetTopicArn(environment, topic);
        
        Assert.That(result, Is.EqualTo(topicArn));
        GetMock<ISnsClientWrapper>().VerifyNever(x => x.FindTopicAsync(IsAny<string>()));
    }
}