namespace SynkedUp.AwsMessaging;

internal class DrainEstimator
{
    internal const double MinimumDrainRatePerSecond = 1;
    internal const double SmoothingFactor = 0.3;

    private int? previousDepth;
    private double smoothedDrainRatePerSecond = MinimumDrainRatePerSecond;

    public double DrainRatePerSecond => Math.Max(MinimumDrainRatePerSecond, smoothedDrainRatePerSecond);

    public void AddSample(int depth, int publishedBetweenSamples, double secondsBetweenSamples)
    {
        if (previousDepth == null || secondsBetweenSamples <= 0)
        {
            previousDepth = depth;
            return;
        }

        var depthChange = depth - previousDepth.Value;
        var sampledDrainRatePerSecond = Math.Max(0, (publishedBetweenSamples - depthChange) / secondsBetweenSamples);
        var queueStayedEmpty = previousDepth.Value == 0 && depth == 0;
        previousDepth = depth;

        if (queueStayedEmpty)
        {
            smoothedDrainRatePerSecond = Math.Max(smoothedDrainRatePerSecond, sampledDrainRatePerSecond);
            return;
        }

        smoothedDrainRatePerSecond = SmoothingFactor * sampledDrainRatePerSecond
                                     + (1 - SmoothingFactor) * smoothedDrainRatePerSecond;
    }
}
