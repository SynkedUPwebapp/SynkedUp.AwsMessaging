using System.Collections.Concurrent;
using Amazon.SimpleNotificationService;

namespace SynkedUp.AwsMessaging;

internal interface ITopicArnCache
{
    Task<string> GetTopicArn(string environment, Topic topic);
}

internal class TopicArnCache : ITopicArnCache
{
    private readonly IAmazonSimpleNotificationService snsClient;
    private readonly ConcurrentDictionary<string, string> cache = new();

    public TopicArnCache(IAmazonSimpleNotificationService snsClient)
    {
        this.snsClient = snsClient;
    }
    
    public async Task<string> GetTopicArn(string environment, Topic topic)
    {
        var topicName = $"{environment}:{topic}";
        if (cache.TryGetValue(topicName, out var topicArn))
        {
            return topicArn;
        }
        
        var result = await snsClient.FindTopicAsync(topicName);
        if (result == null)
        {
            throw new Exception($"Unable to find topic: {topicName}");
        }
        SetArn(topicName, result.TopicArn);
        return result.TopicArn;
    }

    public void SetArn(string topicName, string topicArn)
    {
        cache.TryAdd(topicName, topicArn);
    }

    public bool IsArnSet(string topicName)
    {
        return cache.ContainsKey(topicName);
    }
}