using System.Collections.Concurrent;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

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
        var topicName = topic.EnvironmentName(environment);
        if (cache.TryGetValue(topicName, out var topicArn))
        {
            return topicArn;
        }
        
        var foundTopicArn = await FindTopicArnAsync(topicName);
        if (foundTopicArn == null)
        {
            throw new Exception($"Unable to find topic: {topicName}");
        }
        SetArn(topicName, foundTopicArn);
        return foundTopicArn;
    }

    private async Task<string?> FindTopicArnAsync(string topicName)
    {
        var topicArnSuffix = $":{topicName}";
        string? nextToken = null;
        do
        {
            var request = new ListTopicsRequest();
            if (!string.IsNullOrEmpty(nextToken))
            {
                request.NextToken = nextToken;
            }

            var response = await snsClient.ListTopicsAsync(request);
            var match = response.Topics?.FirstOrDefault(x => x.TopicArn != null && x.TopicArn.EndsWith(topicArnSuffix, StringComparison.Ordinal));
            if (match != null)
            {
                return match.TopicArn;
            }

            nextToken = response.NextToken;
        } while (!string.IsNullOrEmpty(nextToken));

        return null;
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