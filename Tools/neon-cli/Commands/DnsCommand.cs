//-----------------------------------------------------------------------------
// FILE:	    DnsCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;
using Neon.Net;
using System.Diagnostics.Contracts;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>dns</b> command.
    /// </summary>
    public class DnsCommand : CommandBase
    {
        private const string usage =
@"
Manages cluster DNS records.

USAGE:

    neon dns help                           - Describes target file format
    neon dns addr|addresses [HOST]          - Lists current host addresses
    neon dns [--yaml] get HOST              - Gets DNS host settings
    neon dns ls|list                        - Lists the DNS host targets
    neon dns rm|remove HOST                 - Removes DNS host settings
    neon dns set [--check] HOST ADDRESSES   - Sets DNS host settings
    neon dns set PATH                       - Sets DNS settings from a file
    neon dns set -                          - Sets DNS settings from STDIN

ARGUMENTS:

    HOST        - FQDN of the DNS entry being added (e.g. server.domain.com)
    ADDRESSES   - Specifies one or more target IP addresses, FQDNs or
                  host group names via [group=GROUPNAME]
    PATH        - Path to a JSON/YAML file describing the new entry
    -           - Indicates that the JSON/YAML file is specified by STDIN

OPTIONS:

    --check     - Indicates that indvidual endpoint health should be
                  verified by sending ICMP pings.

    --yaml      - Output YAML instead of JSON.
";
        private const string help =
@"
neonCLUSTER can load DNS entries specified by JSON or YAML files.  Each DNS
entry specifies the hostname for the entry as well as the endpoints to be 
registered for the hostname.  Each endpoint can be an IP address, another
hostname that will be resolved into an IP address or a cluster node group.
Endpoint health may optionally be ensured.

Here's a JSON example that specifies that [api.test.com] should resolve to
address [1.2.3.4] as well as the IP addresses for [api.backend.net] IP with
health checks:

    {
        ""Hostname"": ""api.test.com"",
        ""Endpoints"": [
            {
                ""Target"": ""1.2.3.4""
            },
            {
                ""Target"": ""api.backend.net"",
                ""Check"": true
            }
        ]
    }

Here is how this will look as YAML:

    hostname: api.test.com
    endpoints:
    - target: 1.2.3.4
    - target: api.backend.net
      check: true

Targeting a neonCLUSTER host group is a powerful way to register a hostname
that maps to cluster nodes.  neonCLUSTER defines several built-in groups
like: manager, workers, pets, swarm,... and it's possible to define custom
groups during cluster setup.

The YAML example below defines [my-managers] using the [managers] group:

    hostname: my-managers
    endpoints:
    - target: group=managers
      check: true

Note that [neon-dns-mon] automatically creates DNS entries for all cluster 
host groups if they don't already exist (named like: [GROUPNAME.cluster]).
";

        private ClusterLogin    clusterLogin;
        private ClusterProxy    cluster;

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "dns" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--check", "--yaml" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.Arguments.Length == 0)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            clusterLogin = Program.ConnectCluster();
            cluster      = new ClusterProxy(clusterLogin);

            var command  = commandLine.Arguments.ElementAt(0);
            var yaml     = commandLine.HasOption("--yaml");

            if (command == "help")
            {
                Console.WriteLine(help);
                Program.Exit(0);
            }

            switch (command)
            {
                case "addr":
                case "addresses":

                    ListAddresses(commandLine);
                    break;

                case "get":

                    GetTarget(commandLine);
                    break;

                case "ls":
                case "list":

                    ListTargets(commandLine);
                    break;

                case "rm":
                case "remove":

                    RemoveTarget(commandLine);
                    break;

                case "set":

                    SetTarget(commandLine);
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
            var ensureConnection = shim.CommandLine.Arguments.ElementAtOrDefault(1) != "help";

            return new DockerShimInfo(isShimmed: false, ensureConnection: ensureConnection);
        }

        /// <summary>
        /// Implements the <b>addr|addresses</b> command.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private void ListAddresses(CommandLine commandLine)
        {
            // We're simply going to download and parse [neon/dns/answers/hosts.txt].

            var answers    = GetAnswers();
            var targetHost = commandLine.Arguments.ElementAtOrDefault(2);

            Console.WriteLine();

            if (targetHost != null)
            {
                // Print the addresses for a specific host.

                if (!answers.TryGetValue(targetHost, out var addresses))
                {
                    Console.Error.WriteLine($"*** ERROR: [host={targetHost}] does not exist.");
                    Program.Exit(1);
                }

                PrintHostAddresses(targetHost, addresses);
            }
            else
            {
                // Print the addresses for all hosts.

                var maxHostNameWidth = answers.Keys.Max(h => h.Length);

                foreach (var item in answers.OrderBy(i => i.Key))
                {
                    PrintHostAddresses(item.Key, item.Value, maxHostNameWidth);
                }
            }
        }

        /// <summary>
        /// Returns the current DNS host/answers as a dictionary.
        /// </summary>
        /// <returns>The answers dictionary.</returns>
        private Dictionary<string, List<string>> GetAnswers()
        {
            var hosts = cluster.Consul.KV.GetStringOrDefault("neon/dns/answers/hosts.txt").Result;

            if (hosts == null)
            {
                Console.Error.WriteLine($"*** ERROR: [neon/dns/answers/hosts.txt] does not exist in Consul.");
                Console.Error.WriteLine($"***        Verify that [neon-dns-mon] service is running.");
                Program.Exit(1);
            }

            var answers = new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);

            using (var reader = new StringReader(hosts))
            {
                foreach (var line in reader.Lines())
                {
                    var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (fields.Length != 2)
                    {
                        continue;
                    }

                    var address = fields[0];
                    var host = fields[1];

                    if (!answers.TryGetValue(host, out var addresses))
                    {
                        addresses = new List<string>();
                        answers.Add(host, addresses);
                    }

                    addresses.Add(address);
                }
            }

            return answers;
        }

        /// <summary>
        /// Print host addresses to the console.
        /// </summary>
        /// <param name="host">The host name.</param>
        /// <param name="addresses">The host addresses.</param>
        /// <param name="maxHostNameWidth">Optionally specifies the maximum name width.</param>
        private void PrintHostAddresses(string host, List<string> addresses, int maxHostNameWidth = 0)
        {
            string lead;

            if (maxHostNameWidth <= 0)
            {
                lead = $"{host}";
            }
            else
            {
                var spacing = new string(' ', maxHostNameWidth - host.Length);

                lead = $"{host} {spacing}";
            }

            lead += " ";

            var indent = new string(' ', lead.Length);
            var first  = true;

            foreach (var address in addresses)
            {
                if (first)
                {
                    Console.Write(lead);
                    first = false;
                }
                else
                {
                    Console.Write(indent);
                }

                Console.WriteLine(address);
            }
        }

        /// <summary>
        /// Implements the <b>get</b> command.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private void GetTarget(CommandLine commandLine)
        {
            var host = commandLine.Arguments.ElementAtOrDefault(1);
            var yaml = commandLine.HasOption("--yaml");

            if (host == null)
            {
                Console.Error.WriteLine("*** ERROR: [HOST] argument expected.");
                Program.Exit(1);
            }

            host = host.ToLowerInvariant();

            var targetDef = cluster.Consul.KV.GetObjectOrDefault<DnsTarget>(GetTargetConsulKey(host)).Result;

            if (targetDef == null)
            {
                Console.Error.WriteLine($"*** ERROR: DNS entry for [{host}] does not exist.");
                Program.Exit(1);
            }

            if (yaml)
            {
                Console.WriteLine(NeonHelper.YamlSerialize(targetDef));
            }
            else
            {
                Console.WriteLine(NeonHelper.JsonSerialize(targetDef, Formatting.Indented));
            }
        }

        /// <summary>
        /// Implements the <b>ls|list</b> command.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private void ListTargets(CommandLine commandLine)
        {
            var targetResult = cluster.Consul.KV.ListOrDefault<DnsTarget>(NeonClusterConst.DnsConsulTargetsKey).Result;

            Console.WriteLine();

            if (targetResult == null)
            {
                Console.WriteLine("[0] DNS targets");
                return;
            }

            var targetDefs   = targetResult.ToList();
            var maxHostWidth = targetDefs.Max(t => t.Hostname.Length);
            var answers      = GetAnswers();

            foreach (var target in targetDefs)
            {
                var host       = target.Hostname.ToLowerInvariant();
                var hostPart   = $"{host} {new string(' ', maxHostWidth - host.Length)}";
                var healthyCount = 0;

                if (answers.TryGetValue(target.Hostname, out var answer))
                {
                    healthyCount = answer.Count;
                }

                Console.WriteLine($"{hostPart}    [healthy={healthyCount}]");
            }

            Console.WriteLine();
            Console.WriteLine($"[{targetDefs.Count}] DNS targets");
        }

        /// <summary>
        /// Returns the Consul key for the DNS target for a hostname.
        /// </summary>
        /// <param name="hostname">The target hostname.</param>
        /// <returns>The Consul key path.</returns>
        private string GetTargetConsulKey(string hostname)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hostname));

            return $"{NeonClusterConst.DnsConsulTargetsKey}/{hostname}";
        }

        /// <summary>
        /// Implements the <b>rm|remove</b> command.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private void RemoveTarget(CommandLine commandLine)
        {
            var targetHost = commandLine.Arguments.ElementAtOrDefault(1);

            if (targetHost == null)
            {
                Console.Error.WriteLine("*** ERROR: [HOST] argument expected.");
                Program.Exit(1);
            }

            cluster.Consul.KV.Delete(GetTargetConsulKey(targetHost)).Wait();
            Console.WriteLine($"Removed [{targetHost}] (if it existed).");
        }

        /// <summary>
        /// Implements the <b>set</b> command.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private void SetTarget(CommandLine commandLine)
        {
            DnsTarget dnsTarget;

            if (commandLine.Arguments.Length >= 3)
            {
                // Handle: neon dns set [--check] HOST ADDRESSES

                var host  = commandLine.Arguments.ElementAtOrDefault(1);
                var check = commandLine.HasOption("--check");

                dnsTarget = new DnsTarget()
                {
                    Hostname = host
                };

                foreach (var address in commandLine.Arguments.Skip(2))
                {
                    dnsTarget.Endpoints.Add(
                        new DnsEndpoint()
                        {
                            Target = address,
                            Check  = check
                        });
                }
            }
            else
            {
                // Handle: neon dns set PATH
                //     or: neon dns set -

                string path = commandLine.Arguments.ElementAtOrDefault(1);
                string data;

                if (path == null)
                {
                    Console.Error.WriteLine("*** ERROR: [PATH] or [-] argument expected.");
                    Program.Exit(1);
                }

                if (path == "-")
                {
                    data = NeonHelper.ReadStandardInputText();
                }
                else
                {
                    data = File.ReadAllText(path);
                }

                dnsTarget = NeonHelper.JsonOrYamlDeserialize<DnsTarget>(data, strict: true);
            }

            var errors = dnsTarget.Validate(cluster.Definition, cluster.Definition.GetNodeGroups(excludeAllGroup: true));

            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    Console.Error.WriteLine($"*** ERROR: {error}");
                }

                Program.Exit(1);
            }

            var key = GetTargetConsulKey(dnsTarget.Hostname);

            cluster.Consul.KV.PutObject(key, dnsTarget, Formatting.Indented).Wait();

            Console.WriteLine();
            Console.WriteLine($"Saved [{dnsTarget.Hostname}] DNS host mappings.");
        }

        /// <summary>
        /// Verifies that a hostname is valid.
        /// </summary>
        /// <param name="hostname">The hostname.</param>
        private void ValidateHost(string hostname)
        {
            Covenant.Requires<ArgumentNullException>(hostname == null);

            if (!ClusterDefinition.DnsHostRegex.IsMatch(hostname))
            {
                Console.Error.WriteLine($"*** ERROR: [{hostname}] is not a valid DNS host name.");
                Program.Exit(1);
            }
        }
    }
}
