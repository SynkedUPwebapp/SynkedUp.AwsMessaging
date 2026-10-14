using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

internal class DelayerTests
{
    [Test]
    public async Task When_the_delay_is_cancelled_it_returns_without_throwing()
    {
        var delayer = new Delayer();
        var cancellationTokenSource = new CancellationTokenSource(50);
        var stopwatch = Stopwatch.StartNew();

        await delayer.Delay(10000, cancellationTokenSource.Token);

        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000));
    }
}
