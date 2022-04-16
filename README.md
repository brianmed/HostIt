# HostIt

Reverse Proxy that supports running executables with dynamic port assignment.
In addition, SSL with SNI is supported via Kestrel.  Also, has been tested with
Docker.

Supplied executables are started via ProcessStartInfo and Killed during
shutdown.

The app is configured via a hostit.json file.  This is an IConfiguration
compatible json file with a single key dictionary "ProcessMetaData" that is an
array.

Below is an example hostit.json that will run one executable and one docker
container and reverse proxy them with dynamic port assignment.

```json
{
    "ProcessMetaData": [
        {
            "ExecutablePath": "/srv/App1",
            "StderrLogfile": "/var/log/app1/stderr.log",
            "StdoutLogfile": "/var/log/app1/stdout.log",
            "PortMode": "Dynamic",
            "ArgumentList": [
                "--urls=http://127.0.0.1:{{port}}"                
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
                                "Address": "http://127.0.0.1:{{port}}"
                            }
                        }
                    }
                ]
            }
        },
        {
            "ExecutablePath": "/usr/bin/docker",
            "StderrLogfile": "/srv/docker/ghost/stderr.log",
            "StdoutLogfile": "/srv/docker/ghost/stdout.log",
            "PortMode": "Dynamic",
            "ArgumentList": [
                "run",
                "--rm",
                "-p",
                "{{port}}:2368",
                "-v", 
                "/opt/ghost/content:/var/lib/ghost/content",
                "-e",
                "url=https://blog.app2",
                "ghost"
            ],
            "WorkingDirectory": "/srv/docker/ghost",
            "ReverseProxy": {
                "Routes": [
                    {
                        "RouteId" : "blog.app2",
                        "ClusterId": "blog.app2",
                        "Match": {
                            "Path": "{**catch-all}",
                            "Hosts": [ "blog.app2" ]
                        }
                    }
                ],
                "Clusters": [
                    {
                        "ClusterId": "blog.app2",
                        "Destinations": {
                            "blog.app2/destination0": {
                                "Address": "http://127.0.0.1:{{port}}"
                            }
                        }
                    }
                ]
            }
        }
    ]
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
                    "blog.app2": {
                        "Protocols": "Http1AndHttp2",
                        "SslProtocols": [ "Tls11", "Tls12", "Tls13"],
                        "Certificate": {
                            "Path": "/etc/letsencrypt/live/blog.app2/cert.pem",
                            "KeyPath": "/etc/letsencrypt/live/blog.app2/privkey.pem"
                        }
                    }
                }
            }
        }
    }
}
```
