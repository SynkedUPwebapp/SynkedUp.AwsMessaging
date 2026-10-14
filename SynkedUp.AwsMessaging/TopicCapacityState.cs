namespace SynkedUp.AwsMessaging;

internal record CapacityResult(bool Acquired, TimeSpan WaitTime, string? LimitingQueueName, TimeSpan LimitingQueueEstimatedDelay);

internal class TopicCapacityState
{
    internal static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan LagAllowance = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan StaleSampleCutoff = TimeSpan.FromSeconds(15);
    internal static readonly TimeSpan MaximumSampleGap = TimeSpan.FromSeconds(15);
    internal const double SafetyFactor = 0.5;
    internal const double HeadroomSpreadIntervals = 3;
    internal const double MaximumRateGrowthFactor = 2;
    internal const double NearlyEmptyFraction = 0.1;
    private static readonly TimeSpan MinimumWaitTime = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan MaximumTokenBurst = TimeSpan.FromSeconds(1);

    private readonly double maximumMessageDelaySeconds;
    private readonly object stateLock = new();
    private readonly Dictionary<string, DrainEstimator> drainEstimators = new();
    private readonly PublishRecord publishRecord = new();
    private DateTimeOffset? sampledAt;
    private DateTimeOffset? sampleAttemptedAt;
    private bool hasQueues;
    private double allowedRatePerSecond;
    private double availableTokens;
    private DateTimeOffset tokensUpdatedAt;
    private string? limitingQueueName;
    private TimeSpan limitingQueueEstimatedDelay;

    public TopicCapacityState(int maximumMessageDelayMilliseconds)
    {
        maximumMessageDelaySeconds = maximumMessageDelayMilliseconds / 1000.0;
    }

    public SemaphoreSlim RefreshLock { get; } = new(1, 1);

    public double AllowedRatePerSecond
    {
        get
        {
            lock (stateLock)
            {
                return allowedRatePerSecond;
            }
        }
    }

    public string? LimitingQueueName
    {
        get
        {
            lock (stateLock)
            {
                return limitingQueueName;
            }
        }
    }

    public bool NeedsSample(DateTimeOffset now)
    {
        lock (stateLock)
        {
            return sampleAttemptedAt == null || now - sampleAttemptedAt.Value >= SampleInterval;
        }
    }

    public void RecordFailedSample(DateTimeOffset now)
    {
        lock (stateLock)
        {
            sampleAttemptedAt = now;
        }
    }

    public void ApplySample(DateTimeOffset now, IReadOnlyList<QueueDepth> depths)
    {
        lock (stateLock)
        {
            if (sampledAt != null && now - sampledAt.Value > MaximumSampleGap)
            {
                Reset();
            }

            RefillTokens(now);

            var secondsBetweenSamples = sampledAt == null ? 0 : (now - sampledAt.Value).TotalSeconds;
            var publishedBetweenSamples = sampledAt == null
                ? 0
                : publishRecord.CountBetween(sampledAt.Value - LagAllowance, now - LagAllowance);
            var unseenPublishes = publishRecord.CountAfter(now - LagAllowance);

            RemoveEstimatorsForMissingQueues(depths);

            var lowestQueueRate = double.MaxValue;
            foreach (var queueDepth in depths)
            {
                if (!drainEstimators.TryGetValue(queueDepth.QueueName, out var drainEstimator))
                {
                    drainEstimator = new DrainEstimator();
                    drainEstimators[queueDepth.QueueName] = drainEstimator;
                }
                drainEstimator.AddSample(queueDepth.Depth, publishedBetweenSamples, secondsBetweenSamples);

                var drainRate = drainEstimator.DrainRatePerSecond;
                var maximumDepth = drainRate * maximumMessageDelaySeconds;
                var effectiveDepth = queueDepth.Depth + unseenPublishes;
                var headroom = maximumDepth - effectiveDepth;
                var headroomRate = headroom / (SampleInterval.TotalSeconds * HeadroomSpreadIntervals);
                var queueRate = drainRate + (headroom > 0 ? headroomRate * SafetyFactor : headroomRate);
                if (queueDepth.Depth <= maximumDepth * NearlyEmptyFraction)
                {
                    queueRate = Math.Max(queueRate, allowedRatePerSecond * MaximumRateGrowthFactor);
                }

                if (queueRate < lowestQueueRate)
                {
                    lowestQueueRate = queueRate;
                    limitingQueueName = queueDepth.QueueName;
                    limitingQueueEstimatedDelay = TimeSpan.FromSeconds(effectiveDepth / drainRate);
                }
            }

            hasQueues = depths.Count > 0;
            if (!hasQueues)
            {
                limitingQueueName = null;
                limitingQueueEstimatedDelay = TimeSpan.Zero;
            }

            var growthLimit = Math.Max(allowedRatePerSecond, DrainEstimator.MinimumDrainRatePerSecond) * MaximumRateGrowthFactor;
            allowedRatePerSecond = hasQueues ? Math.Max(0, Math.Min(lowestQueueRate, growthLimit)) : 0;
            availableTokens = Math.Min(availableTokens, MaximumTokens());

            publishRecord.RemoveBefore(now - LagAllowance - TimeSpan.FromSeconds(1));
            sampledAt = now;
            sampleAttemptedAt = now;
        }
    }

