namespace SynkedUp.AwsMessaging;

internal interface IDelayer
{
    Task Delay(int milliseconds);
    Task Delay(int milliseconds, CancellationToken cancellationToken);
}

internal class Delayer : IDelayer
{
    public async Task Delay(int milliseconds)
    {
        await Task.Delay(milliseconds);
    }

    public async Task Delay(int milliseconds, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(milliseconds, cancellationToken);
        }
        catch (TaskCanceledException)
        {
        }
    }
}