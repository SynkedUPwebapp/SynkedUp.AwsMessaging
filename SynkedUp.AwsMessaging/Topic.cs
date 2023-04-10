using System.Text.RegularExpressions;

namespace SynkedUp.AwsMessaging;

public class Topic
{
    private readonly string fullName;
    internal static string Pattern = "^[a-z]+[a-z-]*[a-z]+$";
    private static Regex regex = new(Pattern, RegexOptions.Compiled);
    private const int TopicNameMaximumLength = 36;
    private const int MaxEnvironmentLength = 3;

    public Topic(string publisher, string eventName, int version)
    {
        if (!regex.IsMatch(publisher ?? ""))
        {
            throw new ArgumentException("Publisher name must match pattern: " + Pattern, nameof(publisher));
        }
        if (!regex.IsMatch(eventName ?? ""))
        {
            throw new ArgumentException("Event name must match pattern: " + Pattern, nameof(eventName));
        }
        if (version < 0)
        {
            throw new ArgumentException("Version may not be negative", nameof(version));
        }

        fullName = $"{publisher}_{eventName}_v{version}";
            
        if (fullName.Length > TopicNameMaximumLength) {
            throw new Exception($"The topic name '{fullName}' exceeds the {TopicNameMaximumLength} character limit");
        }
    }

    public override string ToString() => fullName;

    internal string EnvironmentName(string environment)
    {
        if (environment.Length > MaxEnvironmentLength)
        {
            throw new Exception($"Environment {environment} must not exceed {MaxEnvironmentLength} characters");
        }
        return $"{environment}_{fullName}";
    }
}