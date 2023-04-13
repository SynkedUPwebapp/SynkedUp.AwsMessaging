using System.Text.RegularExpressions;

namespace SynkedUp.AwsMessaging;

public class Subscription
{
    public readonly Topic Topic;
    public readonly string SubscriptionName;
    readonly string fullName;
    internal static string Pattern = "^[a-z]+[a-z-]*[a-z]+$";
    private static Regex regex = new(Pattern, RegexOptions.Compiled);
    private const int SubscriptionNameMaximumLength = 36;

    public Subscription(Topic topic, string subscriber, string process)
    {
        if (topic == null)
        {
            throw new ArgumentException("Topic may not be null", nameof(topic));
        }
        if (!regex.IsMatch(subscriber ?? ""))
        {
            throw new ArgumentException("Subscriber name must match pattern: " + Pattern, nameof(subscriber));
        }
        if (!regex.IsMatch(process ?? ""))
        {
            throw new ArgumentException("Process name must match pattern: " + Pattern, nameof(process));
        }
            
        Topic = topic;
        SubscriptionName = $"{subscriber}_{process}";
        fullName = $"{Topic}_{SubscriptionName}";
            
        if (SubscriptionName.Length > SubscriptionNameMaximumLength) {
            throw new Exception($"The subscription name '{SubscriptionName}' exceeds the {SubscriptionNameMaximumLength} character limit");
        }
    }

    public override string ToString() => fullName;
    
    internal string EnvironmentName(string environment)
    {
        return $"{Topic.EnvironmentName(environment)}_{SubscriptionName}";
    }

    internal string EnvironmentDeadLetterName(string environment)
    {
        return $"{EnvironmentName(environment)}_dl";
    }
}