using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

[assembly: InternalsVisibleTo("SynkedUp.AwsMessaging.UnitTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace SynkedUp.AwsMessaging;

public class DependencyInjectionConfig
{
    public static void ConfigureSubscriberServices(IServiceCollection services)
    {
        services.AddTransient<IMessageSerializer, MessageSerializer>();
    }
}