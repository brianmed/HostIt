using Microsoft.Extensions.FileProviders;

using Yarp.ReverseProxy.Transforms;

using HostIt;
using HostIt.HostedServices;

string appConfigJsonFile = args.FirstOrDefault("hostit.json");

Hub hub = new Hub();

hub.InitializePortMetadata(appConfigJsonFile);
hub.InitializeProcessMetaData(appConfigJsonFile);
hub.InitializeRouteConfig(appConfigJsonFile);
hub.InitializeClusterConfig(appConfigJsonFile);
hub.InitializeStaticFiles(appConfigJsonFile);

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureAppConfiguration((hostingContext, config) =>
{
    config.AddJsonFile(appConfigJsonFile, optional: false);
});

if (hub.RouteConfigs.Any() || hub.ClusterConfigs.Any()) {
    builder.Services
        .AddReverseProxy()
        // Without the original host header, ghost was redirecting to 127.0.0.1 for ssl
        .AddTransforms(builderContext =>
        {
            builderContext.CopyRequestHeaders = true;
            builderContext.AddOriginalHost(useOriginal: true);
            builderContext.UseDefaultForwarders = true;
        })
        .LoadFromMemory(hub.RouteConfigs, hub.ClusterConfigs);
}
    
if (hub.StaticFileMetaData.EnableDirectoryBrowsing) {
    builder.Services.AddDirectoryBrowser();
}

if (hub.ProcesseMetaDatas.Any()) {
    MonitorProcessHostedService.Add(hub.ProcesseMetaDatas);

    builder.Services.AddHostedService<MonitorProcessHostedService>();
}

WebApplication app = builder.Build();

if (hub.StaticFileMetaData.RootPath is string rootPath && rootPath is not null) {
    PhysicalFileProvider fileProvider = Path.IsPathRooted(rootPath) switch
    {
        true => new PhysicalFileProvider(rootPath),
        false => new PhysicalFileProvider(Path.Combine(builder.Environment.WebRootPath, rootPath))
    };

    app.UseFileServer(new FileServerOptions
    {
        FileProvider = fileProvider,
        EnableDefaultFiles = hub.StaticFileMetaData.EnableDefaultFiles,
        EnableDirectoryBrowsing = hub.StaticFileMetaData.EnableDirectoryBrowsing
    });
}

if (hub.RouteConfigs.Any() || hub.ClusterConfigs.Any()) {
    app.UseRouting();

    app.UseEndpoints(endpoints =>
    {
        endpoints.MapReverseProxy();
    });
}

await app.RunAsync();
