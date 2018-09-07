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

using Neon.Common;
using Neon.Hive;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>login status</b> command.
    /// </summary>
    public class LoginStatusCommand : CommandBase
    {
        private const string usage = @"
Displays status about the current hive login.

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
            HiveProxy    hive;

            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            var hiveLogin = Program.HiveLogin;

            // Print the current login status if no hive name was passed.

            if (hiveLogin == null)
            {
                Console.Error.WriteLine("*** You are not logged in.");
                Program.Exit(1);
            }

            Console.WriteLine(hiveLogin.LoginName);

            // Parse and validate the hive definition.

            hive = new HiveProxy(hiveLogin,
                (nodeName, publicAddress, privateAddress, append) =>
                {
                    return new SshProxy<NodeDefinition>(nodeName, publicAddress, privateAddress, hiveLogin.GetSshCredentials(), TextWriter.Null);
                });

            // Verify the credentials by logging into a manager node.

            var verifyCredentials = true;

            Console.Error.WriteLine();
            Console.Error.WriteLine($"Checking login [{hiveLogin.LoginName}]...");

            if (hiveLogin.ViaVpn)
            {
                var vpnClient = HiveHelper.VpnGetClient(hiveLogin.HiveName);

                if (vpnClient == null)
                {
                    Console.Error.WriteLine("*** ERROR: VPN is not running.");
                }
                else
                {
                    switch (vpnClient.State)
                    {
                        case HiveHelper.VpnState.Connecting:

                            Console.Error.WriteLine("VPN is connecting");
                            break;

                        case HiveHelper.VpnState.Healthy:

                            Console.Error.WriteLine("VPN connection is healthy");
                            break;

                        case HiveHelper.VpnState.Unhealthy:

                            Console.Error.WriteLine("*** ERROR: VPN connection is not healthy");
                            verifyCredentials = false;
                            break;
                    }
                }
            }

            if (verifyCredentials)
            {
                Console.Error.WriteLine("Authenticating...");

                try
                {
                    hive.GetReachableManager().Connect();
                    Console.Error.WriteLine("Authenticated");
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"*** ERROR: Hive authentication failed: {NeonHelper.ExceptionError(e)}");
                }
            }

            Console.WriteLine();
            return;
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None, ensureConnection: false);
        }
    }
}
