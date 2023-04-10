using System.Runtime.CompilerServices;
using Amazon.SimpleNotificationService;
using Microsoft.Extensions.DependencyInjection;

[assembly: InternalsVisibleTo("SynkedUp.AwsMessaging.UnitTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace SynkedUp.AwsMessaging;

public class DependencyInjectionConfig
{
    public static void ConfigurePublisherServices(ServiceCollection services)
    {
        services.AddSingleton<IMessagePublisher, MessagePublisher>();
        
        services.AddTransient<IMessageSerializer, MessageSerializer>();
        services.AddTransient<IMessageMapper, MessageMapper>();
        services.AddTransient<ITopicArnCache, TopicArnCache>();
        
        services.AddAWSService<IAmazonSimpleNotificationService>();
    }

    public static void ConfigureSubscriberServices(IServiceCollection services)
    {
        services.AddTransient<IMessageSerializer, MessageSerializer>();
    }
}