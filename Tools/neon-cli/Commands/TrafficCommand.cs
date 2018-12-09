//-----------------------------------------------------------------------------
// FILE:	    TrafficCommand.cs
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

using Neon.Common;
using Neon.Cryptography;
using Neon.Hive;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>traffic</b> command.
    /// </summary>
    public class TrafficCommand : CommandBase
    {
        private const string proxyManagerPrefix = "neon/service/neon-proxy-manager";
        private const string vaultCertPrefix    = "neon-secret/cert";

        private const string usage = @"
Manages the hive's public and private proxies.

USAGE:

    neon traffic help
    neon traffic NAME deploy
    neon traffic NAME get [--yaml] RULE
    neon traffic NAME haproxy
    neon traffic NAME haproxy-bridge
    neon traffic NAME inspect
    neon traffic NAME list|ls [--all] [--sys] 
    neon traffic NAME purge URI-PATTERN | ALL
    neon traffic NAME remove|rm RULE
    neon traffic NAME set FILE
    neon traffic NAME set -
    neon traffic NAME settings FILE
    neon traffic NAME settings -
    neon traffic NAME status
    neon traffic NAME varnish

ARGUMENTS:

    NAME        - Load balancer name: [public] or [private].
    RULE        - Rule name.
    FILE        - Path to a JSON or YAML file.
    URI-PATTERN - Uri with optional ""*"" and ""**"" wildcards.
    -           - Indicates that JSON/YAML is read from standard input.

COMMANDS:

    help            - Prints traffic manager rule details.
    get             - Output a specific rule as JSON by default.
                      Use [--yaml] to return as YAML.
    haproxy         - Outputs the traffic manager's HAProxy configuration.
    haproxy-bridge  - Outputs the pet bridge's HAProxy configuration.
    inspect         - Displays JSON details for all traffic manager
                      rules and settings.
    list|ls         - Lists the rule names.
    purge           - Purges cached items by URI glob pattern or ALL items
    remove|rm       - Removes a named rule.
    set             - Adds or updates a rule from a file or by
                      reading standard input.  JSON or YAML
                      input is supported.
    settings        - Updates the global traffic manager settings from
                      a JSON file or by reading standard input.
    status          - Displays the current status for a traffic manager.
    update          - Signals [neon-proxy-manager] to immediately deploy
                      any pending changes.

OPTIONS:

    --all           - List all traffic manager rules 
                      (system rules are excluded by default)
    --sys           - List only system rules
";

        private const string ruleHelp =
@"
neonHIVE proxies support two types of traffic manager rules: HTTP/S and 
TCP.  Each rule defines one or more frontend and backends.

HTTP/S frontends handle requests for a hostname for one or more hostname
and port combinations.  HTTPS is enabled by specifying the name of a
certificate loaded into the hive.  The port defaults to 80 for HTTP
and 443 for HTTPS.   The [https_redirect] option indicates that clients
making HTTP requests should be redirected with the HTTPS scheme.  HTTP/S
rules for the PUBLIC traffic manager are exposed on the hosting environment's
Internet facing  load balancer by default on the standard ports 80/443. 
It is possible  to change these public ports or disable exposure of
individual rules.

TCP frontends simply specify one of the TCP ports assigned to the load
balancer (note that the first two ports are reserved for HTTP and HTTPS). 
TCP rules for the PUBLIC proxy may also be exposed on the hosting 
environment's Internet facing load balancer by setting the public 
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
port 1000 on the hive's Internet-facing load balancer to the internal
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

See the documentation for more traffic manager rule and setting details.
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "traffic" }; }
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

            Program.ConnectHive();

            // Process the command arguments.

            var trafficManager = (TrafficManager)null;
            var yaml           = commandLine.HasOption("--yaml");
            var directorName   = commandLine.Arguments.FirstOrDefault();
            var isPublic       = false;

            switch (directorName)
            {
                case "help":

                    // $hack: This isn't really a traffic manager name.

                    Console.WriteLine(ruleHelp);
                    Program.Exit(0);
                    break;

                case "public":

                    trafficManager = HiveHelper.Hive.PublicTraffic;
                    isPublic       = true;
                    break;

                case "private":

                    trafficManager = HiveHelper.Hive.PrivateTraffic;
                    isPublic       = false;
                    break;

                default:

                    Console.Error.WriteLine($"*** ERROR: Load balancer name must be one of [public] or [private] ([{directorName}] is not valid).");
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

                    if (!HiveDefinition.IsValidName(ruleName))
                    {
                        Console.Error.WriteLine($"*** ERROR: [{ruleName}] is not a valid rule name.");
                        Program.Exit(1);
                    }

                    // Fetch a specific traffic manager rule and output it.

                    var rule = trafficManager.GetRule(ruleName);

                    if (rule == null)
                    {
                        Console.Error.WriteLine($"*** ERROR: Load balancer [{directorName}] rule [{ruleName}] does not exist.");
                        Program.Exit(1);
                    }

                    Console.WriteLine(yaml ? rule.ToYaml() : rule.ToJson());
                    break;

                case "haproxy":
                case "haproxy-bridge":
                case "varnish":

                    // We're going to download the traffic manager's ZIP archive containing the
                    // [haproxy.cfg] or [varnish.vcl] file, extract and write it to the console.

                    using (var consul = HiveHelper.OpenConsul())
                    {
                        var proxy        = command.Equals("haproxy-bridge", StringComparison.InvariantCultureIgnoreCase) ? directorName + "-bridge" : directorName;
                        var confKey      = $"neon/service/neon-proxy-manager/proxies/{proxy}/proxy-conf";
                        var confZipBytes = consul.KV.GetBytesOrDefault(confKey).Result;

                        if (confZipBytes == null)
                        {
                            Console.Error.WriteLine($"*** ERROR: Proxy ZIP configuration was not found in Consul at [{confKey}].");
                            Program.Exit(1);
                        }

                        using (var msZipData = new MemoryStream(confZipBytes))
                        {
                            using (var zip = new ZipFile(msZipData))
                            {
                                var file  = command.Equals("varnish", StringComparison.InvariantCultureIgnoreCase) ? "varnish.vcl" : "haproxy.cfg";
                                var entry = zip.GetEntry(file);

                                if (entry == null || !entry.IsFile)
                                {
                                    Console.Error.WriteLine($"*** ERROR: Proxy ZIP configuration in Consul at [{confKey}] appears to be corrupt.  Cannot locate the [{file}] entry.");
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

                    Console.WriteLine(NeonHelper.JsonSerialize(trafficManager.GetDefinition(), Formatting.Indented));
                    break;

                case "list":
                case "ls":

                    var showAll = commandLine.HasOption("--all");
                    var showSys = commandLine.HasOption("--sys");
                    var rules   = trafficManager.ListRules(
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
                    Console.WriteLine($"[{rules.Count()}] {trafficManager.Name} rules");
                    Console.WriteLine();

                    foreach (var item in rules)
                    {
                        Console.WriteLine(item.Name);
                    }

                    Console.WriteLine();
                    break;

                case "purge":

                    var purgeUri = commandLine.Arguments.FirstOrDefault();

                    if (string.IsNullOrEmpty(purgeUri))
                    {
                        Console.Error.WriteLine("*** ERROR: [URI-PATTERN] or [ALL] argument expected.");
                    }

                    if (purgeUri.Equals("all", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (!commandLine.HasOption("--force") && !Program.PromptYesNo($"*** Are you sure you want to purge all cached items for [{directorName.ToUpperInvariant()}]?"))
                        {
                            return;
                        }

                        trafficManager.PurgeAll();
                    }
                    else
                    {
                        trafficManager.Purge(new string[] { purgeUri });
                    }

                    Console.WriteLine();
                    Console.WriteLine("Purge request submitted.");
                    Console.WriteLine();
                    break;

                case "update":

                    trafficManager.Update();
                    break;

                case "remove":
                case "rm":

                    ruleName = commandLine.Arguments.FirstOrDefault();

                    if (string.IsNullOrEmpty(ruleName))
                    {
                        Console.Error.WriteLine("*** ERROR: [RULE] argument expected.");
                        Program.Exit(1);
                    }

                    if (!HiveDefinition.IsValidName(ruleName))
                    {
                        Console.Error.WriteLine($"*** ERROR: [{ruleName}] is not a valid rule name.");
                        Program.Exit(1);
                    }

                    if (trafficManager.RemoveRule(ruleName))
                    {
                        Console.Error.WriteLine($"Deleted load balancer [{directorName}] rule [{ruleName}].");
                    }
                    else
                    {
                        Console.Error.WriteLine($"*** ERROR: Load balancer [{directorName}] rule [{ruleName}] does not exist.");
                        Program.Exit(1);
                    }
                    break;

                case "set":

                    // $todo(jeff.lill):
                    //
                    // It would be really nice to download the existing rules and verify that
                    // adding the new rule won't cause conflicts.  Currently errors will be
                    // detected only by the [neon-proxy-manager] which will log them and cease
                    // updating the hive until the errors are corrected.
                    //
                    // An alternative would be to have some kind of service available in the
                    // hive to do this for us or perhaps having [neon-proxy-manager] generate
                    // a summary of all of the certificates (names, covered hostnames, and 
                    // expiration dates) and save this to Consul so it would be easy to
                    // download.  Perhaps do the same for the rules?

                    if (commandLine.Arguments.Length != 2)
                    {
                        Console.Error.WriteLine("*** ERROR: FILE or [-] argument expected.");
                        Program.Exit(1);
                    }

                    // Load the rule.  Note that we support reading rules as JSON or
                    // YAML, automatcially detecting the format.  We always persist
                    // rules as JSON though.

                    var ruleFile = commandLine.Arguments[1];

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

                    var trafficManagerRule = TrafficRule.Parse(ruleText, strict: true);

                    ruleName = trafficManagerRule.Name;

                    if (!HiveDefinition.IsValidName(ruleName))
                    {
                        Console.Error.WriteLine($"*** ERROR: [{ruleName}] is not a valid rule name.");
                        Program.Exit(1);
                    }

                    // Validate a clone of the rule with any implicit frontends.

                    var clonedRule = NeonHelper.JsonClone(trafficManagerRule);
                    var context    = new TrafficValidationContext(directorName, null)
                    {
                        ValidateCertificates = false    // Disable this because we didn't download the certs (see note above)
                    };

                    clonedRule.Validate(context);
                    clonedRule.Normalize(isPublic);

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

                    if (trafficManager.SetRule(trafficManagerRule))
                    {
                        Console.WriteLine($"Load balancer [{directorName}] rule [{ruleName}] has been updated.");
                    }
                    else
                    {
                        Console.WriteLine($"Load balancer [{directorName}] rule [{ruleName}] has been added.");
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

                    var trafficManagerSettings = TrafficSettings.Parse(settingsText, strict: true);

                    trafficManager.UpdateSettings(trafficManagerSettings);
                    Console.WriteLine($"Traffic manager [{directorName}] settings have been updated.");
                    break;

                case "status":

                    using (var consul = HiveHelper.OpenConsul())
                    {
                        var statusJson  = consul.KV.GetStringOrDefault($"neon/service/neon-proxy-manager/status/{directorName}").Result;

                        if (statusJson == null)
                        {
                            Console.Error.WriteLine($"*** ERROR: Status for traffic manager [{directorName}] is not currently available.");
                            Program.Exit(1);
                        }

                        var trafficManagerStatus = NeonHelper.JsonDeserialize<TrafficStatus>(statusJson);

                        Console.WriteLine();
                        Console.WriteLine($"Snapshot Time: {trafficManagerStatus.TimestampUtc} (UTC)");
                        Console.WriteLine();

                        using (var reader = new StringReader(trafficManagerStatus.Status))
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

            return new DockerShimInfo(shimability: DockerShimability.Optional, ensureConnection: true);
        }
    }
}
