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

using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;
using Neon.Net;

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
    neon dns ls|list                        - Lists the DNS targets
    neon dns rm|remove HOST                 - Removes a DNS target
    neon dns set [--check] HOST ADDRESSES   - Sets a DNS target
    neon dns set PATH                       - Sets a DNS target from a file
    neon dns set -                          - Sets a DNS target from STDIN

ARGUMENTS:

    HOST        - FQDN of the DNS entry being added (e.g. server.domain.com)
    ADDRESSES   - Specifies one or more target IP addresses, FQDNs or
                  host group names via [group=GROUPNAME]
    PATH        - Path to a JSON/YAML file describing the new entry
    -           - Indicates that the JSON/YAML file is specified by STDIN

OPTIONS:

    --check     - Indicates that indvidual endpoint health should be
                  verified by sending ICMP pings.
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
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "dns" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.Arguments.Length != 1)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            var clusterLogin = Program.ConnectCluster();
            var cluster      = new ClusterProxy(clusterLogin);
            var command      = commandLine.Arguments.ElementAt(0);

            switch (command)
            {
                case "help":

                    Console.WriteLine(help);
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
            var ensureConnection = shim.CommandLine.Arguments.ElementAt(1) != "help";

            return new DockerShimInfo(isShimmed: false, ensureConnection: ensureConnection);
        }
    }
}
