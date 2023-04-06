using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

public class TopicTests
{
    [TestCase("monolith", "test-event", 1)]
    [TestCase("job-costing", "job-updated", 2)]
    [TestCase("zero-wing", "for-great-justice", 700)]
    [TestCase("insights", "user-event", 0)]
    public void When_creating_a_valid_topic(string publisher, string eventName, int version)
    {
        var topic = new Topic(publisher, eventName, version);

        Assert.That(topic.ToString(), Is.EqualTo($"{publisher}.{eventName}.v{version}"));
    }

    [TestCase("Monolith", "test-event", 1)]
    [TestCase("1job-costing", "job-updated", 2)]
    [TestCase("zero.wing", "for-great-justice", 700)]
    [TestCase("", "user-event", 0)]
    [TestCase(null, "user-event", 0)]
    public void When_attempting_to_create_a_topic_with_invalid_product_context(string publisher, string eventName, int version)
    {
        var exception = Assert.Catch(() => { var _ = new Topic(publisher, eventName, version); });
        
        Assert.That(exception!.Message, Is.EqualTo("Publisher name must match pattern: " + Topic.Pattern + " (Parameter 'publisher')"));
    }

    [TestCase("monolith", "test-Event", 1)]
    [TestCase("job-costing", "job-updated1", 2)]
    [TestCase("zero-wing", "for-great@justice", 700)]
    [TestCase("insights", "", 0)]
    [TestCase("insights", null, 0)]
    public void When_attempting_to_create_a_topic_with_invalid_event_name(string publisher, string eventName, int version)
    {
        var exception = Assert.Catch(() => { var _ = new Topic(publisher, eventName, version); });

        Assert.That(exception!.Message, Is.EqualTo("Event name must match pattern: " + Topic.Pattern + " (Parameter 'eventName')"));
    }

    [TestCase("monolith", "test-event", -1)]
    [TestCase("job-costing", "job-updated", -2)]
    public void When_attempting_to_create_a_topic_with_invalid_version(string publisher, string eventName, int version)
    {
        var exception = Assert.Catch(() => { var _ = new Topic(publisher, eventName, version); });

        Assert.That(exception!.Message, Is.EqualTo("Version may not be negative (Parameter 'version')"));
    }

    [Test]
    public void When_attempting_to_create_a_topic_with_a_name_which_is_too_long()
    {
        var publisher = "a-long-publisher-name";
        var eventName = "long-event-name";
        var version = 1;
        var exception = Assert.Catch(() => new Topic(
            publisher, 
            eventName,
            version));

        var expected = $"The topic name '{publisher}.{eventName}.v{version}' exceeds the 37 character limit";
        Assert.That(exception!.Message, Is.EqualTo(expected));
    }
}