using System.Text;
using System.Text.Json;

using Yarp.ReverseProxy.Configuration;

using JsonCons.JsonPath;

namespace HostIt;

public class Hub
{
    public PortMetaData PortMetaData { get; } = new();

    public List<ProcessMetaData> ProcesseMetaDatas { get; } = new();

    public List<RouteConfig> RouteConfigs { get; } = new();

    public List<ClusterConfig> ClusterConfigs { get; } = new();

    public StaticFileMetaData StaticFileMetaData { get; private set; } = new();

    public void InitializeClusterConfig(string path)
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile(path)
            .Build();

        if (config.GetSection("ReverseProxy:Clusters").GetChildren().Count() == 0) {
            return;
        }

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));

        JsonSerializerOptions serializerOptions = new() { WriteIndented = true };
        JsonSelector selector = JsonSelector.Parse("$.ReverseProxy.Clusters[*]");
        IList<JsonElement> values = selector.Select(doc.RootElement);

        foreach ((JsonElement Cluster, int Index) jsonElement in values.Select((pmd, idx) => (pmd, idx)))
        {
            string clusterJson = JsonSerializer.Serialize(jsonElement.Cluster, serializerOptions);

            foreach (string tag in PortMetaData.Tags.Keys)
            {
                if (clusterJson.Contains($"{{{{{tag}}}}}")) {
                    clusterJson = clusterJson.Replace($"{{{{{tag}}}}}", PortMetaData.Tags[tag].ToString());
                }
            }

            MemoryStream ms = new();
            ms.Write(Encoding.Default.GetBytes("{ \"ClusterConfig\": " + clusterJson + "}"));
            ms.Position = 0;

            // Console.WriteLine(new StreamReader(ms).ReadToEnd());
            // ms.Position = 0;

            IConfiguration clusterSectionConfig = new ConfigurationBuilder()
                .AddJsonStream(ms)
                .Build();

            ClusterConfig clusterConfig = clusterSectionConfig
                .GetSection("ClusterConfig")
                .Get<ClusterConfig>();

            ClusterConfigs.Add(clusterConfig);
        }
    }

    public void InitializePortMetadata(string path)
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile(path)
            .Build();

        PortMetaData portMetaData = config
            .GetSection("PortMetaData")
            .Get<PortMetaData>();

        string[] names = config
            .GetSection("PortMetaData")
            .GetSection("Names")
            .GetChildren()
            .Select(v => v.Get<string>())
            .ToArray();

        foreach (string name in names)
        {
            PortMetaData.Tags.Add(name, PortMetaData.ReservePort());
        }
    }

    public void InitializeProcessMetaData(string path)
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile(path)
            .Build();

        if (config.GetSection("ProcessMetaData").GetChildren().Count() == 0) {
            return;
        }

        List<ProcessMetaData> processMetaDatas = config
            .GetSection("ProcessMetaData")
            .Get<ProcessMetaData[]>()
            .ToList();

        // Eww
        foreach (ProcessMetaData processMetaData in processMetaDatas)
        {
            foreach (int idx in Enumerable.Range(0, processMetaData.ArgumentList.Count()))
            {
                foreach (string tag in PortMetaData.Tags.Keys)
                {
                    if (processMetaData.ArgumentList[idx].Contains($"{{{{{tag}}}}}")) {
                        processMetaData.ArgumentList[idx] = processMetaData.ArgumentList[idx]
                            .Replace($"{{{{{tag}}}}}", PortMetaData.Tags[tag].ToString());
                    }
                }
            }

            ProcesseMetaDatas.Add(processMetaData);
        }
    }

    public void InitializeRouteConfig(string path)
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile(path)
            .Build();

        if (config.GetSection("ReverseProxy:Routes").GetChildren().Count() == 0) {
            return;
        }

        List<RouteConfig> routeConfigs = config
            .GetSection("ReverseProxy:Routes")
            .Get<RouteConfig[]>()
            .ToList();

        RouteConfigs.AddRange(routeConfigs);
    }

    public void InitializeStaticFiles(string path)
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile(path)
            .Build();

        IConfigurationSection joy = config.GetSection("StaticFiles");

        string[] root = joy.GetSection("RootPath")
            .GetChildren()
            .Select(v => v.Get<string>())
            .ToArray();

        bool enableDirectoryBrowsing = joy.GetValue<bool>("EnableDirectoryBrowsing");

        bool enableDefaultFiles = joy.GetValue<bool>("EnableDefaultFiles");

        string rootPath = root.Any() switch
        {
            true => Path.Combine(root),
            false => null
        };

        string requestPath = joy.GetValue<string>("RequestPath");

        StaticFileMetaData = new()
        {
            EnableDirectoryBrowsing = enableDirectoryBrowsing,
            EnableDefaultFiles = enableDefaultFiles,
            RequestPath = requestPath,
            RootPath = rootPath
        };
    }
}
