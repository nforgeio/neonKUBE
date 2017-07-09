//-----------------------------------------------------------------------------
// FILE:	    ProxyCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;
using Neon.Cryptography;

namespace NeonTool
{
    /// <summary>
    /// Implements the <b>proxy</b> command.
    /// </summary>
    public class ProxyCommand : CommandBase
    {
        private const string proxyManagerPrefix = "neon/service/neon-proxy-manager";
        private const string vaultCertPrefix    = "neon-secret/cert";

        private const string usage = @"
Manages the cluster's public and private proxies.

USAGE:

    neon proxy NAME get ROUTE
    neon proxy NAME inspect
    neon proxy NAME list|ls
    neon proxy NAME rebuild
    neon proxy NAME remove|rm ROUTE
    neon proxy NAME put FILE
    neon proxy NAME put -
    neon proxy NAME settings FILE
    neon proxy NAME settings -
    neon proxy NAME status

ARGUMENTS:

    NAME    - Proxy name: [public] or [private].
    ROUTE   - Route name.
    FILE    - Path to a JSON file.
    -       - Indicates that JSON is read from standard input.

COMMANDS:

    get             - Displays a specific route.

    inspect         - Displays JSON details for all proxy routes
                      and settings.

    list|ls         - Lists the route names.

    rebuild         - Forces the proxy manager to rebuild the 
                      proxy configuration.

    remove|rm       - Removes a route (if it exists).

    put             - Adds or updates a route from a JSON file
                      or by reading standard input.

    settings        - Updates the proxy global settings from a
                      JSON file or by reading standard input.

    status          - Displays the current status for a proxy.

ROUTES:

NeonCluster proxies support two types of routes: HTTP/S and TCP.
Each route defines one or more frontend and backends.

HTTP/S frontends handle requests for a hostname for one or more hostname
and port combinations.  HTTPS is enabled by specifying the name of a
certificate loaded into the cluster.  The port defaults to 80 for HTTP
and 443 for HTTPS.   The [https_redirect] option indicates that clients
making HTTP requests should be redirected with the HTTPS scheme.  HTTP/S
routes for the PUBLIC proxy are exposed on the Internet facing load balancer
by default on the standard ports 80/443.  It is possible to change
these public ports or disable exposure of individual routes.

TCP frontends simply specify one of the TCP ports assigned to the proxy
(note that the first two ports are reserved for HTTP and HTTPS).  TCP
routes for the PUBLIC proxy may also be exposed on the Internet facing
load balancer by setting the public port property.

Backends specify one or more target servers by IP address or DNS name
and port number.

Routes are specified using JSON.  Here's an example HTTP/S route that
accepts HTTP traffic for [foo.com] and [www.foo.com] and redirects it
to HTTPS and then also accepts HTTPS traffic using the [foo.com] certificate.
Traffic is routed to the [foo_service] on port 80 which could be a Docker
swarm mode service or DNS name.

    {
        ""Name"": ""my-http-route"",
        ""Mode"": ""http"",
        ""HttpsRedirect"": true,
        ""Frontends"": [
            { ""Host"": ""foo.com"" },
            { ""Host"": ""www.foo.com"" },
            { ""Host"": ""foo.com"", ""CertName"": ""foo.com"" },
            { ""Host"": ""www.foo.com"", ""CertName"": ""foo.com"" }
        ],
        ""Backends"": [
            { ""Server"": ""foo_service"", ""Port"": 80 }
        ]
    }

Here's an example public TCP route that forwards TCP connections to the
port 1000 on the cluster's Internet facing load balancer to the internal
HAProxy server listening on Docker ingress port 11102 port which then
load balances the traffic to the backend servers listening on port 1000:

    {
        ""Name"": ""my-tcp-route"",
        ""Mode"": ""tcp"",
        ""Frontends"": [
            { ""PublicPort"": 1000, ""ProxyPort"": 11102 }
        ],
        ""Backends"": [
            { ""Server"": ""10.0.1.40"", ""Port"": 1000 },
            { ""Server"": ""10.0.1.41"", ""Port"": 1000 },
            { ""Server"": ""10.0.1.42"", ""Port"": 1000 }
        ]
    }

See the documentation for more proxy route and setting details.
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "proxy" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption || commandLine.Arguments.Length == 0)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            Program.ConnectCluster();

            // Process the command arguments.

            ProxyManager proxyManager = null;

            var proxyName = commandLine.Arguments.FirstOrDefault();

            switch (proxyName)
            {
                case "public":

                    proxyManager = NeonClusterHelper.Cluster.PublicProxy;
                    break;

                case "private":

                    proxyManager = NeonClusterHelper.Cluster.PrivateProxy;
                    break;

                default:

                    Console.WriteLine($"*** ERROR: Proxy name must be one of [public] or [private] ([{proxyName}] is not valid).");
                    Program.Exit(1);
                    break;
            }

            commandLine = commandLine.Shift(1);

            var command = commandLine.Arguments.FirstOrDefault();

            if (command == null)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            commandLine = commandLine.Shift(1);

            string routeName;

