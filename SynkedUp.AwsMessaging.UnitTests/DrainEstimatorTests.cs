using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

internal class DrainEstimatorTests
{
    [Test]
    public void When_there_are_no_samples_the_minimum_drain_rate_is_used()
    {
        var drainEstimator = new DrainEstimator();

        Assert.That(drainEstimator.DrainRatePerSecond, Is.EqualTo(DrainEstimator.MinimumDrainRatePerSecond));
    }

    [Test]
    public void When_there_is_only_one_sample_the_minimum_drain_rate_is_used()
    {
        var drainEstimator = new DrainEstimator();

        drainEstimator.AddSample(500, 0, 0);

        Assert.That(drainEstimator.DrainRatePerSecond, Is.EqualTo(DrainEstimator.MinimumDrainRatePerSecond));
    }

    [Test]
    public void When_the_queue_shrinks_while_publishing_the_drain_includes_published_messages()
    {
        var drainEstimator = new DrainEstimator();
        drainEstimator.AddSample(1000, 0, 0);

        drainEstimator.AddSample(900, 400, 5);

        var expectedSample = (400 + 100) / 5.0;
        var expected = DrainEstimator.SmoothingFactor * expectedSample
                       + (1 - DrainEstimator.SmoothingFactor) * DrainEstimator.MinimumDrainRatePerSecond;
        Assert.That(drainEstimator.DrainRatePerSecond, Is.EqualTo(expected).Within(0.0001));
    }

    [Test]
    public void When_repeated_samples_are_consistent_the_estimate_converges()
    {
        var drainEstimator = new DrainEstimator();
        drainEstimator.AddSample(1000, 0, 0);

        for (var i = 0; i < 50; i++)
        {
            drainEstimator.AddSample(1000, 250, 5);
        }

        Assert.That(drainEstimator.DrainRatePerSecond, Is.EqualTo(50).Within(0.01));
    }

    [Test]
    public void When_the_queue_grows_faster_than_publishing_the_estimate_falls_to_the_minimum()
    {
        var drainEstimator = new DrainEstimator();
        drainEstimator.AddSample(1000, 0, 0);
        for (var i = 0; i < 20; i++)
        {
            drainEstimator.AddSample(1000, 250, 5);
        }

        var depth = 1000;
        for (var i = 0; i < 50; i++)
        {
            depth += 500;
            drainEstimator.AddSample(depth, 100, 5);
        }

        Assert.That(drainEstimator.DrainRatePerSecond, Is.EqualTo(DrainEstimator.MinimumDrainRatePerSecond).Within(0.01));
    }

    [Test]
    public void When_the_queue_stays_empty_without_publishing_the_estimate_is_kept()
    {
        var drainEstimator = new DrainEstimator();
        drainEstimator.AddSample(0, 0, 0);
        for (var i = 0; i < 50; i++)
        {
            drainEstimator.AddSample(0, 500, 5);
        }
        var estimateBeforeIdle = drainEstimator.DrainRatePerSecond;

        for (var i = 0; i < 50; i++)
        {
            drainEstimator.AddSample(0, 0, 5);
        }

        Assert.That(drainEstimator.DrainRatePerSecond, Is.EqualTo(estimateBeforeIdle));
    }
}
