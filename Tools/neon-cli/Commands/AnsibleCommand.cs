//-----------------------------------------------------------------------------
// FILE:	    AnsibleCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
    /// Implements the <b>ansible</b> commands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ansible is not supported on Windows and although it's possible to deploy Ansible
    /// on Mac OSX, we don't want to require it as a dependency to make the experience
    /// the same on Windows and Mac and also to simplify neonCLUSTER setup.  The <b>neon-cli</b>
    /// implements the <b>neon ansible...</b> commands to map files from the host operating
    /// system into a <b>neoncluster/neon-cli</b> container where Ansible is installed so any
    /// operations can be executed there.
    /// </para>
    /// <para>
    /// This command works by mapping the current client directory into the <b>neon-cli</b> 
    /// container at <b>/ansible</b> and then generating the hosts and host variables files at
    /// <b>/etc/ansible</b> in the container and then running <b>ansible ARGS</b> or 
    /// <b>ansible-playbook ARGS</b>, passing the SSH client certificate and any command 
    /// line arguments.
    /// </para>
    /// <note>
    /// This command makes no attempt to map files referenced by command line arguments
    /// or options into the container other than to map the current directory.
    /// </note>
    /// </remarks>
    public class AnsibleCommand : CommandBase
    {
        private const string usage = @"
USAGE:

    neon ansible exec -- ARGS       - runs an adhoc command via: ansible ARGS
    neon ansible playbook -- ARGS   - runs a playbook via: ansible-playbook ARGS

ARGS: Pass any valid Ansible options and arguments.

NOTE: This command makes no attempt to map files referenced by command line arguments
      or options into the container other than to map the current directory and to
      run the Ansible command within this mapped directory.
";

        private const string sshKeyPath       = "/dev/shm/ssh-client.key";   // Path to the SSH private client key
        private const string currentDirectory = "/cwd";                      // Path to the current working directory mapped into the container

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "ansible" }; }
        }

        /// <inheritdoc/>
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
                Help();
                Program.Exit(0);
            }

            var login              = NeonClusterHelper.ClusterLogin;
            var commandSplit       = commandLine.Split();
            var neonCommandLine    = commandSplit.Left;
            var ansibleCommandLine = commandSplit.Right;

            if (Program.DirectMode)
            {
                Console.WriteLine("*** ERROR: The [ansible] commands do not support [--direct] mode.");
                Program.Exit(1);
            }

            if (neonCommandLine.Arguments.Length == 0)
            {
                Help();
                Program.Exit(0);
            }

            if (NeonClusterHelper.ClusterLogin.Definition.HostNode.SshAuth != AuthMethods.Tls)
            {
                Console.WriteLine($"*** ERROR: The [ansible] commands require that the cluster nodes were deployed with [{nameof(HostNodeOptions)}.{nameof(HostNodeOptions.SshAuth)}.{nameof(AuthMethods.Tls)}].");
                Program.Exit(1);
            }

            // Write the cluster's SSH client private key to [/dev/shm/ssh-client.key] (which is a container RAM drive).

            File.WriteAllText(sshKeyPath, login.SshClientKey.PrivatePEM);

            // We need to execute the Ansible command within the client workstation's current directory 
            // mapped into the container.

            Environment.CurrentDirectory = currentDirectory;

            // Execute the command.

            switch (neonCommandLine.Arguments.First())
            {
                case "exec":

                    NeonHelper.Execute("ansible", NeonHelper.NormalizeExecArgs("--user", login.SshUsername, "--private-key", sshKeyPath, ansibleCommandLine.Items));
                    break;

                case "playbook":

                    NeonHelper.Execute("ansible-playbook", NeonHelper.NormalizeExecArgs("--user", login.SshUsername, "--private-key", sshKeyPath, ansibleCommandLine.Items));
                    break;

                default:

                    Help();
                    Program.Exit(1);
                    break;
            }
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            // We need to map the current directory into the container.

            shim.MappedFolders.Add(new DockerShimFolder(Environment.CurrentDirectory, currentDirectory));

            return new ShimInfo(isShimmed: true, ensureConnection: true);
        }
    }
}
