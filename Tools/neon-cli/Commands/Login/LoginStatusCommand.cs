//-----------------------------------------------------------------------------
// FILE:	    LoginStatusCommand.cs
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

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>login status</b> command.
    /// </summary>
    public class LoginStatusCommand : CommandBase
    {
        private const string usage = @"
Displays status about the current cluster login.

USAGE:

        neon login status
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "login", "status" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            ClusterProxy    clusterProxy;

            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            Console.WriteLine();

            var clusterLogin = Program.ClusterLogin;

            // Print the current login status if no cluster name was passed.

            if (clusterLogin == null)
            {
                Console.Error.WriteLine("*** You are not logged in.");
                Program.Exit(1);
            }

            // Parse and validate the cluster definition.

            clusterProxy = new ClusterProxy(clusterLogin,
                (nodeName, publicAddress, privateAddress) =>
                {
                    return new NodeProxy<NodeDefinition>(nodeName, publicAddress, privateAddress, clusterLogin.GetSshCredentials(), TextWriter.Null);
                });

            // Verify the credentials by logging into a manager node.

            var verifyCredentials = true;

            Console.WriteLine($"Checking login [{clusterLogin.LoginName}]...");

            if (clusterLogin.ViaVpn)
            {
                var vpnClient = NeonClusterHelper.VpnGetClient(clusterLogin.ClusterName);

                if (vpnClient == null)
                {
                    Console.Error.WriteLine("*** ERROR: VPN is not running.");
                }
                else
                {
                    switch (vpnClient.State)
                    {
                        case NeonClusterHelper.VpnState.Connecting:

                            Console.WriteLine("VPN is connecting");
                            break;

                        case NeonClusterHelper.VpnState.Healthy:

                            Console.WriteLine("VPN connection is healthy");
                            break;

                        case NeonClusterHelper.VpnState.Unhealthy:

                            Console.Error.WriteLine("*** ERROR: VPN connection is not healthy");
                            verifyCredentials = false;
                            break;
                    }
                }
            }

            if (verifyCredentials)
            {
                Console.WriteLine("Authenticating...");

                try
                {
                    clusterProxy.FirstManager.Connect();
                    Console.WriteLine("Authenticated");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"*** ERROR: Cluster authentication failed: {NeonHelper.ExceptionError(e)}");
                }
            }

            Console.WriteLine();
            return;
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: false, ensureConnection: false);
        }
    }
}
