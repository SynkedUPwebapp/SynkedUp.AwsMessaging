using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

public class SubscriptionTests
{
    [TestCase("job-costing", "sign-in-handler")]
    [TestCase("data-context", "monolith-listener")]
    public void When_creating_a_subscription(string subscriber, string process)
    {
        var topic = new Topic("monolith", "user-signed-in", 1);
        var subscription = new Subscription(topic, subscriber, process);

        Assert.That(subscription.Topic, Is.SameAs(topic));
        Assert.That(subscription.SubscriptionName, Is.EqualTo($"{subscriber}_{process}"));
        Assert.That(subscription.ToString(), Is.EqualTo($"{topic}_{subscription.SubscriptionName}"));
    }

    [Test]
    public void When_attempting_to_create_a_subscription_with_an_invalid_topic()
    {
        var exception = Assert.Catch(() => { var _ = new Subscription(null!, "test", "listener"); });

        Assert.That(exception!.Message, Is.EqualTo("Topic may not be null (Parameter 'topic')"));
    }

    [TestCase("JobCosting", "sign-in-handler")]
    [TestCase(null, "sign-in-handler")]
    [TestCase("data.context", "monolith-listener")]
    [TestCase("data context", "monolith-listener")]
    public void When_attempting_to_create_a_subscription_with_an_invalid_subscriber(string subscriber, string process)
    {
        var topic = new Topic("monolith", "user-signed-in", 1);

        var exception = Assert.Catch(() => { var _ = new Subscription(topic, subscriber, process); });

        Assert.That(exception!.Message,
            Is.EqualTo("Subscriber name must match pattern: " + Subscription.Pattern + " (Parameter 'subscriber')"));
    }

    [TestCase("job-costing", "")]
    [TestCase("data-context", "^MONOLITH")]
    [TestCase("insights", null)]
    public void When_attempting_to_create_a_subscription_with_an_invalid_process_name(string subscriber,
        string process)
    {
        var topic = new Topic("monolith", "user-signed-in", 1);

        var exception = Assert.Catch(() => { var _ = new Subscription(topic, subscriber, process); });

        Assert.That(exception!.Message,
            Is.EqualTo("Process name must match pattern: " + Subscription.Pattern + " (Parameter 'process')"));
    }

    [Test]
    public void When_attempting_to_create_a_subscription_but_the_name_is_too_long()
    {
        var topic = new Topic("monolith", "user-signed-in", 1);
        var subscriber = "really-long-subscriber-name";
        var process = "some-listener";

        var exception = Assert.Catch(() => { var _ = new Subscription(topic, subscriber, process); });

        var expected = $"The subscription name '{subscriber}_{process}' exceeds the 36 character limit";
        Assert.That(exception!.Message, Is.EqualTo(expected));
    }
    
    [Test]
    public void When_getting_the_environment_name()
    {
        var topic = new Topic("publisher", "event-name", 1);
        var subscription = new Subscription(topic, "subscriber", "process");
        var environment = "dev";
        
        Assert.That(subscription.EnvironmentName(environment), Is.EqualTo("dev_publisher_event-name_v1_subscriber_process"));
    }

    [Test]
    public void When_getting_the_environment_dead_letter_queue_name()
    {
        var topic = new Topic("publisher", "event-name", 1);
        var subscription = new Subscription(topic, "subscriber", "process");
        var environment = "dev";
        
        Assert.That(subscription.EnvironmentDeadLetterName(environment), Is.EqualTo("dev_publisher_event-name_v1_subscriber_process_dl"));
    }
}