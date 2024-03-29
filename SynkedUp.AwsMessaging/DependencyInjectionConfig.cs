using System.Runtime.CompilerServices;
using Amazon.Scheduler;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection;

[assembly: InternalsVisibleTo("SynkedUp.AwsMessaging.UnitTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace SynkedUp.AwsMessaging;

public class DependencyInjectionConfig
{
    public static void ConfigurePublisherServices(IServiceCollection services)
    {
        services.AddSingleton<IMessagePublisher, MessagePublisher>();

        services.AddTransient<IMessageMapper, MessageMapper>();
        services.AddTransient<IMessageSerializer, MessageSerializer>();
        services.AddTransient<ITopicArnCache, TopicArnCache>();
        
        services.AddAWSService<IAmazonSimpleNotificationService>();
        services.AddAWSService<IAmazonScheduler>();
    }

    public static void ConfigureSubscriberServices(IServiceCollection services)
    {
        services.AddSingleton<IMessageSubscriber, MessageSubscriber>();

        services.AddTransient<IDelayer, Delayer>();
        services.AddTransient<IMessageMapper, MessageMapper>();
        services.AddTransient<IMessageSerializer, MessageSerializer>();
        services.AddTransient<IQueueUrlRetriever, QueueUrlRetriever>();
        services.AddTransient<IQueueCreator, QueueCreator>();
        services.AddTransient<IRetryingTopicSubscriber, RetryingTopicSubscriber>();
        services.AddTransient<ITopicArnCache, TopicArnCache>();

        services.AddAWSService<IAmazonSQS>();
        services.AddAWSService<IAmazonSimpleNotificationService>();
    }
}