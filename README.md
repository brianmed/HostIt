# HostIt

## Summary

The main features of HostIt are being a reverse proxy, running and monitoring
executables, and static file serving.  In addition, the port assignment between
executables and the reverse proxy can be assigned at run time.

HostIt has been has been tested with Docker (running the blogging engine
Ghost).  

Also, SSL with SNI is supported via Kestrel.  

Supplied executables are ran at startup and killed during shutdown.  The 
executables should stay in the foreground.

## Configuration

The app is configured via a hostit.json file.  This is an IConfiguration
compatible json file with multiple sections.  They are "PortMetaData", 
"ProcessMetaData", "ReverseProxy", and "StaticFiles".

See examples below.

They may turn in subsections under "HostIt" or similar in the future.

## Examples

Below are example hostit.json and static.app1.com.json files.  Of note is that
three executables will be ran.  They are "app1", "docker", and "hostit".  All
three will be ran and restared on failure.

This example shows that "hostit" can be executed with different .json files for
different roles.  The hostit.json file is when "hostit" should run and monitor
executables while being a reverse proxy.  The static.app1.com.json file 
is for serving static files only via "hostit".

Also, please see the PortMetaData section for declaring Port Names and their use
in the ArgumentList of ProcessMetaData and Address of Clusters.

Simply running ./hostit should work with this example.

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
                "static.app1.com.json",
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

// static.app1.com.json
{
    "StaticFiles": {
        "EnableDefaultFiles": true,
        "RequestPath": "",
        "RootPath": [ "/", "opt", "bmedley.org", "wwwroot" ]
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
