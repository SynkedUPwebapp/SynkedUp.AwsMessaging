using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

internal class TopicCapacityStateTests
{
    private const int MaximumMessageDelayMilliseconds = 30000;
    private const string QueueName = "queue";
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public void When_there_has_been_no_sample_capacity_is_not_available()
    {
        var state = new TopicCapacityState(MaximumMessageDelayMilliseconds);

        var result = state.TryTake(Start);

        Assert.That(result.Acquired, Is.False);
        Assert.That(state.NeedsSample(Start), Is.True);
    }

    [Test]
    public void When_the_topic_has_no_queues_capacity_is_always_available()
    {
        var state = new TopicCapacityState(MaximumMessageDelayMilliseconds);
        state.ApplySample(Start, new List<QueueDepth>());

        var results = Enumerable.Range(0, 1000).Select(_ => state.TryTake(Start)).ToList();

        Assert.That(results.All(x => x.Acquired), Is.True);
    }

    [Test]
    public void When_the_sample_is_stale_capacity_is_not_available()
    {
        var state = new TopicCapacityState(MaximumMessageDelayMilliseconds);
        state.ApplySample(Start, Depths(0));

        var result = state.TryTake(Start + TopicCapacityState.StaleSampleCutoff + TimeSpan.FromSeconds(1));

        Assert.That(result.Acquired, Is.False);
    }

    [Test]
    public void When_a_sample_fails_the_next_sample_waits_for_the_sample_interval()
    {
        var state = new TopicCapacityState(MaximumMessageDelayMilliseconds);

        state.RecordFailedSample(Start);

        Assert.That(state.NeedsSample(Start + TimeSpan.FromSeconds(1)), Is.False);
        Assert.That(state.NeedsSample(Start + TopicCapacityState.SampleInterval), Is.True);
    }

    [Test]
    public void When_the_rate_increases_it_grows_by_at_most_the_maximum_growth_factor_per_sample()
    {
        var simulation = new Simulation(drainRatePerSecond: 100000, workerCount: 1);
        var previousRate = 0.0;

        for (var sample = 0; sample < 20; sample++)
        {
            simulation.RunFor(TopicCapacityState.SampleInterval);
            var rate = simulation.Workers[0].AllowedRatePerSecond;
            var growthLimit = Math.Max(previousRate, DrainEstimator.MinimumDrainRatePerSecond) * TopicCapacityState.MaximumRateGrowthFactor;
            Assert.That(rate, Is.LessThanOrEqualTo(growthLimit + 0.0001));
            previousRate = rate;
        }

        Assert.That(previousRate, Is.GreaterThan(1000));
    }

    [Test]
    public void When_one_worker_publishes_against_a_steady_consumer()
    {
        var simulation = new Simulation(drainRatePerSecond: 100, workerCount: 1);

        simulation.RunFor(TimeSpan.FromMinutes(5));
        var publishedBefore = simulation.TotalPublished;
        var maximumDepth = simulation.RunFor(TimeSpan.FromMinutes(2));
        var throughput = (simulation.TotalPublished - publishedBefore) / 120.0;

        Assert.That(throughput, Is.GreaterThan(90));
        Assert.That(maximumDepth, Is.LessThanOrEqualTo(100 * 30 * 1.1));
    }

    [Test]
    public void When_two_workers_share_one_queue()
    {
        var simulation = new Simulation(drainRatePerSecond: 100, workerCount: 2);

        simulation.RunFor(TimeSpan.FromMinutes(5));
        var publishedBefore = simulation.TotalPublished;
        var maximumDepth = simulation.RunFor(TimeSpan.FromMinutes(2));
        var throughput = (simulation.TotalPublished - publishedBefore) / 120.0;

        Assert.That(throughput, Is.GreaterThan(90));
        Assert.That(maximumDepth, Is.LessThanOrEqualTo(100 * 30 * 1.1));
    }

    [Test]
    public void When_real_time_traffic_uses_most_of_the_capacity()
    {
        var simulation = new Simulation(drainRatePerSecond: 100, workerCount: 1, realTimeRatePerSecond: 80);

        simulation.RunFor(TimeSpan.FromMinutes(5));
        var publishedBefore = simulation.TotalPublished;
        var maximumDepth = simulation.RunFor(TimeSpan.FromMinutes(2));
        var throughput = (simulation.TotalPublished - publishedBefore) / 120.0;

        Assert.That(throughput, Is.GreaterThan(15));
        Assert.That(throughput, Is.LessThanOrEqualTo(22));
        Assert.That(maximumDepth, Is.LessThanOrEqualTo(100 * 30 * 1.1));
    }

