//-----------------------------------------------------------------------------
// FILE:	    ClusterGetCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster property</b> command.
    /// </summary>
    public class ClusterGetCommand : CommandBase
    {
        private const string usage = @"
Writes a specified value from the currently logged in cluster to the
standard output.  Global cluster values as well as node specific ones
can be obtained.

USAGE:

    neon cluster get VALUE
    neon cluster get NODE.VALUE

ARGUMENTS:

    VALUE       - identifies the desired value
    NODE        - optionally names a specific node.

CLUSTER VALUE IDENTIFIERS:

    username                - root account username
    password                - root account password
    sshkey-client-pem       - client SSH private key (PEM format)
    sshkey-client-ppk       - client SSH private key (PPK format)
    sshkey-fingerprint      - SSH host key fingerprint

NODE VALUE IDENTIFIERS:

    ip                      - internal cluster IP address
    role                    - role: manager or worker
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "cluster", "get" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            var clusterLogin = Program.ConnectCluster();
            var cluster      = new ClusterProxy(clusterLogin, Program.CreateNodeProxy<NodeDefinition>);

            if (commandLine.Arguments.Length != 1)
            {
                Console.Error.WriteLine("*** ERROR: VALUE-EXPR expected.");
                Program.Exit(1);
            }

            var valueExpr = commandLine.Arguments[0];

            if (valueExpr.Contains('.'))
            {
                // Node expression.

                var fields   = valueExpr.Split(new char[] { '.' }, 2);
                var nodeName = fields[0].ToLowerInvariant();
                var value    = fields[1];

                if (string.IsNullOrEmpty(nodeName) || string.IsNullOrEmpty(value))
                {
                    Console.Error.WriteLine("*** ERROR: VALUE-EXPR is not valid.");
                    Program.Exit(1);
                }

                var node = cluster.Definition.Nodes.SingleOrDefault(n => n.Name == nodeName);

                if (node == null)
                {
                    Console.Error.WriteLine($"*** ERROR: Node [{nodeName}] is not present.");
                    Program.Exit(1);
                }

                switch (value.ToLowerInvariant())
                {
                    case "ip":

                        Console.Write(node.PrivateAddress);
                        break;

                    case "role":

                        Console.Write(node.Role);
                        break;

                    default:

                        Console.Error.WriteLine($"*** ERROR: Unknown value [{value}].");
                        Program.Exit(1);
                        break;
                }
            }
            else
            {
                // Cluster expression.

                switch (valueExpr.ToLowerInvariant())
                {
                    case "username":

                        Console.Write(clusterLogin.SshUsername);
                        break;

                    case "password":

                        Console.Write(clusterLogin.SshPassword);
                        break;

                    case "sshkey-fingerprint":

                        Console.Write(clusterLogin.SshServerKeyFingerprint);
                        break;

                    case "sshkey-client-pem":

                        Console.Write(clusterLogin.SshClientKey.PrivatePEM);
                        break;

                    case "sshkey-client-ppk":

                        Console.Write(clusterLogin.SshClientKey.PrivatePPK);
                        break;

                    default:

                        Console.Error.WriteLine($"*** ERROR: Unknown value [{valueExpr}].");
                        Program.Exit(1);
                        break;
                }
            }
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: false, ensureConnection: false);
        }
    }
}
