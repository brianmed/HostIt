# HostIt

## Proof of Concept - Not Fit for Anyting

Reverse Proxy that supports running executables with dynamic port assignment.  In addition, SSL with SNI is supported via Kestrel.

Supplied executables are started via ProcessStartInfo and Killed during shutdown.

Below is an example appsettings.json that will run two executables and reverse proxy them with dynamic port assignment.

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

Kestrel appsettings.json section that supports Sni.

```json
{
    "Kestrel": {
        "Endpoints": {
            "EndpointName": {
                "Url": "https://*",
                "Sni": {
                    "app1": {
                        "Protocols": "Http1AndHttp2",
                        "SslProtocols": [ "Tls11", "Tls12", "Tls13"],
                        "Certificate": {
                            "Path": "/etc/letsencrypt/live/app1/cert.pem",
                            "KeyPath": "/etc/letsencrypt/live/app1/privkey.pem"
                        }
                    },
                    "app2": {
                        "Protocols": "Http1AndHttp2",
                        "SslProtocols": [ "Tls11", "Tls12", "Tls13"],
                        "Certificate": {
                            "Path": "/etc/letsencrypt/live/app2/cert.pem",
                            "KeyPath": "/etc/letsencrypt/live/app2/privkey.pem"
                        }
                    }
                }
            }
        }
    }
}
```
