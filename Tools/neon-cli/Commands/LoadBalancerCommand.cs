//-----------------------------------------------------------------------------
// FILE:	    LoadBalancerCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;
using Neon.Cryptography;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>load-balancer|lb</b> command.
    /// </summary>
    public class LoadBalancerCommand : CommandBase
    {
        private const string proxyManagerPrefix = "neon/service/neon-proxy-manager";
        private const string vaultCertPrefix    = "neon-secret/cert";

        private const string usage = @"
Manages the cluster's public and private proxies.

USAGE:

    neon load-balancer|lb help
    neon load-balancer|lb NAME build
    neon load-balancer|lb NAME get [--yaml] RULE
    neon load-balancer|lb NAME inspect
    neon load-balancer|lb NAME [--all] [--sys] list|ls
    neon load-balancer|lb NAME remove|rm RULE
    neon load-balancer|lb NAME set FILE
    neon load-balancer|lb NAME set -
    neon load-balancer|lb NAME settings FILE
    neon load-balancer|lb NAME settings -
    neon load-balancer|lb NAME status

ARGUMENTS:

    NAME    - Load balancer name: [public] or [private].
    RULE    - Rule name.
    FILE    - Path to a JSON file.
    -       - Indicates that JSON/YAML is read from standard input.

COMMANDS:

    help            - Prints load balacer rule details.
    build           - Forces the [neon-proxy-manager] to rebuild
                      the load balancer configuration.
    get             - Output a specific rule as JSON by default.
                      Use [--yaml] to return as YAML.
    haproxy         - Outputs the HAProxy configuration.
    inspect         - Displays JSON details for all load balancer
                      rules and settings.
    list|ls         - Lists the rule names.
    remove|rm       - Removes a named rule.
    set             - Adds or updates a rule from a file or by
                      reading standard input.  JSON or YAML
                      input is supported.
    settings        - Updates the global load balancer settings from
                      a JSON file or by reading standard input.
    status          - Displays the current status for a load balancer.

OPTIONS:

    --all           - List all load balancer rules 
                      (system rules are excluded by default)
    --sys           - List only system rules
";

        private const string ruleHelp =
@"
neonCLUSTER proxies support two types of load balancer rules: HTTP/S and TCP.
Each rule defines one or more frontend and backends.

HTTP/S frontends handle requests for a hostname for one or more hostname
and port combinations.  HTTPS is enabled by specifying the name of a
certificate loaded into the cluster.  The port defaults to 80 for HTTP
and 443 for HTTPS.   The [https_redirect] option indicates that clients
making HTTP requests should be redirected with the HTTPS scheme.  HTTP/S
rules for the PUBLIC load balancer are exposed on the hosting environment's
Internet facing  load balancer by default on the standard ports 80/443. 
It is possible  to change these public ports or disable exposure of
individual rules.

TCP frontends simply specify one of the TCP ports assigned to the load
balancer (note that the first two ports are reserved for HTTP and HTTPS). 
TCP rules for the PUBLIC proxy may also be exposed on the hosting 
environment'sInternet facing load balancer by setting the public 
port property.

Backends specify one or more target servers by IP address or DNS name
and port number.

Rules are specified using JSON or YAML.  Here's an example HTTP/S rule that
accepts HTTP traffic for [foo.com] and [www.foo.com] and redirects it to
HTTPS and then also accepts HTTPS traffic using the [foo.com] certificate.
Traffic is routed to the Swarm [foo_service] on port 80.

    {
        ""Name"": ""my-http-rule"",
        ""Mode"": ""http"",
        ""HttpsRedirect"": true,
        ""Frontends"": [
            { ""Host"": ""foo.com"", ""CertName"": ""foo.com"" },
            { ""Host"": ""www.foo.com"", ""CertName"": ""foo.com"" }
        ],
        ""Backends"": [
            { ""Server"": ""foo_service"", ""Port"": 80 }
        ]
    }

Here's an example public TCP rule that forwards TCP connections to
port 1000 on the cluster's Internet-facing load balancer to the internal
HAProxy server listening on Docker ingress port 5305 port which then
load balances the traffic to the backend servers listening on port 1000:

    {
        ""Name"": ""my-tcp-rule"",
        ""Mode"": ""tcp"",
        ""Frontends"": [
            { ""PublicPort"": 1000, ""ProxyPort"": 5305 }
        ],
        ""Backends"": [
            { ""Server"": ""10.0.1.40"", ""Port"": 1000 },
            { ""Server"": ""10.0.1.41"", ""Port"": 1000 },
            { ""Server"": ""10.0.1.42"", ""Port"": 1000 }
        ]
    }

Here's how this rule looks as YAML:

    Name: my-tcp-rule
    Mode: tcp
    Frontends:
    - PublicPort: 1000
      ProxyPort: 5305
    Backends:
    - Server: 10.0.1.40
      Port: 1000
    - Server: 10.0.1.41
      Port: 1000
    - Server: 10.0.1.42
      Port:1000

See the documentation for more load balancer rule and setting details.
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "load-balancer" }; }
        }

        public override string[] AltWords
        {
            get { return new string[] { "lb" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--all", "--sys", "--yaml" }; }
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

            var loadBalancer = (LoadBalanceManager)null;
            var yaml         = commandLine.HasOption("--yaml");

            var loadBalancerName = commandLine.Arguments.FirstOrDefault();

            switch (loadBalancerName)
            {
                case "help":

                    // $hack: This isn't really a load balancer name.

                    Console.WriteLine(ruleHelp);
                    Program.Exit(0);
                    break;

                case "public":

                    loadBalancer = NeonClusterHelper.Cluster.PublicLoadBalancer;
                    break;

                case "private":

                    loadBalancer = NeonClusterHelper.Cluster.PrivateLoadBalancer;
                    break;

                default:

                    Console.Error.WriteLine($"*** ERROR: Load balancer name must be one of [public] or [private] ([{loadBalancerName}] is not valid).");
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

            string ruleName;

            switch (command)
            {
                case "get":

                    ruleName = commandLine.Arguments.FirstOrDefault();

                    if (string.IsNullOrEmpty(ruleName))
                    {
                        Console.Error.WriteLine("*** ERROR: [RULE] argument expected.");
                        Program.Exit(1);
                    }

                    if (!ClusterDefinition.IsValidName(ruleName))
                    {
                        Console.Error.WriteLine($"*** ERROR: [{ruleName}] is not a valid rule name.");
                        Program.Exit(1);
                    }

                    // Fetch a specific load balancer rule and output it.

                    var rule = loadBalancer.GetRule(ruleName);

                    if (rule == null)
                    {
                        Console.Error.WriteLine($"*** ERROR: Load balancer [{loadBalancerName}] rule [{ruleName}] does not exist.");
                        Program.Exit(1);
                    }

                    Console.WriteLine(yaml ? rule.ToYaml() : rule.ToJson());
                    break;

                case "haproxy":

                    // We're going to download the load balancer's ZIP archive containing 
                    // the [haproxy.cfg] file, extract and write it to the console.

                    using (var consul = NeonClusterHelper.OpenConsul())
                    {
                        var confKey      = $"neon/service/neon-proxy-manager/proxies/{loadBalancerName}/conf";
                        var confZipBytes = consul.KV.GetBytesOrDefault(confKey).Result;

                        if (confZipBytes == null)
                        {
                            Console.Error.WriteLine($"*** ERROR: HAProxy ZIP configuration was not found in Consul at [{confKey}].");
                            Program.Exit(1);
                        }

                        using (var msZipData = new MemoryStream(confZipBytes))
                        {
                            using (var zip = new ZipFile(msZipData))
                            {
                                var entry = zip.GetEntry("haproxy.cfg");

                                if (entry == null || !entry.IsFile)
                                {
                                    Console.Error.WriteLine($"*** ERROR: HAProxy ZIP configuration in Consul at [{confKey}] appears to be corrupt.  Cannot locate the [haproxy.cfg] entry.");
                                    Program.Exit(1);
                                }

                                using (var entryStream = zip.GetInputStream(entry))
                                {
                                    using (var reader = new StreamReader(entryStream))
                                    {
                                        foreach (var line in reader.Lines())
                                        {
                                            Console.WriteLine(line);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;

                case "inspect":

                    Console.WriteLine(NeonHelper.JsonSerialize(loadBalancer.GetDefinition(), Formatting.Indented));
                    break;

                case "list":
                case "ls":

                    var showAll = commandLine.HasOption("--all");
                    var showSys = commandLine.HasOption("--sys");
                    var rules   = loadBalancer.ListRules(
                        r =>
                        {
                            if (showAll)
                            {
                                return true;
                            }
                            else if (showSys)
                            {
                                return r.System;
                            }
                            else
                            {
                                return !r.System;
                            }
                        });

                    Console.WriteLine();
                    Console.WriteLine($"[{rules.Count()}] {loadBalancer.Name} rules");
                    Console.WriteLine();

                    foreach (var item in rules)
                    {
                        Console.WriteLine(item.Name);
                    }

                    Console.WriteLine();
                    break;

                case "build":

                    loadBalancer.Build();
                    break;

                case "remove":
                case "rm":

                    ruleName = commandLine.Arguments.FirstOrDefault();

                    if (string.IsNullOrEmpty(ruleName))
                    {
                        Console.Error.WriteLine("*** ERROR: [RULE] argument expected.");
                        Program.Exit(1);
                    }

                    if (!ClusterDefinition.IsValidName(ruleName))
                    {
                        Console.Error.WriteLine($"*** ERROR: [{ruleName}] is not a valid rule name.");
                        Program.Exit(1);
                    }

                    if (loadBalancer.RemoveRule(ruleName))
                    {
                        Console.Error.WriteLine($"Deleted load balancer [{loadBalancerName}] rule [{ruleName}].");
                    }
                    else
                    {
                        Console.Error.WriteLine($"*** ERROR: Load balancer [{loadBalancerName}] rule [{ruleName}] does not exist.");
                        Program.Exit(1);
                    }
                    break;

                case "set":

                    // $todo(jeff.lill):
                    //
                    // It would be really nice to download the existing rules and verify that
                    // adding the new rule won't cause conflicts.  Currently errors will be
                    // detected only by the [neon-proxy-manager] which will log them and cease
                    // updating the cluster until the errors are corrected.
                    //
                    // An alternative would be to have some kind of service available in the
                    // cluster to do this for us or perhaps having [neon-proxy-manager] generate
                    // a summary of all of the certificates (names, covered hostnames, and 
                    // expiration dates) and save this to Consul so it would be easy to
                    // download.  Perhaps do the same for the rules?

                    if (commandLine.Arguments.Length != 1)
                    {
                        Console.Error.WriteLine("*** ERROR: FILE or [-] argument expected.");
                        Program.Exit(1);
                    }

                    // Load the rule.  Note that we support reading rules as JSON or
                    // YAML, automatcially detecting the format.  We always persist
                    // rules as JSON though.

                    var ruleFile = commandLine.Arguments[0];

                    string ruleText;

                    if (ruleFile == "-")
                    {
                        using (var input = Console.OpenStandardInput())
                        {
                            using (var reader = new StreamReader(input, detectEncodingFromByteOrderMarks: true))
                            {
                                ruleText = reader.ReadToEnd();
                            }
                        }
                    }
                    else
                    {
                        ruleText = File.ReadAllText(ruleFile);
                    }

                    var loadbalancerRule = LoadBalancerRule.Parse(ruleText, strict: true);

                    ruleName = loadbalancerRule.Name;

                    if (!ClusterDefinition.IsValidName(ruleName))
                    {
                        Console.Error.WriteLine($"*** ERROR: [{ruleName}] is not a valid rule name.");
                        Program.Exit(1);
                    }

                    // Validate a clone of the rule with any implicit frontends.

                    var clonedRule = NeonHelper.JsonClone(loadbalancerRule);
                    var context    = new LoadBalancerValidationContext(loadBalancerName, null)
                    {
                        ValidateCertificates = false    // Disable this because we didn't download the certs (see note above)
                    };

                    clonedRule.Validate(context, addImplicitFrontends: true);

                    if (context.HasErrors)
                    {
                        Console.Error.WriteLine("*** ERROR: One or more rule errors:");
                        Console.Error.WriteLine();

                        foreach (var error in context.Errors)
                        {
                            Console.Error.WriteLine(error);
                        }

                        Program.Exit(1);
                    }
                    
                    if (loadBalancer.SetRule(loadbalancerRule))
                    {
                        Console.WriteLine($"Load balancer [{loadBalancerName}] rule [{ruleName}] has been updated.");
                    }
                    else
                    {
                        Console.WriteLine($"Load balancer [{loadBalancerName}] rule [{ruleName}] has been added.");
                    }
                    break;

                case "settings":

                    var settingsFile = commandLine.Arguments.FirstOrDefault();

                    if (string.IsNullOrEmpty(settingsFile))
                    {
                        Console.Error.WriteLine("*** ERROR: [-] or FILE argument expected.");
                        Program.Exit(1);
                    }

                    string settingsText;

                    if (settingsFile == "-")
                    {
                        settingsText = NeonHelper.ReadStandardInputText();
                    }
                    else
                    {
                        settingsText = File.ReadAllText(settingsFile);
                    }

                    var loadbalancerSettings = LoadBalancerSettings.Parse(settingsText, strict: true);

                    loadBalancer.UpdateSettings(loadbalancerSettings);
                    Console.WriteLine($"Load balancer [{loadBalancerName}] settings have been updated.");
                    break;

                case "status":

                    using (var consul = NeonClusterHelper.OpenConsul())
                    {
                        var statusJson  = consul.KV.GetStringOrDefault($"neon/service/neon-proxy-manager/status/{loadBalancerName}").Result;

                        if (statusJson == null)
                        {
                            Console.Error.WriteLine($"*** ERROR: Status for load balancer [{loadBalancerName}] is not currently available.");
                            Program.Exit(1);
                        }

                        var loadBalancerStatus = NeonHelper.JsonDeserialize<LoadBalancerStatus>(statusJson);

                        Console.WriteLine();
                        Console.WriteLine($"Snapshot Time: {loadBalancerStatus.TimestampUtc} (UTC)");
                        Console.WriteLine();

                        using (var reader = new StringReader(loadBalancerStatus.Status))
                        {
                            foreach (var line in reader.Lines())
                            {
                                Console.WriteLine(line);
                            }
                        }
                    }
                    break;

                default:

                    Console.Error.WriteLine($"*** ERROR: Unknown command: [{command}]");
                    Program.Exit(1);
                    break;
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
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
                    case "set":
                    case "settings":

                        shim.AddFile(commandLine.Arguments[3]);
                        break;
                }
            }

            return new DockerShimInfo(isShimmed: true, ensureConnection: true);
        }
    }
}
