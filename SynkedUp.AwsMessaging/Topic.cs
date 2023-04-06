using System.Text.RegularExpressions;

namespace SynkedUp.AwsMessaging;

public class Topic
{
    private readonly string fullName;
    internal static string Pattern = "^[a-z]+[a-z-]*[a-z]+$";
    private static Regex regex = new(Pattern, RegexOptions.Compiled);
    private const int TopicNameMaximumLength = 37;

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

        fullName = $"{publisher}.{eventName}.v{version}";
            
        if (fullName.Length > TopicNameMaximumLength) {
            throw new Exception($"The topic name '{fullName}' exceeds the {TopicNameMaximumLength} character limit");
        }
    }

    public override string ToString() => fullName;
}