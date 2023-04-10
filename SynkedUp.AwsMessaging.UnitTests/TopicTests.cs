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

        Assert.That(topic.ToString(), Is.EqualTo($"{publisher}_{eventName}_v{version}"));
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

        var expected = $"The topic name '{publisher}_{eventName}_v{version}' exceeds the 36 character limit";
        Assert.That(exception!.Message, Is.EqualTo(expected));
    }

    [Test]
    public void When_getting_the_environment_name()
    {
        var topic = new Topic("publisher", "event-name", 1);
        var environment = "dev";
        
        Assert.That(topic.EnvironmentName(environment), Is.EqualTo($"{environment}_publisher_event-name_v1"));
    }
    
    [Test]
    public void When_getting_the_environment_name_but_the_environment_is_too_long()
    {
        var topic = new Topic("publisher", "event-name", 1);
        var environment = "1234";
        
        var exception = Assert.Catch(() => topic.EnvironmentName(environment));
        
        Assert.That(exception!.Message, Is.EqualTo($"Environment {environment} must not exceed 3 characters"));
    }
}