using System.Collections.Concurrent;
using System.Diagnostics;

namespace HostIt.HostedServices;

public class MonitorProcessHostedService : BackgroundService
{
    private static ConcurrentBag<ProcessMetaData> Processes { get; } = new();

    private static ConcurrentBag<int> AvailablePorts { get; } = new();

    static MonitorProcessHostedService()
    {
        foreach (int port in Enumerable.Range(9000, 30))
        {
            AvailablePorts.Add(port);
        }
    }

    public static void Add(List<ProcessMetaData> processMetaDatas)
    {
        foreach (ProcessMetaData processMetaData in processMetaDatas)
        {
            Add(processMetaData);
        }
    }

    public static void Add(ProcessMetaData processMetaData)
    {
        if (AvailablePorts.TryTake(out int port)) {
            processMetaData.Port = port;

            Processes.Add(processMetaData);
        } else {
            throw new ArgumentOutOfRangeException("No available ports");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Initialize();

        using PeriodicTimer periodicTimer = new(TimeSpan.FromSeconds(30));

        try
        {
            while (await periodicTimer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    foreach (ProcessMetaData processMetaData in Processes)
                    {
                        Console.WriteLine($"{processMetaData.ExecutablePath} {(processMetaData.Process.HasExited ? "Exited" : "Running")}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Issue Monitoring Process {ex}");
                }
            }

            foreach (ProcessMetaData processMetaData in Processes)
            {
                if (processMetaData?.Process.HasExited is false) {
                    processMetaData.Process.Kill(true);
                }
            }
        }
        catch (Exception ex) when(ex is not OperationCanceledException)
        {
            Console.WriteLine($"Issue Running PeriodicTimer {ex}");
        }
    }

    private void Initialize()
    {
        foreach (ProcessMetaData processMetaData in Processes)
        {
            Start(processMetaData);
        }
    }

    private void Start(ProcessMetaData processMetaData)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo(processMetaData.ExecutablePath)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = processMetaData.WorkingDirectory
        };

        foreach (string argv in processMetaData.ArgumentList)
        {
            string _argv = processMetaData.PortMode switch
            {
                PortModes.Dynamic => argv.Replace("{port}", processMetaData.Port.ToString()),
                PortModes.Static => argv,
                _ => throw new Exception("Unknown Input Found")
            };

            startInfo.ArgumentList.Add(_argv);
        }

        Console.WriteLine($"Starting: {processMetaData.ExecutablePath}");
        processMetaData.Process = Process.Start(startInfo);

        processMetaData.Process.StandardInput.AutoFlush = true;
        processMetaData.Process.ErrorDataReceived += OnReceivedProcessError;
        processMetaData.Process.OutputDataReceived += OnReceivedProcessOutput;
        processMetaData.Process.BeginErrorReadLine();
        processMetaData.Process.BeginOutputReadLine();
    }

    public static void OnReceivedProcessError(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not null) {
            ProcessMetaData processMetaData = Processes
                .Where(p => p.Process == sender)
                .Single();

            File.AppendAllLines(
                processMetaData.StderrLogfile, 
                new[] { e.Data.Replace("\r", "").Replace("\n", "") });
        }
    }

    public static void OnReceivedProcessOutput(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not null) {
            ProcessMetaData processMetaData = Processes
                .Where(p => p.Process == sender)
                .Single();

            File.AppendAllLines(
                processMetaData.StdoutLogfile, 
                new[] { e.Data.Replace("\r", "").Replace("\n", "") });
        }
    }
}

public enum PortModes
{
    Dynamic,
    Static
}

public class ProcessMetaData
{
    public PortModes PortMode { get; init; }

    public int Port { get; set; }

    public string ExecutablePath { get; init; }

    public List<string> ArgumentList { get; } = new();

    public string StderrLogfile { get; init; }

    public string StdoutLogfile { get; init; }

    public Process Process { get; set; }

    public string WorkingDirectory { get; set; }
}
