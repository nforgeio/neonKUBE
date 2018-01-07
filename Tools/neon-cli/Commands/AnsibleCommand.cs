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
using Neon.Net;

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
    /// <para>
    /// Variables are generated for each Docker label specified for each host node.  Each
    /// variable is prefixed by <b>label_</b> and all periods (.) in label names are converted
    /// to underscores (_).
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

ARGS: Any valid Ansible options and arguments.

NOTE: This command makes no attempt to map files referenced by command line arguments
      or options into the container other than to map the current directory and to
      run the Ansible command within this mapped directory.
";

        private const string sshClientPrivateKeyPath = "/dev/shm/ansible/ssh-client.key";  // Path to the SSH private client key (on a container RAM drive)
        private const string currentDirectory        = "/cwd";                             // Path to the current working directory mapped into the container

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

            var login              = Program.ClusterLogin;
            var commandSplit       = commandLine.Split();
            var neonCommandLine    = commandSplit.Left;
            var ansibleCommandLine = commandSplit.Right;

            if (!NeonClusterHelper.InToolContainer)
            {
                Console.WriteLine("*** ERROR: The [ansible] commands do not support [--direct] mode.");
                Program.Exit(1);
            }

            if (neonCommandLine.Arguments.Length == 0)
            {
                Help();
                Program.Exit(0);
            }

            if (ansibleCommandLine == null)
            {
                Console.WriteLine($"*** ERROR: The [ansible] commands require the [--] argument to prefix the Ansible arguments.");
                Program.Exit(1);
            }

            if (login.Definition.HostNode.SshAuth != AuthMethods.Tls)
            {
                Console.WriteLine($"*** ERROR: The [ansible] commands require that the cluster nodes were deployed with [{nameof(HostNodeOptions)}.{nameof(HostNodeOptions.SshAuth)}.{nameof(AuthMethods.Tls)}].");
                Program.Exit(1);
            }

            // Execute the command.

            switch (neonCommandLine.Arguments.First())
            {
                case "exec":

                    GenerateAnsibleFiles(login);
                    NeonHelper.Execute("ansible", NeonHelper.NormalizeExecArgs("--user", login.SshUsername, "--private-key", sshClientPrivateKeyPath, ansibleCommandLine.Items));
                    break;

                case "playbook":

                    GenerateAnsibleFiles(login);
                    NeonHelper.Execute("ansible-playbook", NeonHelper.NormalizeExecArgs("--user", login.SshUsername, "--private-key", sshClientPrivateKeyPath, ansibleCommandLine.Items));
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

        /// <summary>
        /// Generates Ansible files including host inventory related files and the private TLS client key.
        /// </summary>
        /// <param name="login">The cluster login.</param>
        private void GenerateAnsibleFiles(ClusterLogin login)
        {
            // Write the cluster's SSH client private key to [/dev/shm/ssh-client.key],
            // which is on a container RAM drive for security.  Note that the key file
            // must be locked down or else be rejected by Ansible.

            Directory.CreateDirectory(Path.GetDirectoryName(sshClientPrivateKeyPath));
            File.WriteAllText(sshClientPrivateKeyPath, login.SshClientKey.PrivatePEM);
            NeonHelper.Execute("chmod", $"600 \"{sshClientPrivateKeyPath}\"");

            // Generate the [/etc/ssh/ssh_known_hosts] file with the public SSH key of the cluster
            // nodes so Ansible will be able to verify host identity.  Note that all nodes share 
            // the same key.  This documented here:
            //
            //      http://man.openbsd.org/sshd.8

            var hostPublicKeyFields = login.SshClusterHostPublicKey.Split(" ");
            var hostPublicKey       = $"{hostPublicKeyFields[0]} {hostPublicKeyFields[1]}"; // Strip off the [user@host] field from the end (if present).

            using (var writer = new StreamWriter(new FileStream("/etc/ssh/ssh_known_hosts", FileMode.Create, FileAccess.ReadWrite), Encoding.ASCII))
            {
                foreach (var node in login.Definition.SortedNodes)
                {
                    writer.WriteLine($"# Node: {node.Name}");
                    writer.WriteLine($"{node.PrivateAddress} {hostPublicKey}");
                    writer.WriteLine();
                }
            }

            // We need to execute the Ansible command within the client workstation's current directory 
            // mapped into the container.

            Environment.CurrentDirectory = currentDirectory;

            // Generate the Ansible inventory and variable files.  We're going to use the cluster node
            // name for each host and then generate some standard ansible variables and then a generate
            // variable for host label.  Each label variable will be prefixed by "label_" with the the
            // label name appended and with any embedded periods converted to underscores.
            //
            // The hosts will be organized into four groups: managers, workers, pets, and swarm (where
            // swarm includes the managers and workers but not the pets.

            const string ansibleConfigFolder = "/etc/ansible";
            const string ansibleVarsFolder = ansibleConfigFolder + "/host_vars";

            Directory.CreateDirectory(ansibleConfigFolder);
            Directory.CreateDirectory(ansibleVarsFolder);

            // Generate the hosts file using the INI format.

            using (var writer = new StreamWriter(new FileStream(Path.Combine(ansibleConfigFolder, "hosts"), FileMode.Create, FileAccess.ReadWrite), Encoding.ASCII))
            {
                writer.WriteLine("[swarm]");

                foreach (var node in login.Definition.SortedNodes.Where(n => n.InSwarm))
                {
                    writer.WriteLine(node.Name);
                }

                writer.WriteLine();
                writer.WriteLine("[managers]");

                foreach (var node in login.Definition.SortedNodes.Where(n => n.IsManager))
                {
                    writer.WriteLine(node.Name);
                }

                writer.WriteLine();
                writer.WriteLine("[workers]");

                foreach (var node in login.Definition.SortedNodes.Where(n => n.IsWorker))
                {
                    writer.WriteLine(node.Name);
                }

                writer.WriteLine();
                writer.WriteLine("[pets]");

                foreach (var node in login.Definition.SortedNodes.Where(n => n.IsPet))
                {
                    writer.WriteLine(node.Name);
                }
            }

            // Generate host variable files as YAML.

            foreach (var node in login.Definition.Nodes)
            {
                using (var writer = new StreamWriter(new FileStream(Path.Combine(ansibleConfigFolder, "host_vars", node.Name), FileMode.Create, FileAccess.ReadWrite), Encoding.UTF8))
                {
                    writer.WriteLine("---");
                    writer.WriteLine("# Ansible variables:");
                    writer.WriteLine();
                    writer.WriteLine($"ansible_host: \"{node.PrivateAddress}\"");
                    writer.WriteLine($"ansible_port: \"{NetworkPorts.SSH}\"");
                    writer.WriteLine($"ansible_user: \"{login.SshUsername}\"");
                    writer.WriteLine($"ansible_ssh_private_key_file: \"{sshClientPrivateKeyPath}\"");
                    writer.WriteLine();
                    writer.WriteLine("# neonCLUSTER node variables:");
                    writer.WriteLine();

                    foreach (var label in node.Labels.Standard
                        .Union(node.Labels.Custom)
                        .OrderBy(l => l.Key))
                    {
                        var name = label.Key.Replace('.', '_');    // Convert periods in label names to underscores

                        // We may need to escape the label value to be YAML/Ansible safe.
                        // Note that I'm going to just go ahead and quote all values for
                        // simplicity.

                        var value = label.Value != null ? label.Value.ToString() : "null";
                        var sb    = new StringBuilder();

                        sb.Append('"');

                        foreach (var ch in value)
                        {
                            switch (ch)
                            {
                                case '{':

                                    sb.Append("{{");
                                    break;

                                case '}':

                                    sb.Append("}}");
                                    break;

                                case '"':

                                    sb.Append("\\\"");
                                    break;

                                case '\\':

                                    sb.Append("\\\\");
                                    break;

                                case '\r':

                                    sb.Append("\\r");
                                    break;

                                case '\n':

                                    sb.Append("\\n");
                                    break;

                                case '\t':

                                    sb.Append("\\t");
                                    break;

                                default:

                                    sb.Append(ch);
                                    break;
                            }
                        }

                        sb.Append('"');

                        writer.WriteLine($"{name}: {sb}");
                    }
                }
            }
        }
    }
}
