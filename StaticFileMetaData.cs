namespace HostIt;

public class StaticFileMetaData
{
    public bool EnableDirectoryBrowsing { get; init; }

    public bool EnableDefaultFiles { get; init; }

    public string RootPath { get; init; }

    public string RequestPath { get; init; }
}
