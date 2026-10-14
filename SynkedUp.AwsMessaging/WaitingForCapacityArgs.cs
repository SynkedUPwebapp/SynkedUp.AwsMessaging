namespace SynkedUp.AwsMessaging;

public delegate void OnWaitingForCapacity(object source, WaitingForCapacityArgs e);

public class WaitingForCapacityArgs : EventArgs
{
    public WaitingForCapacityArgs(Topic topic, string? limitingQueueName, TimeSpan limitingQueueEstimatedDelay, TimeSpan waitTime)
    {
        Topic = topic;
        LimitingQueueName = limitingQueueName;
        LimitingQueueEstimatedDelay = limitingQueueEstimatedDelay;
        WaitTime = waitTime;
    }

    public Topic Topic { get; }
    public string? LimitingQueueName { get; }
    public TimeSpan LimitingQueueEstimatedDelay { get; }
    public TimeSpan WaitTime { get; }
}
