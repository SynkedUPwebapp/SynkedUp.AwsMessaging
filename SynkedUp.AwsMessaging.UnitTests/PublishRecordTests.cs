using System;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

internal class PublishRecordTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public void When_counting_publishes_between_two_times()
    {
        var publishRecord = new PublishRecord();
        publishRecord.Add(Start.AddSeconds(1));
        publishRecord.Add(Start.AddSeconds(2));
        publishRecord.Add(Start.AddSeconds(2));
        publishRecord.Add(Start.AddSeconds(3));

        Assert.That(publishRecord.CountBetween(Start.AddSeconds(1), Start.AddSeconds(2)), Is.EqualTo(2));
        Assert.That(publishRecord.CountAfter(Start.AddSeconds(1)), Is.EqualTo(3));
        Assert.That(publishRecord.CountAfter(Start), Is.EqualTo(4));
    }

    [Test]
    public void When_removing_old_publishes()
    {
        var publishRecord = new PublishRecord();
        publishRecord.Add(Start.AddSeconds(1));
        publishRecord.Add(Start.AddSeconds(2));
        publishRecord.Add(Start.AddSeconds(3));

        publishRecord.RemoveBefore(Start.AddSeconds(2));

        Assert.That(publishRecord.CountAfter(Start), Is.EqualTo(2));
    }

    [Test]
    public void When_clearing()
    {
        var publishRecord = new PublishRecord();
        publishRecord.Add(Start.AddSeconds(1));

        publishRecord.Clear();

        Assert.That(publishRecord.CountAfter(Start), Is.EqualTo(0));
    }
}
