using System.Text;
using System.Text.Json;

using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;
using JsonCons.JsonPath;

using HostIt;
using HostIt.HostedServices;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

(List<RouteConfig> routeConfigs, List<ClusterConfig> clusterConfigs) = InitializeProcessMetaDataJson("hostit.json");

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

builder.Services.AddHostedService<MonitorProcessHostedService>();

WebApplication app = builder.Build();

app.UseRouting();

app.UseEndpoints(endpoints =>
{
    endpoints.MapReverseProxy();
});

await app.RunAsync();

(List<RouteConfig>, List<ClusterConfig>) InitializeProcessMetaDataJson(string path)
{
    IConfiguration hostitConfig = new ConfigurationBuilder()
        .AddJsonFile(path)
        .Build();

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
