# HostIt

## Proof of Concept - Not Fit for Anyting

Reverse Proxy that supports running executables with dynamic port assignment.

Supplied executables are started via ProcessStartInfo and Killed during shutdown.

```json
{
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "Microsoft.AspNetCore": "Warning"
        }
    },
    "ProcessMetaData": [
        {
            "ExecutablePath": "/srv/App1",
            "StderrLogfile": "/var/log/app1/stderr.log",
            "StdoutLogfile": "/var/log/app1/stdout.log",
            "PortMode": "Dynamic",
            "ArgumentList": [
                "--urls=http://127.0.0.1:{port}"                
            ],
            "WorkingDirectory": "/srv/App1",
            "ReverseProxy": {
                "Routes": [
                    {
                        "RouteId" : "app1",
                        "ClusterId": "app1",
                        "Match": {
                            "Path": "{**catch-all}",
                            "Hosts": [ "app1.com" ]
                        }
                    }
                ],
                "Clusters": [
                    {
                        "ClusterId": "app1",
                        "Destinations": {
                            "app1/destination0": {
                                "Address": "http://127.0.0.1:{port}"
                            }
                        }
                    }
                ]
            }
        },
        {
            "ExecutablePath": "/srv/App2",
            "StderrLogfile": "/var/log/app2/stderr.log",
            "StdoutLogfile": "/var/log/app2/stdout.log",
            "PortMode": "Dynamic",
            "ArgumentList": [
                "--urls=http://127.0.0.1:{port}"
            ],
            "WorkingDirectory": "/srv/app2",
            "ReverseProxy": {
                "Routes": [
                    {
                        "RouteId" : "app2",
                        "ClusterId": "app2",
                        "Match": {
                            "Path": "{**catch-all}",
                            "Hosts": [ "app2.com" ]
                        }
                    }
                ],
                "Clusters": [
                    {
                        "ClusterId": "app2",
                        "Destinations": {
                            "app2/destination0": {
                                "Address": "http://127.0.0.1:{port}"
                            }
                        }
                    }
                ]
            }
        }
    ],
    "AllowedHosts": "*"
}
```
