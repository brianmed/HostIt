# HostIt

## Summary

HostIt is a reverse proxy, executable watchdog, and static file server with
dynamic port assignment between the reverse proxy and executables.  Also,
manual port assignment is possible.

HostIt has been has been tested with Docker (running the blogging engine
Ghost) and SSL with SNI via Kestrel.  

Supplied executables are ran at startup and killed during shutdown.  The 
executables should stay in the foreground.

## Commands

There are currently two supported commands "json" and "static".

The "json" command has a single argument: a json file (explained below).

The "static" command will start a static file server with the specified
options.  For example, directory browsing and implicity serving index.html is
supported.

## Command Line Arguments

The command line arguments are paritioned into two groups.  One is hostit
arguments and the other is ASP.Net arguments.  They are separated by "--".  For
example, "hostit static --enableDirectoryBrowsing -- '--urls=http://*:9000'" 
will run hostit as a static file server listening on port 9000.

## Configuration

The app is configured via a json file.  This is an IConfiguration compatible
json file with multiple sections.  They are "PortMetaData", "ProcessMetaData",
"ReverseProxy", and "StaticFiles".

See examples below.

They may turn in subsections under "HostIt" or similar in the future.

## Ports

The PortMetaData configuration section sets up two things.  One is the
beginning range for dyanmic port assignments and the other is the list of names
for each dynamically assigned port.

HostIt will replace an instance of {{name}} in the ArgumentList of a Process
and Cluster Address with the assigned port name.

## Examples

Below are example hostit.json and static.app1.com.json files.  Of note is that
three executables will be ran.  They are "app1", "docker", and "hostit".  All
three will be ran and restared on failure.

This example shows that "hostit" can be executed with different .json files for
different roles.

The hostit.json file configures "hostit" so that executables are ran and
monitoried  while also being a reverse proxy.

The static.app1.com.json file is for serving static files only via "hostit".

Also, please see the PortMetaData section for declaring Port Names and their use
in the ArgumentList of ProcessMetaData and Address of Clusters.

Running "$ ./hostit json hostit.json" should work with this example.

```json
// hostit.json
{
    "PortMetaData": {
        "RangeStart": 8080,
        "Names": [
            "app1.com",
            "static.app1.com",
            "blog.app1.com"
        ]
    },
    "ProcessMetaData": [
        {
            "ExecutablePath": "/srv/app1.com/app1",
            "StderrLogfile": "/srv/app1.com/stderr.log",
            "StdoutLogfile": "/srv/app1.com/stdout.log",
            "ArgumentList": [
                "--urls=http://127.0.0.1:{{app1.com}}"
            ],
            "WorkingDirectory": "/srv/app1.com",
            "Username": "www-data"
        },
        {
            "ExecutablePath": "./hostit",
            "StderrLogfile": "/srv/app1.com/stderrStatic.log",
            "StdoutLogfile": "/srv/app1.com/stdoutStatic.log",
            "ArgumentList": [
                "static",
                "--enableDefaultFiles",
                "--",
                "--urls=http://127.0.0.1:{{static.app1.com}}"
            ],
            "WorkingDirectory": "/srv/app1.com",
            "Username": "www-data"
        },
        {
            "ExecutablePath": "/usr/bin/docker",
            "StderrLogfile": "/srv/docker/ghost/stderr.log",
            "StdoutLogfile": "/srv/docker/ghost/stdout.log",
            "ArgumentList": [
                "run",
                "--rm",
                "-p",
                "{{blog.app1.com}}:2368",
                "-v", 
                "/opt/ghost/content:/var/lib/ghost/content",
                "-e",
                "url=https://blog.app1.com",
                "ghost"
            ],
            "WorkingDirectory": "/srv/docker/ghost"
        }
    ],
    "ReverseProxy": {
        "Routes": [
            {
                "RouteId" : "app1.com",
                "ClusterId": "app1.com",
                "Match": {
                    "Path": "{**catch-all}",
                    "Hosts": [ "app1.com" ]
                }
            },
            {
                "RouteId" : "static.app1.com",
                "ClusterId": "static.app1.com",
                "Match": {
                    "Path": "{**catch-all}",
                    "Hosts": [ "static.app1.com" ]
                }
            },
            {
                "RouteId" : "blog.app1.com",
                "ClusterId": "blog.app1.com",
                "Match": {
                    "Path": "{**catch-all}",
                    "Hosts": [ "blog.app1.com" ]
                }
            }
        ],
        "Clusters": [
            {
                "ClusterId": "app1.com",
                "Destinations": {
                    "app1.com/destination0": {
                        "Address": "http://127.0.0.1:{{app1.com}}"
                    }
                }
            },
            {
                "ClusterId": "static.app1.com",
                "Destinations": {
                    "static.app1.com/destination0": {
                        "Address": "http://127.0.0.1:{{static.app1.com}}"
                    }
                }
            },
            {
                "ClusterId": "blog.app1.com",
                "Destinations": {
                    "blog.app1.com/destination0": {
                        "Address": "http://127.0.0.1:{{blog.app1.com}}"
                    }
                }
            }
        ]
    }
}
```

Kestrel hostit.json section that supports Sni.

```json
{
    "Kestrel": {
        "Endpoints": {
            "Http": {
                "Url": "http://*"
            },
            "Https": {
                "Url": "https://*",
                "Sni": {
                    "app1.com": {
                        "Protocols": "Http1AndHttp2",
                        "SslProtocols": [ "Tls11", "Tls12", "Tls13"],
                        "Certificate": {
                            "Path": "/etc/letsencrypt/live/app1.com/cert.pem",
                            "KeyPath": "/etc/letsencrypt/live/app1.com/privkey.pem"
                        }
                    },
                    "blog.app1.com": {
                        "Protocols": "Http1AndHttp2",
                        "SslProtocols": [ "Tls11", "Tls12", "Tls13"],
                        "Certificate": {
                            "Path": "/etc/letsencrypt/live/blog.app1.com/cert.pem",
                            "KeyPath": "/etc/letsencrypt/live/blog.app1.com/privkey.pem"
                        }
                    }
                }
            }
        }
    }
}
```

A hostit.json file with static files only.

```json
{
    "StaticFiles": [
        {
            "RequestPath": "/images",
            "RootPath": [ "/", "Volumes", "Storage", "Images" ]
        }
    ]
}
```
