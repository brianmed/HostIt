using System.Collections.Concurrent;
using System.Diagnostics;

namespace HostIt.HostedServices;

public class MonitorProcessHostedService : BackgroundService
{
    private static ConcurrentBag<ProcessMetaData> Processes { get; } = new();

    private static ConcurrentBag<int> AvailablePorts { get; } = new();

    private ILogger<MonitorProcessHostedService> Logger { get; set; }

    static MonitorProcessHostedService()
    {
        foreach (int port in Enumerable.Range(9000, 30))
        {
            AvailablePorts.Add(port);
        }
    }

    public MonitorProcessHostedService(ILogger<MonitorProcessHostedService> logger)
    {
        Logger = logger;
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
                    bool somethingHappened = false;

                    foreach (ProcessMetaData processMetaData in Processes)
                    {
                        if (processMetaData.Process is null) {
                            somethingHappened = true;

                            Start(processMetaData);
                        } else if (processMetaData.Process.HasExited) {
                            somethingHappened = true;

                            // Will restart the next iteration
                            Reap(processMetaData);
                        }
                    }

                    if (somethingHappened is false) {
                        Logger.LogInformation("All Processes Running");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Issue Monitoring Process");
                }
            }
        }
        catch (Exception ex) when(ex is not OperationCanceledException)
        {
            Logger.LogError(ex, "Issue Running PeriodicTimer");
        }
        finally
        {
            foreach (ProcessMetaData processMetaData in Processes)
            {
                if (processMetaData?.Process.HasExited is false) {
                    Logger.LogInformation($"Killing {processMetaData.ExecutablePath}");

                    processMetaData.Process.Kill(true);
                }
            }
        }
    }

    private void Initialize()
    {
        foreach (ProcessMetaData processMetaData in Processes)
        {
            Start(processMetaData);
        }
    }

    private void Reap(ProcessMetaData pmd)
    {
        pmd.Process.CancelErrorRead();
        pmd.Process.CancelOutputRead();

        pmd.Process.ErrorDataReceived -= OnReceivedProcessError;
        pmd.Process.OutputDataReceived -= OnReceivedProcessOutput;

        Logger.LogWarning($"Reaping: {pmd.ExecutablePath}: {pmd.Process.ExitCode}: {pmd.Process.ExitTime}");

        pmd.Process.Dispose();

        pmd.Process = null;
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
            WorkingDirectory = processMetaData.WorkingDirectory,
            UserName = processMetaData.Username
        };

        foreach (string argv in processMetaData.ArgumentList)
        {
            string _argv = processMetaData.PortMode switch
            {
                PortModes.Dynamic => argv.Replace("{{port}}", processMetaData.Port.ToString()),
                PortModes.Static => argv,
                _ => throw new Exception("Unknown Input Found")
            };

            startInfo.ArgumentList.Add(_argv);
        }

        Logger.LogInformation($"Starting Process: {processMetaData.ExecutablePath} {String.Join(" ", startInfo.ArgumentList)}");
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

    public string Username { get; set; }
}