    public CapacityResult TryTake(DateTimeOffset now)
    {
        lock (stateLock)
        {
            if (sampledAt == null || now - sampledAt.Value > StaleSampleCutoff)
            {
                return Wait(TimeUntilNextSample(now));
            }

            if (!hasQueues)
            {
                return Acquired();
            }

            RefillTokens(now);
            if (availableTokens >= 1)
            {
                availableTokens -= 1;
                publishRecord.Add(now);
                return Acquired();
            }

            var timeUntilNextSample = TimeUntilNextSample(now);
            if (allowedRatePerSecond <= 0)
            {
                return Wait(timeUntilNextSample);
            }

            var timeUntilNextToken = TimeSpan.FromSeconds((1 - availableTokens) / allowedRatePerSecond);
            return Wait(timeUntilNextToken < timeUntilNextSample ? timeUntilNextToken : timeUntilNextSample);
        }
    }

    private void Reset()
    {
        drainEstimators.Clear();
        publishRecord.Clear();
        allowedRatePerSecond = 0;
        availableTokens = 0;
        sampledAt = null;
    }

    private void RemoveEstimatorsForMissingQueues(IReadOnlyList<QueueDepth> depths)
    {
        var queueNames = new HashSet<string>(depths.Select(x => x.QueueName));
        foreach (var missingQueueName in drainEstimators.Keys.Where(x => !queueNames.Contains(x)).ToList())
        {
            drainEstimators.Remove(missingQueueName);
        }
    }

    private void RefillTokens(DateTimeOffset now)
    {
        if (now > tokensUpdatedAt)
        {
            var elapsedSeconds = (now - tokensUpdatedAt).TotalSeconds;
            availableTokens = Math.Min(MaximumTokens(), availableTokens + allowedRatePerSecond * elapsedSeconds);
        }
        tokensUpdatedAt = now;
    }

    private double MaximumTokens()
    {
        return Math.Max(1, allowedRatePerSecond * MaximumTokenBurst.TotalSeconds);
    }

    private TimeSpan TimeUntilNextSample(DateTimeOffset now)
    {
        var nextSampleAt = (sampleAttemptedAt ?? now) + SampleInterval;
        return nextSampleAt - now;
    }

    private CapacityResult Acquired()
    {
        return new CapacityResult(true, TimeSpan.Zero, limitingQueueName, limitingQueueEstimatedDelay);
    }

    private CapacityResult Wait(TimeSpan waitTime)
    {
        var boundedWaitTime = waitTime < MinimumWaitTime ? MinimumWaitTime : waitTime;
        return new CapacityResult(false, boundedWaitTime, limitingQueueName, limitingQueueEstimatedDelay);
    }
}
