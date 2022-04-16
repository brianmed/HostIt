using Microsoft.Extensions.DependencyInjection;

using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

using HostIt;
using HostIt.HostedServices;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

List<ProcessMetaData> processMetaDatas = builder.Configuration
    .GetSection("ProcessMetaData")
    .Get<ProcessMetaData[]>()
    .ToList();

MonitorProcessHostedService.Add(processMetaDatas);

RouteConfig[] routeConfigs = builder.Configuration
    .GetSection("ProcessMetaData")
    .GetChildren()
    .Select(c => c.GetSection("ReverseProxy:Routes").Get<RouteConfig[]>())
    .SelectMany(c => c)
    .ToArray();

List<ClusterConfig> clusterConfigs = new();

foreach ((ClusterConfig Instance, int Index) clusterConfig in builder.Configuration
    .GetSection("ProcessMetaData")
    .GetChildren()
    .Select(c => c
        .GetSection("ReverseProxy:Clusters")
        .Get<ClusterConfig[]>())
    .SelectMany(c => c)
    .Select((c, idx) => (c, idx)))
{
    string destinationAddress = clusterConfig.Instance
        .Destinations.First().Value.Address;

    if (processMetaDatas[clusterConfig.Index].PortMode == PortModes.Dynamic) {
        destinationAddress = destinationAddress
            .Replace("{port}", processMetaDatas[clusterConfig.Index].Port.ToString());
    }

    clusterConfigs.Add(new ClusterConfig()
    {
        ClusterId = clusterConfig.Instance.ClusterId,
        Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
        {
            {
                clusterConfig.Instance.Destinations.First().Key,
                new DestinationConfig()
                {
                    Address = destinationAddress
                }
            }
        }        
    });
}

builder.Services
    .AddReverseProxy()
    .LoadFromMemory(routeConfigs, clusterConfigs);

builder.Services.AddHostedService<MonitorProcessHostedService>();

WebApplication app = builder.Build();

app.UseRouting();

app.UseEndpoints(endpoints =>
{
    endpoints.MapReverseProxy();
});

await app.RunAsync();
