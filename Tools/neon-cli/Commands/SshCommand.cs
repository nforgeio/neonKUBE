//-----------------------------------------------------------------------------
// FILE:	    SshCommand.cs
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
    /// Implements the <b>ssh</b> command.
    /// </summary>
    public class SshCommand : CommandBase
    {
        private const string usage = @"
Opens a PuTTY SSH connection to the named node in the current hive
or the first manager node if no node is specified.

USAGE:

    neon ssh [NODE]

ARGUMENTS:

    NODE        - Optionally names the target hive node.
                  Otherwise, the first manager node will be opened.
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "ssh" }; }
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

            // The host's SSH key fingerprint looks something like the example below.  We
            // need to extract the MD5 HEX part to generate a PuTTY compatible fingerprint.
            //
            //      2048 MD5:cb:2f:f1:68:4b:aa:b3:8a:72:4d:53:f6:9f:5f:6a:fa root@manage-0 (RSA)

            const string    md5Pattern = "MD5:";
            string          fingerprint;
            int             startPos;
            int             endPos;

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
                fingerprint = hiveLogin.SshHiveHostKeyFingerprint.Substring(startPos).Trim();
            }
            else
            {
                fingerprint = hiveLogin.SshHiveHostKeyFingerprint.Substring(startPos, endPos - startPos).Trim();
            }

            // Launch PuTTY.

            if (!File.Exists(Program.PuttyPath))
            {
                Console.Error.WriteLine($"*** ERROR: PuTTY application is not installed at [{Program.PuttyPath}].");
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
                        Process.Start(Program.PuttyPath, $"-l {hiveLogin.SshUsername} -i \"{keyPath}\" {node.PrivateAddress}:22 -hostkey \"{fingerprint}\"");
                    }
                    finally
                    {
                        // Wait a bit for PuTTY to start and then delete the key.

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
                    }
                    break;

                case AuthMethods.Password:

                    Process.Start(Program.PuttyPath, $"-l {hiveLogin.SshUsername} -pw {hiveLogin.SshPassword} {node.PrivateAddress}:22 -hostkey \"{fingerprint}\"");
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
