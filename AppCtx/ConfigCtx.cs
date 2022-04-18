using System.Net;

using Microsoft.Extensions.PlatformAbstractions;
using Yarp.ReverseProxy.Configuration;

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;

namespace HostIt.AppCtx;

public class Options
{
    public static string AppName
    {
        get
        {
            return nameof(HostIt);
        }
    }

    public static CommandNames? CommandName { get; set; }

    public static StaticOptions StaticOptions { get; set; }

    public static JsonOptions JsonOptions { get; set; }
}

#if false
    [Verb("process", HelpText = "Start and monitor a process with an optional reverse proxy")]
    public class ProcessOptions
    {
    }

    [Verb("reverse", HelpText = "Start a reverse proxy")]
    public class ReverseOptions
    {
    }
#endif

public enum CommandNames
{
    Json,
    Static
}

public class JsonOptions
{
    public string Path { get; internal set; }
}

public class StaticOptions
{
    public bool EnableDirectoryBrowsing { get; internal set; }

    public bool EnableDefaultFiles { get; internal set; }

    public string RequestPath { get; internal set; }

    public string? RootPath { get; internal set; }
}

public class ProcessOptions
{
    public string ExecutablePath { get; init; }

    public List<string> ArgumentList { get; } = new();

    public string StderrLogfile { get; init; }

    public string StdoutLogfile { get; init; }

    public string WorkingDirectory { get; set; }

    public string Username { get; set; }
}

public static class ConfigCtx
{
    public static Options Options { get; private set; }

    public static void ParseOptions(string[] args)
    {
        try
        {
            RootCommand rootCommand = new();

            rootCommand.Name = "HostIt";
            rootCommand.Description = "Reverse Proxy, Executable Watchdog, and Static File Server";
            
            Command jsonCommand = new Command("json")
            {
                new Argument<string>("path", description: "Path of Json File")
            };
            jsonCommand.Description = "Application Json File";
            jsonCommand.Handler = CommandHandler.Create<string>((path) =>
            {
                if (Options.CommandName is null) {
                    Options.CommandName = CommandNames.Json;
                } else {
                    throw new ArgumentException("Only One Command at a Time is Allowed");
                }

                Options.JsonOptions = new()
                {
                    Path = path
                };
            });

            Command staticCommand = new Command("static")
            {
                new Option<bool>("--enableDirectoryBrowsing", description: "Enable Directory Browsing of RootPath")
                {
                    IsRequired = false
                },
                new Option<bool>("--enableDefaultFiles", description: "Server a Default File (i.e. index.html)")
                {
                    IsRequired = false
                },
                new Option<string>("--requestPath", description: "Request Path in Url for Accessing Static Files")
                {
                    IsRequired = false
                },
                new Option<string>("--rootPath", description: "Directory where Static Files will be Served From")
                {
                    IsRequired = false
                }
            };
            staticCommand.Description = "Serve Static Files";
            staticCommand.Handler = CommandHandler.Create<bool, bool, string, string>((enableDirectoryBrowsing, enableDefaultFiles, requestPath, rootPath) =>
            {
                if (Options.CommandName is null) {
                    Options.CommandName = CommandNames.Static;
                } else {
                    throw new ArgumentException("Only One Command at a Time is Allowed");
                }

                Options.StaticOptions = new()
                {
                    EnableDirectoryBrowsing = enableDirectoryBrowsing,
                    EnableDefaultFiles = enableDefaultFiles,
                    RequestPath = requestPath ?? "",
                    RootPath = rootPath ?? ""
                };
            });

            rootCommand.Add(jsonCommand);
            rootCommand.Add(staticCommand);

            rootCommand.Invoke(args);

            // Not sure if there is a way to handle these via the library
            if (args.Any(a => new[] { "--version", "--help", "-h", "-?" }.Contains(a.Trim()))) {
                Environment.Exit(0);
            }

            if (Options.CommandName is null) {
                Environment.Exit(0);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            Console.WriteLine($"Try '{PlatformServices.Default.Application.ApplicationName} --help' for more information.");

            Environment.Exit(1);
        }   
    }
}
