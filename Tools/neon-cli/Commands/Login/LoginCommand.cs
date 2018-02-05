//-----------------------------------------------------------------------------
// FILE:	    LoginCommand.cs
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
    /// Implements the <b>login</b> command.
    /// </summary>
    public class LoginCommand : CommandBase
    {
        private const string usage = @"
Manages cluster logins for the current user on the local computer.

USAGE:

    neon login          [--no-vpn] USER@CLUSTER
    neon login export   USER@CLUSTER
    neon login import   PATH
    neon login list
    neon login ls
    neon login remove   USER@CLUSTER
    neon login rm       USER@CLUSTER
    neon login status

OPTIONS:

    --no-vpn        - Don't connect using the cluster VPN
                      (for on-premise clusters only)
                   
ARGUMENTS:

    PATH            - Path to a cluster login file.
    USER@CLUSTER    - Specifies a cluster login username and cluster.
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "login" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--no-vpn" }; }
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

            if (commandLine.HasHelpOption || commandLine.Arguments.Length == 0)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            Console.WriteLine();

            var clusterLogin = Program.ClusterLogin;

            // Logout from the current cluster (if any) and if the current cluster
            // is different from the new one.

            var login = NeonClusterHelper.SplitLogin(commandLine.Arguments[0]);

            if (!login.IsOK)
            {
                Console.WriteLine($"*** ERROR: Invalid username/cluster [{commandLine.Arguments[0]}].  Expected something like: USER@CLUSTER");
                Program.Exit(1);
            }

            var usernme     = login.Username;
            var clusterName = login.ClusterName;

            if (clusterLogin != null && 
                (!string.Equals(clusterLogin.ClusterName, clusterName, StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(clusterLogin.Username, usernme, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"Logging out of [{Program.ClusterLogin.Username}@{Program.ClusterLogin.ClusterName}].");
                CurrentClusterLogin.Delete();
            }

            // We're passing NULL to close all cluster VPN connections to ensure that 
            // we're only connected to one at a time.  It's very possible for a operator
            // to have to manage multiple disconnnected clusters that share the same
            // IP address space.

            NeonClusterHelper.VpnClose(null);

            // Fetch the new cluster login.

            var clusterLoginPath = Program.GetClusterLoginPath(usernme, clusterName);

            if (!File.Exists(clusterLoginPath))
            {
                Console.Error.WriteLine($"*** ERROR: Cannot find login [{usernme}@{clusterName}].");
                Program.Exit(1);
            }

            clusterLogin = NeonHelper.JsonDeserialize<ClusterLogin>(File.ReadAllText(clusterLoginPath));

            // Determine whether we're going to use the VPN.

            var useVpn = false;

            if (clusterLogin.Definition.Hosting.IsOnPremiseProvider)
            {
                if (clusterLogin.Definition.Vpn.Enabled)
                {
                    if (!commandLine.HasOption("--no-vpn"))
                    {
                        if (!clusterLogin.Definition.Vpn.Enabled)
                        {
                            Console.WriteLine($"*** ERROR: Cluster [{clusterLogin.ClusterName}] was not provisioned with a VPN.");
                            Program.Exit(1);
                        }

                        useVpn = true;
                    }
                    else
                    {
                        useVpn = false;
                        Console.WriteLine("Using the local network (not the VPN)");
                    }
                }
                else
                {
                    useVpn = false;
                }
            }
            else
            {
                useVpn = true; // Always TRUE for cloud environments.
            }

            // Connect the VPN if enabled.

            if (useVpn)
            {
                NeonClusterHelper.VpnOpen(clusterLogin,
                    onStatus: message => Console.WriteLine($"{message}"),
                    onError: message => Console.Error.WriteLine($"*** ERROR {message}"));
            }

            // Verify the credentials by logging into a manager node.

            Console.WriteLine("Authenticating...");

            clusterProxy = new ClusterProxy(clusterLogin,
                (nodeName, publicAddress, privateAddress) =>
                {
                    return new SshProxy<NodeDefinition>(nodeName, publicAddress, privateAddress, clusterLogin.GetSshCredentials(), TextWriter.Null);
                });

            var viaVpn = useVpn ? $" (via VPN)" : string.Empty;

            try
            {
                clusterProxy.FirstManager.Connect();

                var currentLogin =
                    new CurrentClusterLogin()
                    {
                        Login  = clusterLogin.LoginName,
                        ViaVpn = useVpn
                    };

                currentLogin.Save();

                Console.WriteLine($"You are logged into [{clusterLogin.LoginName}]{viaVpn}.");
                Console.WriteLine("");
            }
            catch (Exception e)
            {
                Console.WriteLine($"*** ERROR: Cluster login failed{viaVpn}: {NeonHelper.ExceptionError(e)}");
                Console.WriteLine("");
            }
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: false, ensureConnection: false);
        }
    }
}
