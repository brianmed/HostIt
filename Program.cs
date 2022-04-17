using System.Text;
using System.Text.Json;

using Microsoft.Extensions.FileProviders;

using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;
using JsonCons.JsonPath;

using HostIt;
using HostIt.HostedServices;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

(List<RouteConfig> routeConfigs, List<ClusterConfig> clusterConfigs) = InitializeProcessMetaDataJson(args.FirstOrDefault("hostit.json"));

builder.Services
    .AddReverseProxy()
    // Without this, ghost was redirecting to 127.0.0.1 for ssl
    .AddTransforms(builderContext =>
    {
        builderContext.CopyRequestHeaders = true;
        builderContext.AddOriginalHost(useOriginal: true);
        builderContext.UseDefaultForwarders = true;
    })
    .LoadFromMemory(routeConfigs, clusterConfigs);

bool hasDirectoryBrowser = 
    builder.Configuration
        .GetSection("StaticFiles")
        .GetChildren()
        .Select(c => c
            .GetValue<bool>("DirectoryBrowser"))
        .Any();
    
if (hasDirectoryBrowser) {
    builder.Services.AddDirectoryBrowser();
}

if (routeConfigs.Any() && clusterConfigs.Any()) {
    builder.Services.AddHostedService<MonitorProcessHostedService>();
}

WebApplication app = builder.Build();

InitializeStaticFiles(app, args.FirstOrDefault("hostit.json"));

app.UseRouting();

app.UseEndpoints(endpoints =>
{
    endpoints.MapReverseProxy();
});

await app.RunAsync();

void InitializeStaticFiles(WebApplication app, string path)
{
    IConfiguration hostitConfig = new ConfigurationBuilder()
        .AddJsonFile(path)
        .Build();

    foreach (IConfigurationSection joy in hostitConfig.GetSection("StaticFiles").GetChildren())
    {
        string[] root = joy.GetSection("RootPath")
            .GetChildren()
            .Select(v => v.Get<string>())
            .ToArray();

        PhysicalFileProvider fileProvider = null;

        if (Path.IsPathRooted(root[0])) {
            fileProvider = new(Path.Combine(root));
        } else {
            fileProvider = new(Path.Combine(root.Prepend(builder.Environment.WebRootPath).ToArray()));
        }

        string requestPath = joy.GetValue<string>("RequestPath");

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = fileProvider,
            RequestPath = requestPath
        });

        app.UseDirectoryBrowser(new DirectoryBrowserOptions
        {
            FileProvider = fileProvider,
            RequestPath = requestPath
        });
    }
}

(List<RouteConfig>, List<ClusterConfig>) InitializeProcessMetaDataJson(string path)
{
    IConfiguration hostitConfig = new ConfigurationBuilder()
        .AddJsonFile(path)
        .Build();

    if (hostitConfig.GetSection("ProcessMetaData").GetChildren().Count() == 0) {
        return (Enumerable.Empty<RouteConfig>().ToList(), Enumerable.Empty<ClusterConfig>().ToList());
    }

    List<ProcessMetaData> processMetaDatas = hostitConfig
        .GetSection("ProcessMetaData")
        .Get<ProcessMetaData[]>()
        .ToList();

    // Initialize the ProcessMetaData with a port
    MonitorProcessHostedService.Add(processMetaDatas);

    using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));

    JsonSerializerOptions serializerOptions = new() { WriteIndented = true };
    JsonSelector selector = JsonSelector.Parse("$.ProcessMetaData[*]");
    IList<JsonElement> values = selector.Select(doc.RootElement);

    List<RouteConfig> routeConfigs = new();
    List<ClusterConfig> clusterConfigs = new();

    foreach ((JsonElement ProcessMetaData, int Index) jsonSection in values.Select((pmd, idx) => (pmd, idx)))
    {
        string pmdJson = JsonSerializer.Serialize(jsonSection.ProcessMetaData, serializerOptions);

        // All this for dynamic ports
        pmdJson = pmdJson
            .Replace("{{port}}", processMetaDatas[jsonSection.Index].Port.ToString());

        MemoryStream ms = new();
        ms.Write(Encoding.Default.GetBytes("{ \"ProcessMetaData\": " + pmdJson + "}"));
        ms.Position = 0;

        // Console.WriteLine(new StreamReader(ms).ReadToEnd());
        // ms.Position = 0;

        IConfiguration jsonSectionConfig = new ConfigurationBuilder()
            .AddJsonStream(ms)
            .Build();

        RouteConfig[] _routeConfigs = jsonSectionConfig
            .GetSection("ProcessMetaData:ReverseProxy:Routes")
            .Get<RouteConfig[]>()
            .ToArray();

        ClusterConfig[] _clusterConfigs = jsonSectionConfig
            .GetSection("ProcessMetaData:ReverseProxy:Clusters")
            .Get<ClusterConfig[]>()
            .ToArray();

        routeConfigs.AddRange(_routeConfigs);
        clusterConfigs.AddRange(_clusterConfigs);

        ms.Dispose();
    }

    return (routeConfigs, clusterConfigs);
}
