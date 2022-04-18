using Microsoft.Extensions.FileProviders;

using Yarp.ReverseProxy.Transforms;

using HostIt;
using HostIt.HostedServices;

Hub hub = new Hub(args);

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureHostOptions(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));

if (hub.HasJsonFile) {
    builder.Host.ConfigureAppConfiguration((hostingContext, config) =>
    {
        config.AddJsonFile(hub.JsonFilePath, optional: false);
    });
}

if (hub.HasReverseProxy) {
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

if (hub.HasStaticFile && hub.StaticFileMetaData.EnableDirectoryBrowsing) {
    builder.Services.AddDirectoryBrowser();
}

if (hub.HasProcesses) {
    MonitorProcessHostedService.Add(hub.ProcesseMetaDatas);

    builder.Services.AddHostedService<MonitorProcessHostedService>();
}

WebApplication app = builder.Build();

if (hub.HasStaticFile) {
    string rootPath = hub.StaticFileMetaData.RootPath;

    PhysicalFileProvider fileProvider = Path.IsPathRooted(rootPath) switch
    {
        true => new PhysicalFileProvider(rootPath),
        false => new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), rootPath))
    };

    app.UseFileServer(new FileServerOptions
    {
        FileProvider = fileProvider,
        EnableDefaultFiles = hub.StaticFileMetaData.EnableDefaultFiles,
        EnableDirectoryBrowsing = hub.StaticFileMetaData.EnableDirectoryBrowsing,
        RequestPath = hub.StaticFileMetaData.RequestPath
    });
}

if (hub.HasReverseProxy) {
    app.UseRouting();

    app.UseEndpoints(endpoints =>
    {
        endpoints.MapReverseProxy();
    });
}

await app.RunAsync();
