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

using Neon.Common;
using Neon.Hive;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>login</b> command.
    /// </summary>
    public class LoginCommand : CommandBase
    {
        private const string usage = @"
Manages hive logins for the current user on the local computer.

USAGE:

    neon login          [--no-vpn] [--show-vpn] USER@HIVE
    neon clean
    neon login export   USER@HIVE
    neon login import   PATH
    neon login list
    neon login ls
    neon login remove   USER@HIVE
    neon login rm       USER@HIVE
    neon login status

OPTIONS:

    --no-vpn        - Don't connect using the hive VPN
                      (for on-premise hives only)

    --show-vpn      - Displays the OpenVPN connection window
                      (for debugging purposes)
                   
ARGUMENTS:

    PATH            - Path to a hive login file.
    USER@HIVE       - Specifies a hive login username and hive.
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "login" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--no-vpn", "--show-vpn" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            HiveProxy    hiveProxy;

            if (commandLine.HasHelpOption || commandLine.Arguments.Length == 0)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            Console.Error.WriteLine();

            var hiveLogin = Program.HiveLogin;
            var login     = HiveHelper.SplitLogin(commandLine.Arguments[0]);

            if (!login.IsOK)
            {
                Console.Error.WriteLine($"*** ERROR: Invalid username/hive [{commandLine.Arguments[0]}].  Expected something like: USER@HIVE");
                Program.Exit(1);
            }

            // Check whether we're already logged into the hive.

            var username = login.Username;
            var hiveName = login.HiveName;

            if (hiveLogin != null && 
                string.Equals(hiveLogin.HiveName, hiveName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(hiveLogin.Username, username, StringComparison.OrdinalIgnoreCase))
            {
                // Ensure that the client is compatible with the hive.

                try
                {
                    HiveHelper.ValidateClientVersion(hiveLogin, Program.Version);
                }
                catch (VersionException e)
                {
                    HiveHelper.VpnClose(null);
                    CurrentHiveLogin.Delete();

                    Console.Error.WriteLine($"*** ERROR: {e.Message}");
                    Program.Exit(0);
                }

                // Ensure that the hive's certificates, hostnames,... are properly initialized.

                HiveHelper.OpenHive(hiveLogin);

                Console.Error.WriteLine($"*** You are already logged into [{Program.HiveLogin.Username}@{Program.HiveLogin.HiveName}].");
                Program.Exit(0);
            }

            // Logout of the current hive.

            if (hiveLogin != null)
            {
                Console.Error.WriteLine($"Logging out of [{Program.HiveLogin.Username}@{Program.HiveLogin.HiveName}].");
                CurrentHiveLogin.Delete();
            }

            // We're passing NULL to close all hive VPN connections to ensure that 
            // we're only connected to one at a time.  It's very possible for a operator
            // to have to manage multiple disconnnected hives that share the same
            // IP address space.

            HiveHelper.VpnClose(null);

            // Fetch the new hive login.

            var hiveLoginPath = Program.GetHiveLoginPath(username, hiveName);

            if (!File.Exists(hiveLoginPath))
            {
                Console.Error.WriteLine($"*** ERROR: Cannot find login [{username}@{hiveName}].");
                Program.Exit(1);
            }

            hiveLogin = NeonHelper.JsonDeserialize<HiveLogin>(File.ReadAllText(hiveLoginPath));

            // Determine whether we're going to use the VPN.

            var useVpn  = false;
            var showVpn = commandLine.HasOption("--show-vpn");

            if (hiveLogin.Definition.Hosting.IsOnPremiseProvider)
            {
                if (hiveLogin.Definition.Vpn.Enabled)
                {
                    if (!commandLine.HasOption("--no-vpn"))
                    {
                        if (!hiveLogin.Definition.Vpn.Enabled)
                        {
                            Console.Error.WriteLine($"*** ERROR: Hive [{hiveLogin.HiveName}] was not provisioned with a VPN.");
                            Program.Exit(1);
                        }

                        useVpn = true;
                    }
                    else
                    {
                        useVpn = false;
                        Console.Error.WriteLine("Using the local network (not the VPN)");
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
                HiveHelper.VpnOpen(hiveLogin,
                    onStatus: message => Console.Error.WriteLine($"{message}"),
                    onError: message => Console.Error.WriteLine($"*** ERROR {message}"),
                    show: showVpn);
            }

            // Verify the credentials by logging into a manager node.

            Console.Error.WriteLine("Authenticating...");

            hiveProxy = new HiveProxy(hiveLogin,
                (nodeName, publicAddress, privateAddress, append) =>
                {
                    return new SshProxy<NodeDefinition>(nodeName, publicAddress, privateAddress, hiveLogin.GetSshCredentials(), TextWriter.Null);
                });

            var viaVpn = useVpn ? $" (via VPN)" : string.Empty;

            try
            {
                hiveProxy.GetReachableManager().Connect();

                var currentLogin =
                    new CurrentHiveLogin()
                    {
                        Login  = hiveLogin.LoginName,
                        ViaVpn = useVpn
                    };

                currentLogin.Save();

                // Call GetLogin() with the client version so that the current hive
                // definition will be downloaded and so we'll also verify that the 
                // current client is capable of managing the hive.

                try
                {
                    HiveHelper.GetLogin(clientVersion: Program.Version);
                }
                catch (VersionException e)
                {
                    HiveHelper.VpnClose(null);
                    CurrentHiveLogin.Delete();

                    Console.Error.WriteLine($"*** ERROR: {e.Message}");
                    Program.Exit(1);
                }

                // Ensure that the hive's certificates, hostnames,... are properly initialized.

                HiveHelper.OpenHive(hiveLogin);

                Console.Error.WriteLine($"Logged into [{hiveLogin.LoginName}]{viaVpn}.");
                Console.Error.WriteLine("");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"*** ERROR: Hive login failed{viaVpn}: {NeonHelper.ExceptionError(e)}");
                Console.Error.WriteLine("");

                // Delete the current login because it failed.

                CurrentHiveLogin.Delete();

                Program.Exit(1);
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None, ensureConnection: false);
        }
    }
}
