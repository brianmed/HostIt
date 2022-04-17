using System.Diagnostics;

namespace HostIt;

public class ProcessMetaData
{
    public int Port { get; set; }

    public string ExecutablePath { get; init; }

    public List<string> ArgumentList { get; } = new();

    public string StderrLogfile { get; init; }

    public string StdoutLogfile { get; init; }

    public Process Process { get; set; }

    public string WorkingDirectory { get; set; }

    public string Username { get; set; }
}