            switch (command.ToLowerInvariant())
            {
                case "get":

                    routeName = commandLine.Arguments.FirstOrDefault();

                    if (string.IsNullOrEmpty(routeName))
                    {
                        Console.Error.WriteLine("*** ERROR: [ROUTE] argument expected.");
                        Program.Exit(1);
                    }

                    if (!ClusterDefinition.IsValidName(routeName))
                    {
                        Console.WriteLine($"*** ERROR: [{routeName}] is not a valid route name.");
                        Program.Exit(1);
                    }

                    // Fetch a specific proxy route and output it.

                    var route = proxyManager.GetRoute(routeName);

                    if (route == null)
                    {
                        Console.WriteLine($"*** ERROR: Proxy [{proxyName}] route [{routeName}] does not exist.");
                        Program.Exit(1);
                    }

                    Console.WriteLine(NeonHelper.JsonSerialize(route, Formatting.Indented));
                    break;

                case "inspect":

                    Console.WriteLine(NeonHelper.JsonSerialize(proxyManager.GetDefinition(), Formatting.Indented));
                    break;

                case "list":
                case "ls":

                    var nameList = proxyManager.ListRoutes().ToArray();

                    if (nameList.Length == 0)
                    {
                        Console.WriteLine("* No routes");
                    }
                    else
                    {
                        foreach (var name in proxyManager.ListRoutes())
                        {
                            Console.WriteLine(name);
                        }
                    }
                    break;

                case "rebuild":

                    proxyManager.Rebuild();
                    break;

                case "remove":
                case "rm":

                    routeName = commandLine.Arguments.FirstOrDefault();

                    if (string.IsNullOrEmpty(routeName))
                    {
                        Console.Error.WriteLine("*** ERROR: [ROUTE] argument expected.");
                        Program.Exit(1);
                    }

                    if (!ClusterDefinition.IsValidName(routeName))
                    {
                        Console.WriteLine($"*** ERROR: [{routeName}] is not a valid route name.");
                        Program.Exit(1);
                    }

                    if (proxyManager.DeleteRoute(routeName))
                    {
                        Console.WriteLine($"Deleted proxy [{proxyName}] route [{routeName}].");
                    }
                    else
                    {
                        Console.WriteLine($"*** ERROR: Proxy [{proxyName}] route [{routeName}] does not exist.");
                        Program.Exit(1);
                    }
                    break;

                case "put":

                    // $todo(jeff.lill):
                    //
                    // It would be really nice to download the existing routes and verify that
                    // adding the new route won't cause conflicts.  Currently errors will be
                    // detected only by the [neon-proxy-manager] which will log them and cease
                    // updating the cluster until the errors are corrected.

                    if (commandLine.Arguments.Length != 1)
                    {
                        Console.Error.WriteLine("*** ERROR: FILE or [-] argument expected.");
                        Program.Exit(1);
                    }

                    // Load the route.

                    var routeFile = commandLine.Arguments[0];

                    string routeJson;

                    if (routeFile == "-")
                    {
                        using (var input = Console.OpenStandardInput())
                        {
                            using (var reader = new StreamReader(input, detectEncodingFromByteOrderMarks: true))
                            {
                                routeJson = reader.ReadToEnd();
                            }
                        }
                    }
                    else
                    {
                        routeJson = File.ReadAllText(routeFile);
                    }

                    var proxyRoute = ProxyRoute.Parse(routeJson);

                    routeName = proxyRoute.Name;

                    if (!ClusterDefinition.IsValidName(routeName))
                    {
                        Console.WriteLine($"*** ERROR: [{routeName}] is not a valid route name.");
                        Program.Exit(1);
                    }

                    if (proxyManager.SetRoute(proxyRoute))
                    {
                        Console.WriteLine($"Proxy [{proxyName}] route [{routeName}] has been updated.");
                    }
                    else
                    {
                        Console.WriteLine($"Proxy [{proxyName}] route [{routeName}] has been added.");
                    }
                    break;

                case "settings":

                    var settingsFile = commandLine.Arguments.FirstOrDefault();

                    if (string.IsNullOrEmpty(settingsFile))
                    {
                        Console.Error.WriteLine("*** ERROR: [-] or FILE argument expected.");
                        Program.Exit(1);
                    }

                    string settingsJson;

                    if (settingsFile == "-")
                    {
                        using (var input = Console.OpenStandardInput())
                        {
                            using (var reader = new StreamReader(input, detectEncodingFromByteOrderMarks: true))
                            {
                                settingsJson = reader.ReadToEnd();
                            }
                        }
                    }
                    else
                    {
                        settingsJson = File.ReadAllText(settingsFile);
                    }

                    var proxySettings = NeonHelper.JsonDeserialize<ProxySettings>(settingsJson);

                    proxyManager.UpdateSettings(proxySettings);
                    Console.WriteLine($"Proxy [{proxyName}] settings have been updated.");
                    break;

                case "status":

                    using (var consul = NeonClusterHelper.OpenConsul())
                    {
                        try
                        {
                            var statusJson  = consul.KV.GetString($"neon/service/neon-proxy-manager/status/{proxyName}").Result;
                            var proxyStatus = NeonHelper.JsonDeserialize<ProxyStatus>(statusJson);

                            Console.WriteLine();
                            Console.WriteLine($"Status Time: {proxyStatus.TimestampUtc} (UTC)");
                            Console.WriteLine();

                            using (var reader = new StringReader(proxyStatus.Status))
                            {
                                foreach (var line in reader.Lines())
                                {
                                    Console.WriteLine(line);
                                }
                            }
                        }
                        catch (KeyNotFoundException)
                        {
                            Console.WriteLine($"*** ERROR: Status for proxy [{proxyName}] is not currently available.");
                            Program.Exit(1);
                        }
                    }
                    break;

                default:

                    Console.Error.WriteLine($"*** ERROR: Unknown subcommand [{command}].");
                    Program.Exit(1);
                    break;
            }
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            var commandLine = shim.CommandLine;

            if (commandLine.Arguments.LastOrDefault() == "-")
            {
                shim.AddStdin(text: true);
            }
            else if (commandLine.Arguments.Length == 4)
            {
                switch (commandLine.Arguments[2])
                {
                    case "put":
                    case "settings":

                        shim.AddFile(commandLine.Arguments[3]);
                        break;
                }
            }

            return new ShimInfo(isShimmed: true, ensureConnection: true);
        }
    }
}
