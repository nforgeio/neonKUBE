//-----------------------------------------------------------------------------
// FILE:	    ScpCommand.cs
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
    /// Implements the <b>scp</b> command.
    /// </summary>
    public class ScpCommand : CommandBase
    {
        private const string usage = @"
Opens a WinSCP connection to the named node in the current hive
or the first manager node if no node is specified.

USAGE:

    neon scp [--console] [NODE]

ARGUMENTS:

    NODE        - Optionally names the target hive node.
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

            var hiveLogin = Program.ConnectHive();

            NodeDefinition node;

            if (commandLine.Arguments.Length == 0)
            {
                node = HiveHelper.Hive.GetReachableManager().Metadata;
            }
            else
            {
                var name = commandLine.Arguments[0];

                node = hiveLogin.Definition.Nodes.SingleOrDefault(n => n.Name == name);

                if (node == null)
                {
                    Console.Error.WriteLine($"*** ERROR: The node [{name}] does not exist.");
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

            endPos = hiveLogin.SshHiveHostKeyFingerprint.IndexOf(' ');

            if (!int.TryParse(hiveLogin.SshHiveHostKeyFingerprint.Substring(0, endPos), out bitCount) || bitCount <= 0)
            {
                Console.Error.WriteLine($"*** ERROR: Cannot parse host's SSH key fingerprint [{hiveLogin.SshHiveHostKeyFingerprint}].");
                Program.Exit(1);
            }

            startPos = hiveLogin.SshHiveHostKeyFingerprint.IndexOf(md5Pattern);

            if (startPos == -1)
            {
                Console.Error.WriteLine($"*** ERROR: Cannot parse host's SSH key fingerprint [{hiveLogin.SshHiveHostKeyFingerprint}].");
                Program.Exit(1);
            }

            startPos += md5Pattern.Length;

            endPos = hiveLogin.SshHiveHostKeyFingerprint.IndexOf(' ', startPos);

            if (endPos == -1)
            {
                md5 = hiveLogin.SshHiveHostKeyFingerprint.Substring(startPos).Trim();
            }
            else
            {
                md5 = hiveLogin.SshHiveHostKeyFingerprint.Substring(startPos, endPos - startPos).Trim();
            }

            fingerprint = $"ssh-rsa {bitCount} {md5}";

            // Launch WinSCP.

            if (!File.Exists(Program.WinScpPath))
            {
                Console.Error.WriteLine($"*** ERROR: WinSCP application is not installed at [{Program.WinScpPath}].");
                Program.Exit(1);
            }

            switch (hiveLogin.Definition.HiveNode.SshAuth)
            {
                case AuthMethods.Tls:

                    // We're going write the private key to the hive temp folder.  For Windows
                    // workstations, this is probably encrypted and hopefully Linux/OSX is configured
                    // to encrypt user home directories.  We want to try to avoid persisting unencrypted
                    // hive credentials.

                    // $todo(jeff.lill):
                    //
                    // On Linux/OSX, investigate using the [/dev/shm] tmpfs volume.

                    if (string.IsNullOrEmpty(hiveLogin.SshClientKey.PrivatePPK))
                    {
                        // The hive must have been setup from a non-Windows workstation because
                        // there's no PPK formatted key that PuTTY/WinSCP require.  We'll use
                        // WinSCP] to do the conversion.

                        hiveLogin.SshClientKey.PrivatePPK = Program.ConvertPUBtoPPK(hiveLogin, hiveLogin.SshClientKey.PrivatePEM);
                        hiveLogin.Path                    = Program.GetHiveLoginPath(hiveLogin.Username, hiveLogin.Definition.Name);

                        // Update the login information.

                        hiveLogin.Save();
                    }

                    var keyPath = Path.Combine(Program.HiveTempFolder, $"{hiveLogin.HiveName}.key");

                    File.WriteAllText(keyPath, hiveLogin.SshClientKey.PrivatePPK);

                    try
                    {
                        Process.Start(Program.WinScpPath, $@"scp://{hiveLogin.SshUsername}@{node.PrivateAddress}:22 /privatekey=""{keyPath}"" /hostkey=""{fingerprint}"" /newinstance {consoleOption} /rawsettings Shell=""sudo%20-s"" compression=1");
                    }
                    finally
                    {
                        // $todo(jeff.lill):
                        //
                        // We really need to delete the key, leaving this hanging around
                        // is a security risk.  Unfortunately, the code below doesn't work
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

                    Process.Start(Program.WinScpPath, $@"scp://{hiveLogin.SshUsername}:{hiveLogin.SshPassword}@{node.PrivateAddress}:22 /hostkey=""{fingerprint}"" /newinstance {consoleOption} /rawsettings Shell=""sudo%20-s"" compression=1");
                    break;

                default:

                    throw new NotSupportedException($"Unsupported SSH authentication method [{hiveLogin.Definition.HiveNode.SshAuth}].");
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            // This command cannot be executed within the [neon-cli] container.

            return new DockerShimInfo(shimability: DockerShimability.None, ensureConnection: true);
        }
    }
}
