namespace SynkedUp.AwsMessaging;

internal class PublishRecord
{
    private static readonly long BucketTicks = TimeSpan.FromMilliseconds(100).Ticks;
    private readonly LinkedList<PublishBucket> buckets = new();

    public void Add(DateTimeOffset publishedAt)
    {
        var bucketStartTicks = publishedAt.UtcTicks - publishedAt.UtcTicks % BucketTicks;
        var lastBucket = buckets.Last?.Value;
        if (lastBucket != null && lastBucket.StartTicks == bucketStartTicks)
        {
            lastBucket.Count++;
            return;
        }
        buckets.AddLast(new PublishBucket(bucketStartTicks));
    }

    public int CountBetween(DateTimeOffset startExclusive, DateTimeOffset endInclusive)
    {
        return buckets
            .Where(x => x.StartTicks > startExclusive.UtcTicks && x.StartTicks <= endInclusive.UtcTicks)
            .Sum(x => x.Count);
    }

    public int CountAfter(DateTimeOffset startExclusive)
    {
        return buckets.Where(x => x.StartTicks > startExclusive.UtcTicks).Sum(x => x.Count);
    }

    public void RemoveBefore(DateTimeOffset cutoff)
    {
        while (buckets.First != null && buckets.First.Value.StartTicks < cutoff.UtcTicks)
        {
            buckets.RemoveFirst();
        }
    }

    public void Clear()
    {
        buckets.Clear();
    }

    private class PublishBucket(long startTicks)
    {
        public long StartTicks { get; } = startTicks;
        public int Count { get; set; } = 1;
    }
}
