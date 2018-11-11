//-----------------------------------------------------------------------------
// FILE:	    VaultCommand.cs
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

using Consul;
using Newtonsoft;
using Newtonsoft.Json;
using Renci.SshNet.Common;

using Neon.Common;
using Neon.Hive;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>vault</b> commands.
    /// </summary>
    public class VaultCommand : CommandBase
    {
        private const string usage = @"
Runs a HashiCorp Vault command on the hive.  All command line arguments
and options as well are passed through to the Vault CLI.  The command also
includes the root Vault token from the current hive login.

USAGE:

    neon [OPTIONS] vault -- CMD [ARGS...]  - Invokes a HashiCorp Vault command

ARGUMENTS:

    CMD             - Vault command
    ARGS            - Vault options and arguments

OPTIONS:

    --node=NODE     - Specifies the target node.  The Vault command 
                      will be executed on the first manager node when  
                      this isn't specified.

NOTE: Vault commands are automatically provided with the root token from the 
      current hive login.

NOTE: The [seal], [unseal], and [status] commands have been modified
      to operate on the entire Vault cluster unless a target [--node]
      is specified.

NOTE: Vault commands may only be submitted to manager nodes.

NOTE: The following Vault commands are not supported:

      init, rekey, server and ssh
";
        private HiveProxy           hive;
        private VaultCredentials    vaultCredentials;

        private const string remoteVaultPath = "/usr/local/bin/vault";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "vault" }; }
        }

        /// <inheritdoc/>
        public override string SplitItem
        {
            get { return "--"; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--node" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            // Split the command line on "--".

            var split = commandLine.Split(SplitItem);

            var leftCommandLine  = split.Left;
            var rightCommandLine = split.Right;

            // Basic initialization.

            if (leftCommandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            // Initialize the hive.

            var hiveLogin = Program.ConnectHive();

            hive             = new HiveProxy(hiveLogin);
            vaultCredentials = hiveLogin.VaultCredentials;

            if (rightCommandLine == null)
            {
                Console.WriteLine("*** ERROR: The [--] command separator is required.");
                Console.WriteLine();
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            // Determine which node we're going to target.

            SshProxy<NodeDefinition>    node;
            string                      nodeName = leftCommandLine.GetOption("--node", null);
            CommandBundle               bundle;
            CommandResponse             response;
            bool                        failed = false;

            if (!string.IsNullOrEmpty(nodeName))
            {
                node = hive.GetNode(nodeName);
            }
            else
            {
                node = hive.GetReachableManager();
            }

            var command = rightCommandLine.Arguments.FirstOrDefault(); ;

            switch (command)
            {
                case "init":
                case "rekey":
                case "server":
                case "ssh":

                    Console.Error.WriteLine($"*** ERROR: [neon vault {command}] is not supported.");
                    Program.Exit(1);
                    break;

                case "seal":

                    // Just run the command on the target node if one was specified.

                    if (nodeName != null)
                    {
                        ExecuteOnNode(node, rightCommandLine);
                        return;
                    }

                    // We need to seal the Vault instance on every manager node unless a
                    // specific node was requsted via [--node].
                    //
                    // Note also that it's not possible to seal a node that's in standby
                    // mode so we'll restart the Vault container instead.

                    Console.WriteLine();

                    if (!string.IsNullOrEmpty(nodeName))
                    {
                        response = node.SudoCommand($"vault-direct status");

                        if (response.ExitCode != 0)
                        {
                            Console.WriteLine($"[{node.Name}] is already sealed");
                        }
                        else
                        {
                            var vaultStatus = new VaultStatus(response.OutputText);

                            if (vaultStatus.HAMode == "standby")
                            {
                                Console.WriteLine($"[{node.Name}] restarting to seal standby node...");

                                response = node.SudoCommand($"systemctl restart vault");

                                if (response.ExitCode == 0)
                                {
                                    Console.WriteLine($"[{node.Name}] sealed");
                                }
                                else
                                {
                                    Console.WriteLine($"[{node.Name}] restart failed");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"[{node.Name}] sealing...");

                                response = node.SudoCommand($"export VAULT_TOKEN={vaultCredentials.RootToken} && vault-direct operator seal", RunOptions.Redact);

                                if (response.ExitCode == 0)
                                {
                                    Console.WriteLine($"[{node.Name}] sealed");
                                }
                                else
                                {
                                    Console.WriteLine($"[{node.Name}] seal failed");
                                }
                            }
                        }

                        failed = response.ExitCode != 0;
                    }
                    else
                    {
                        foreach (var manager in hive.Managers)
                        {
                            try
                            {
                                response = manager.SudoCommand($"vault-direct status");
                            }
                            catch (SshOperationTimeoutException)
                            {
                                Console.WriteLine($"[{manager.Name}] ** unavailable **");
                                continue;
                            }

                            var vaultStatus = new VaultStatus(response.OutputText);

                            if (response.ExitCode != 0)
                            {
                                Console.WriteLine($"[{manager.Name}] is already sealed");
                            }
                            else
                            {
                                response = manager.SudoCommand($"vault-direct operator seal");

                                if (vaultStatus.HAMode == "standby")
                                {
                                    Console.WriteLine($"[{manager.Name}] restarting to seal standby node...");

                                    response = manager.SudoCommand($"systemctl restart vault");

                                    if (response.ExitCode == 0)
                                    {
                                        Console.WriteLine($"[{manager.Name}] restart/seal [standby]");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"[{manager.Name}] restart/seal failed [standby]");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"[{manager.Name}] sealing...");

                                    response = manager.SudoCommand($"export VAULT_TOKEN={vaultCredentials.RootToken} && vault-direct operator seal", RunOptions.Redact);

                                    if (response.ExitCode == 0)
                                    {
                                        Console.WriteLine($"[{manager.Name}] sealed");
                                    }
                                    else
                                    {
                                        failed = true;
                                        Console.WriteLine($"[{manager.Name}] seal failed");
                                    }
                                }
                            }
                        }

                        if (!failed)
                        {
                            // Disable auto unseal until the operator explicitly unseals Vault again.

                            hive.Consul.Client.KV.PutBool($"{HiveConst.GlobalKey}/{HiveGlobals.UserDisableAutoUnseal}", true).Wait();
                        }

                        Program.Exit(failed ? 1 : 0);
                    }
                    break;

                case "status":

                    // Just run the command on the target node if one was specified.

                    if (nodeName != null)
                    {
                        ExecuteOnNode(node, rightCommandLine);
                        return;
                    }

                    // We need to obtain the status from the Vault instance on every manager node unless a
                    // specific node was requsted via [--node].

                    Console.WriteLine();

                    if (!string.IsNullOrEmpty(nodeName))
                    {
                        response = node.SudoCommand("vault-direct status");

                        Console.WriteLine(response.AllText);
                        Program.Exit(response.ExitCode);
                    }
                    else
                    {
                        var allSealed = true;

                        foreach (var manager in hive.Managers)
                        {
                            try
                            {
                                response = manager.SudoCommand("vault-direct status");
                            }
                            catch (SshOperationTimeoutException)
                            {
                                Console.WriteLine($"[{manager.Name}] ** unavailable **");
                                continue;
                            }

                            var vaultStatus = new VaultStatus(response.OutputText);

                            if (response.ExitCode == 0)
                            {
                                allSealed = false;

                                var status = vaultStatus.HAMode;

                                if (status == "active")
                                {
                                    status += "  <-- LEADER";
                                }

                                Console.WriteLine($"[{manager.Name}] unsealed {status}");
                            }
                            else if (response.ExitCode == 2)
                            {
                                Console.WriteLine($"[{manager.Name}] sealed");
                            }
                            else
                            {
                                failed = true;
                                Console.WriteLine($"[{manager.Name}] error getting status");
                            }
                        }

                        if (allSealed)
                        {
                            Program.Exit(2);
                        }
                        else
                        {
                            Program.Exit(failed ? 1 : 0);
                        }
                    }
                    break;

                case "unseal":

                    // Just run the command on the target node if one was specified.

                    if (nodeName != null)
                    {
                        ExecuteOnNode(node, rightCommandLine);
                        return;
                    }

                    // We need to unseal the Vault instance on every manager node unless a
                    // specific node was requsted via [--node].

                    Console.WriteLine();

                    if (!string.IsNullOrEmpty(nodeName))
                    {
                        // Verify that the instance isn't already unsealed.

                        response = node.SudoCommand($"vault-direct status");

                        if (response.ExitCode == 2)
                        {
                            Console.WriteLine($"[{node.Name}] unsealing...");
                        }
                        else if (response.ExitCode == 0)
                        {
                            Console.WriteLine($"[{node.Name}] is already unsealed");
                            break;
                        }
                        else
                        {
                            Console.WriteLine($"[{node.Name}] unseal failed");
                            Program.Exit(response.ExitCode);
                        }

                        // Note that we're passing the [-reset] option to ensure that 
                        // any keys from previous attempts have been cleared.

                        node.SudoCommand($"vault-direct operator unseal -reset");

                        foreach (var key in vaultCredentials.UnsealKeys)
                        {
                            response = node.SudoCommand($"vault-direct operator unseal {key}", RunOptions.None);

                            if (response.ExitCode != 0)
                            {
                                Console.WriteLine($"[{node.Name}] unseal failed");
                                Program.Exit(1);
                            }
                        }

                        Console.WriteLine($"[{node.Name}] unsealed");
                    }
                    else
                    {
                        var commandFailed = false;

                        foreach (var manager in hive.Managers)
                        {
                            // Verify that the instance isn't already unsealed.

                            try
                            {
                                response = manager.SudoCommand($"vault-direct status");
                            }
                            catch (SshOperationTimeoutException)
                            {
                                Console.WriteLine($"[{manager.Name}] ** unavailable **");
                                continue;
                            }

                            if (response.ExitCode == 2)
                            {
                                Console.WriteLine($"[{manager.Name}] unsealing...");
                            }
                            else if (response.ExitCode == 0)
                            {
                                Console.WriteLine($"[{manager.Name}] is already unsealed");
                                continue;
                            }
                            else
                            {
                                Console.WriteLine($"[{manager.Name}] unseal failed");
                                continue;
                            }

                            // Note that we're passing the [-reset] option to ensure that 
                            // any keys from previous attempts have been cleared.

                            manager.SudoCommand($"vault-direct operator unseal -reset");

                            foreach (var key in vaultCredentials.UnsealKeys)
                            {
                                response = manager.SudoCommand($"vault-direct operator unseal {key}", RunOptions.None);

                                if (response.ExitCode != 0)
                                {
                                    failed        = true;
                                    commandFailed = true;

                                    Console.WriteLine($"[{manager.Name}] unseal failed");
                                }
                            }

                            if (!failed)
                            {
                                Console.WriteLine($"[{manager.Name}] unsealed");
                            }
                        }

                        if (!commandFailed)
                        {
                            // Reenable auto unseal.

                            hive.Consul.Client.KV.PutBool($"{HiveConst.GlobalKey}/{HiveGlobals.UserDisableAutoUnseal}", false).Wait();
                        }

                        Program.Exit(commandFailed ? 1 : 0);
                    }
                    break;

                case "write":

                    {
                        // We need handle any [key=@file] arguments specially by including them
                        // in a command bundle as data files.

                        var files         = new List<CommandFile>();
                        var commandString = rightCommandLine.ToString();

                        foreach (var dataArg in rightCommandLine.Arguments.Skip(2))
                        {
                            var fields = dataArg.Split(new char[] { '=' }, 2);

                            if (fields.Length == 2 && fields[1].StartsWith("@"))
                            {
                                var fileName      = fields[1].Substring(1);
                                var localFileName = $"{files.Count}.data";

                                files.Add(
                                    new CommandFile()
                                    {
                                         Path = localFileName,
                                         Data = File.ReadAllBytes(fileName)
                                    });

                                commandString = commandString.Replace($"@{fileName}", $"@{localFileName}");
                            }
                        }

                        bundle = new CommandBundle($"export VAULT_TOKEN={vaultCredentials.RootToken} && {remoteVaultPath} {commandString}");

                        foreach (var file in files)
                        {
                            bundle.Add(file);
                        }

                        response = node.SudoCommand(bundle, RunOptions.IgnoreRemotePath | RunOptions.Redact);

                        Console.WriteLine(response.AllText);
                        Program.Exit(response.ExitCode);
                    }
                    break;

                case "policy-write":

                    // The last command line item is either:
                    //
                    //      * A "-", indicating that the content should come from standard input.
                    //      * A file name prefixed by "@"
                    //      * A string holding JSON or HCL

                    if (rightCommandLine.Items.Length < 2)
                    {
                        response = node.SudoCommand($"export VAULT_TOKEN={vaultCredentials.RootToken} && {remoteVaultPath} {rightCommandLine}", RunOptions.IgnoreRemotePath | RunOptions.Redact);

                        Console.WriteLine(response.AllText);
                        Program.Exit(response.ExitCode);
                    }

                    var lastItem   = rightCommandLine.Items.Last();
                    var policyText = string.Empty;

                    if (lastItem == "-")
                    {
                        policyText = NeonHelper.ReadStandardInputText();
                    }
                    else if (lastItem.StartsWith("@"))
                    {
                        policyText = File.ReadAllText(lastItem.Substring(1), Encoding.UTF8);
                    }
                    else
                    {
                        policyText = lastItem;
                    }

                    // We're going to upload a text file holding the policy text and
                    // then run a script piping the policy file into the Vault command passed, 
                    // with the last item replaced by a "-". 

                    bundle = new CommandBundle("./set-vault-policy.sh.sh");

                    var sbScript = new StringBuilder();

                    sbScript.AppendLine($"export VAULT_TOKEN={vaultCredentials.RootToken}");
                    sbScript.Append($"cat policy | {remoteVaultPath}");

                    for (int i = 0; i < rightCommandLine.Items.Length - 1; i++)
                    {
                        sbScript.Append(' ');
                        sbScript.Append(rightCommandLine.Items[i]);
                    }

                    sbScript.AppendLine(" -");

                    bundle.AddFile("set-vault-policy.sh", sbScript.ToString(), isExecutable: true);
                    bundle.AddFile("policy", policyText);

                    response = node.SudoCommand(bundle, RunOptions.IgnoreRemotePath | RunOptions.Redact);

                    Console.WriteLine(response.AllText);
                    Program.Exit(response.ExitCode);
                    break;

                default:

                    ExecuteOnNode(node, rightCommandLine);
                    break;
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            Program.LogPath = null;

            var commandLine = shim.CommandLine;

            if (commandLine.Arguments.Length > 2)
            {
                switch (commandLine[1])
                {
                    case "write":

                        // We need handle any [key=@file] arguments specially by adding
                        // them to the shim.

                        foreach (var arg in commandLine.Arguments.Skip(2))
                        {
                            var pos = arg.IndexOf("=@");

                            if (pos != -1)
                            {
                                var shimFile = shim.AddFile(arg.Substring(pos + 2), dontReplace: true);

                                shim.ReplaceItem(arg, arg.Substring(0, pos + 2) + shimFile);
                            }
                        }
                        break;

                    case "policy-write":

                        // The last command line item is either:
                        //
                        //      * A "-", indicating that the content should come from standard input.
                        //      * A file name prefixed by "@"
                        //      * A string holding JSON or HCL

                        if (commandLine.Arguments.LastOrDefault() == "-")
                        {
                            shim.AddStdin();
                        }
                        else
                        {
                            var lastArg = commandLine.Arguments.LastOrDefault();

                            if (lastArg.StartsWith("@"))
                            {
                                var shimFile = shim.AddFile(lastArg.Substring(1), dontReplace: true);

                                shim.ReplaceItem(lastArg, "@" + shimFile);
                            }
                        }
                        break;
                }
            }

            return new DockerShimInfo(shimability: DockerShimability.Optional, ensureConnection: true);
        }

        /// <summary>
        /// Executes a Vault command on a specific node using the root Vault token.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="commandLine">The Vault command.</param>
        private void ExecuteOnNode(SshProxy<NodeDefinition> node, CommandLine commandLine)
        {
            var response = node.SudoCommand($"export VAULT_TOKEN={vaultCredentials.RootToken} && {remoteVaultPath} {commandLine}", RunOptions.IgnoreRemotePath | RunOptions.Redact);

            Console.WriteLine(response.AllText);
            Program.Exit(response.ExitCode);
        }
    }
}