    [Test]
    public void When_publishes_take_time_to_become_visible_the_queue_does_not_overshoot()
    {
        var simulation = new Simulation(drainRatePerSecond: 100, workerCount: 1, visibilityDelay: TimeSpan.FromSeconds(3));

        var maximumDepth = simulation.RunFor(TimeSpan.FromMinutes(5));

        Assert.That(maximumDepth, Is.LessThanOrEqualTo(100 * 30 * 1.1));
    }

    [Test]
    public void When_the_consumer_is_stuck_publishing_stops()
    {
        var simulation = new Simulation(drainRatePerSecond: 0, workerCount: 1);

        simulation.RunFor(TimeSpan.FromMinutes(5));
        var publishedBefore = simulation.TotalPublished;
        simulation.RunFor(TimeSpan.FromMinutes(2));

        Assert.That(simulation.TotalPublished - publishedBefore, Is.EqualTo(0));
        Assert.That(simulation.Workers[0].AllowedRatePerSecond, Is.EqualTo(0));
        Assert.That(simulation.Workers[0].LimitingQueueName, Is.EqualTo(QueueName));
    }

    [Test]
    public void When_there_is_a_long_gap_between_samples_the_state_is_reset()
    {
        var simulation = new Simulation(drainRatePerSecond: 1000, workerCount: 1);
        simulation.RunFor(TimeSpan.FromMinutes(2));
        var state = simulation.Workers[0];
        Assert.That(state.AllowedRatePerSecond, Is.GreaterThan(100));

        state.ApplySample(simulation.Now + TopicCapacityState.MaximumSampleGap + TimeSpan.FromSeconds(1), Depths(0));

        Assert.That(state.AllowedRatePerSecond, Is.LessThanOrEqualTo(DrainEstimator.MinimumDrainRatePerSecond * TopicCapacityState.MaximumRateGrowthFactor));
    }

    [Test]
    public void When_many_callers_take_concurrently_no_more_than_the_available_tokens_are_issued()
    {
        var state = new TopicCapacityState(MaximumMessageDelayMilliseconds);
        state.ApplySample(Start, Depths(0));
        var now = Start + TimeSpan.FromSeconds(1);
        var expectedTokens = (int)Math.Floor(Math.Max(1, state.AllowedRatePerSecond));
        var acquiredCount = 0;

        Parallel.For(0, 1000, _ =>
        {
            if (state.TryTake(now).Acquired)
            {
                Interlocked.Increment(ref acquiredCount);
            }
        });

        Assert.That(acquiredCount, Is.EqualTo(expectedTokens));
    }

    private static List<QueueDepth> Depths(int depth)
    {
        return new List<QueueDepth> { new(QueueName, depth) };
    }

    private class Simulation
    {
        private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(20);
        private readonly double drainRatePerSecond;
        private readonly double realTimeRatePerSecond;
        private readonly TimeSpan visibilityDelay;
        private readonly Queue<DateTimeOffset> pendingPublishes = new();
        private double depth;
        private double realTimeRemainder;

        public Simulation(double drainRatePerSecond, int workerCount, double realTimeRatePerSecond = 0, TimeSpan visibilityDelay = default)
        {
            this.drainRatePerSecond = drainRatePerSecond;
            this.realTimeRatePerSecond = realTimeRatePerSecond;
            this.visibilityDelay = visibilityDelay;
            Workers = Enumerable.Range(0, workerCount)
                .Select(_ => new TopicCapacityState(MaximumMessageDelayMilliseconds))
                .ToList();
            Now = Start;
        }

        public List<TopicCapacityState> Workers { get; }
        public DateTimeOffset Now { get; private set; }
        public int TotalPublished { get; private set; }

        public double RunFor(TimeSpan duration)
        {
            var end = Now + duration;
            var maximumDepth = depth;
            while (Now < end)
            {
                foreach (var worker in Workers)
                {
                    if (worker.NeedsSample(Now))
                    {
                        worker.ApplySample(Now, Depths((int)Math.Floor(depth)));
                    }
                    while (worker.TryTake(Now).Acquired)
                    {
                        pendingPublishes.Enqueue(Now + visibilityDelay);
                        TotalPublished++;
                    }
                }

                while (pendingPublishes.Count > 0 && pendingPublishes.Peek() <= Now)
                {
                    pendingPublishes.Dequeue();
                    depth++;
                }

                realTimeRemainder += realTimeRatePerSecond * Step.TotalSeconds;
                var realTimeArrivals = Math.Floor(realTimeRemainder);
                realTimeRemainder -= realTimeArrivals;
                depth += realTimeArrivals;

                depth = Math.Max(0, depth - drainRatePerSecond * Step.TotalSeconds);
                maximumDepth = Math.Max(maximumDepth, depth);
                Now += Step;
            }
            return maximumDepth;
        }
    }
}
