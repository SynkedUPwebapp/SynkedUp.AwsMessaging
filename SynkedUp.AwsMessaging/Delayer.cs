namespace SynkedUp.AwsMessaging;

internal interface IDelayer
{
    Task Delay(int milliseconds);
}

internal class Delayer : IDelayer
{
    public async Task Delay(int milliseconds)
    {
        await Task.Delay(milliseconds);
    }
}