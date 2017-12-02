//-----------------------------------------------------------------------------
// FILE:	    ScpCommand.cs
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
    /// Implements the <b>scp</b> command.
    /// </summary>
    public class ScpCommand : CommandBase
    {
        private const string usage = @"
Opens a WinSCP connection to the named node in the current cluster
or the first manager node if no node is specified.

USAGE:

    neon scp [--console] [NODE]

ARGUMENTS:

    NODE        - Optionally names the target cluster node.
                  Otherwise, the first manager node will be opened.

OPTIONS:

    --console       - Opens a command line Window.
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "scp" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--console" }; }
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

            NodeDefinition node;

            if (commandLine.Arguments.Length == 0)
            {
                node = clusterLogin.Definition.SortedManagers.First();
            }
            else
            {
                var name = commandLine.Arguments[0];

                node = clusterLogin.Definition.Nodes.SingleOrDefault(n => n.Name == name);

                if (node == null)
                {
                    Console.WriteLine($"*** ERROR: The node [{name}] does not exist.");
                    Program.Exit(1);
                }
            }

            var consoleOption = commandLine.GetOption("--console") != null ? "/console" : string.Empty;

            // The host's SSH key fingerprint looks something like the example below.
            // We need to extract extract the bitcount and MD5 hash to generate a
            // WinSCP compatible host key fingerprint.
            //
            //      2048 MD5:cb:2f:f1:68:4b:aa:b3:8a:72:4d:53:f6:9f:5f:6a:fa root@manage-0 (RSA)

            const string    md5Pattern = "MD5:";
            string          fingerprint;
            int             bitCount;
            string          md5;
            int             startPos;
            int             endPos;

            endPos = clusterLogin.SshServerKeyFingerprint.IndexOf(' ');

            if (!int.TryParse(clusterLogin.SshServerKeyFingerprint.Substring(0, endPos), out bitCount) || bitCount <= 0)
            {
                Console.WriteLine($"*** ERROR: Cannot parse host's SSH key fingerprint [{clusterLogin.SshServerKeyFingerprint}].");
                Program.Exit(1);
            }

            startPos = clusterLogin.SshServerKeyFingerprint.IndexOf(md5Pattern);

            if (startPos == -1)
            {
                Console.WriteLine($"*** ERROR: Cannot parse host's SSH key fingerprint [{clusterLogin.SshServerKeyFingerprint}].");
                Program.Exit(1);
            }

            startPos += md5Pattern.Length;

            endPos = clusterLogin.SshServerKeyFingerprint.IndexOf(' ', startPos);

            if (endPos == -1)
            {
                md5 = clusterLogin.SshServerKeyFingerprint.Substring(startPos).Trim();
            }
            else
            {
                md5 = clusterLogin.SshServerKeyFingerprint.Substring(startPos, endPos - startPos).Trim();
            }

            fingerprint = $"ssh-rsa {bitCount} {md5}";

            // Launch WinSCP.

            if (!File.Exists(Program.WinScpPath))
            {
                Console.WriteLine($"*** ERROR: WinSCP application is not installed at [{Program.WinScpPath}].");
                Program.Exit(1);
            }

            switch (clusterLogin.Definition.HostAuth.SshAuth)
            {
                case AuthMethods.Tls:

                    // We're going write the private key to the cluster temp folder.  For Windows
                    // workstations, this is probably encrypted and hopefully Linux/OSX is configured
                    // to encrypt user home directories.  We want to try to avoid persisting unencrypted
                    // cluster credentials.

                    // $todo(jeff.lill):
                    //
                    // On Linux/OSX, investigate using the [/dev/shm] tmpfs volume.

                    if (string.IsNullOrEmpty(clusterLogin.SshClientKey.PrivatePPK))
                    {
                        // The cluster must have been setup from a non-Windows workstation because
                        // there's no PPK formatted key that PuTTY/WinSCP require.  We'll use
                        // WinSCP] to do the conversion.

                        clusterLogin.SshClientKey.PrivatePPK = Program.ConvertPUBtoPPK(clusterLogin, clusterLogin.SshClientKey.PrivatePEM);
                        clusterLogin.Path                    = Program.GetClusterLoginPath(clusterLogin.Username, clusterLogin.Definition.Name);

                        // Update the login information.

                        clusterLogin.Save();
                    }

                    var keyPath = Path.Combine(Program.ClusterTempFolder, $"{clusterLogin.ClusterName}.key");

                    File.WriteAllText(keyPath, clusterLogin.SshClientKey.PrivatePPK);

                    try
                    {
                        Process.Start(Program.WinScpPath, $@"scp://{clusterLogin.SshUsername}@{node.PrivateAddress}:22 /privatekey=""{keyPath}"" /hostkey=""{fingerprint}"" /newinstance {consoleOption} /rawsettings Shell=""sudo%20-s"" compression=1");
                    }
                    finally
                    {
                        // $todo(jeff.lill):
                        //
                        // We really need to delete the key, leaving this hanging around
                        // is a security risk.  Unforunately, the code below doesn't work
                        // because WinSCP doesn't seen to hold the key in RAM like it
                        // does with passwords.
                        //
                        // This results in an authentication failure whenever an operation
                        // is performed that would require SUDO access.
#if TODO
                        // Wait a bit for WinSCP to start and then delete the key.

                        while (true)
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(5));

                            try
                            {
                                File.Delete(keyPath);
                                break;
                            }
                            catch
                            {
                                // Intentionally ignoring this.
                            }
                        }
#endif
                    }
                    break;

                case AuthMethods.Password:

                    Process.Start(Program.WinScpPath, $@"scp://{clusterLogin.SshUsername}:{clusterLogin.SshPassword}@{node.PrivateAddress}:22 /hostkey=""{fingerprint}"" /newinstance {consoleOption} /rawsettings Shell=""sudo%20-s"" compression=1");
                    break;

                default:

                    throw new NotSupportedException($"Unsupported SSH authentication method [{clusterLogin.Definition.HostAuth.SshAuth}].");
            }
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            // This command cannot be executed within the [neon-cli] container.

            return new ShimInfo(isShimmed: false, ensureConnection: true);
        }
    }
}
