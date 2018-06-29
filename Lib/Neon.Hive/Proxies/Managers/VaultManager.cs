//-----------------------------------------------------------------------------
// FILE:	    VaultManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Docker;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Hive
{
    /// <summary>
    /// Handles HashiCorp Vault related operations for a <see cref="ClusterProxy"/>.
    /// </summary>
    public sealed class VaultManager : IDisposable
    {
        private object          syncRoot = new object();
        private ClusterProxy    cluster;
        private VaultClient     client;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="cluster">The parent <see cref="ClusterProxy"/>.</param>
        internal VaultManager(ClusterProxy cluster)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            this.cluster = cluster;
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            lock (syncRoot)
            {
                if (client != null)
                {
                    try
                    {
                        client.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Intentially ignoring these.
                    }
                    finally
                    {
                        client = null;
                    }
                }
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Returns a Vault client using the root token.
        /// </summary>
        /// <returns>The <see cref="VaultClient"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="ClusterLogin"/> has not yet been initialized with the Vault root token.</exception>
        public VaultClient Client
        {
            get
            {
                if (!cluster.ClusterLogin.HasVaultRootCredentials)
                {
                    throw new InvalidOperationException($"[{nameof(ClusterProxy)}.{nameof(ClusterLogin)}] has not yet been initialized with the Vault root token.");
                }

                lock (syncRoot)
                {
                    if (client != null)
                    {
                        return client;
                    }

                    client = VaultClient.OpenWithToken(new Uri(cluster.Definition.Vault.Uri), cluster.ClusterLogin.VaultCredentials.RootToken);
                }

                return client;
            }
        }

        /// <summary>
        /// Ensure that we have the Vault token.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the root token is not available.</exception>
        private void VerifyToken()
        {
            if (!cluster.ClusterLogin.HasVaultRootCredentials)
            {
                throw new InvalidOperationException($"[{nameof(ClusterProxy)}.{nameof(ClusterLogin)}] has not yet been initialized with the Vault root token.");
            }
        }

        /// <summary>
        /// Wait for all Vault instances to report being unsealed and then
        /// to be able to perform an operation (e.g. writing a secret).
        /// </summary>
        public void WaitUntilReady()
        {
            var readyManagers = new HashSet<string>();
            var timeout       = TimeSpan.FromSeconds(120);
            var timer         = new Stopwatch();

            // Wait for all of the managers to report being unsealed.

            timer.Start();

            foreach (var manager in cluster.Managers)
            {
                manager.Status = "vault: verify unsealed";
            }

            while (readyManagers.Count < cluster.Managers.Count())
            {
                if (timer.Elapsed >= timeout)
                {
                    var sbNotReadyManagers = new StringBuilder();

                    foreach (var manager in cluster.Managers.Where(m => !readyManagers.Contains(m.Name)))
                    {
                        sbNotReadyManagers.AppendWithSeparator(manager.Name, ", ");
                    }

                    throw new HiveException($"Vault not unsealed after waiting [{timeout}] on: {sbNotReadyManagers}");
                }

                foreach (var manager in cluster.Managers.Where(m => !readyManagers.Contains(m.Name)))
                {
                    var response = manager.SudoCommand("vault-direct status");

                    if (response.ExitCode == 0)
                    {
                        readyManagers.Add(manager.Name);
                        manager.Status = "vault: unsealed";
                    }
                }

                Thread.Sleep(TimeSpan.FromSeconds(2));
            }

            // Now, verify that all managers are really ready by verifying that
            // we can write a Vault secret to each of them.  We'll keep retrying
            // for a while when this fails.

            readyManagers.Clear();
            timer.Restart();

            foreach (var manager in cluster.Managers)
            {
                manager.Status = "vault: check ready";
            }

            while (readyManagers.Count < cluster.Managers.Count())
            {
                if (timer.Elapsed >= timeout)
                {
                    var sbNotReadyManagers = new StringBuilder();

                    foreach (var manager in cluster.Managers.Where(m => !readyManagers.Contains(m.Name)))
                    {
                        sbNotReadyManagers.AppendWithSeparator(manager.Name, ", ");
                    }

                    throw new HiveException($"Vault not ready after waiting [{timeout}] on: {sbNotReadyManagers}");
                }

                foreach (var manager in cluster.Managers.Where(m => !readyManagers.Contains(m.Name)))
                {
                    var response = Command(manager, $"vault-direct write secret {manager.Name}-ready=true");

                    if (response.ExitCode == 0)
                    {
                        readyManagers.Add(manager.Name);
                        manager.Status = "vault: ready";
                    }
                }

                Thread.Sleep(TimeSpan.FromSeconds(2));
            }

            // Looks like all Vault instances are ready, so remove the secrets we added.

            foreach (var manager in cluster.Managers.Where(m => !readyManagers.Contains(m.Name)))
            {
                CommandNoFault(manager, $"vault-direct delete secret {manager.Name}-ready");
            }

            foreach (var manager in cluster.Managers)
            {
                manager.Status = string.Empty;
            }
        }

        /// <summary>
        /// Executes a command on a healthy cluster manager node using the root Vault token.
        /// </summary>
        /// <param name="command">The command (including the <b>vault</b>).</param>
        /// <param name="args">The optional arguments.</param>
        /// <returns>The command response.</returns>
        /// <remarks>
        /// <note>
        /// This method faults and throws an exception if the command returns
        /// a non-zero exit code.
        /// </note>
        /// </remarks>
        public CommandResponse Command(string command, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(command != null);

            return Command(cluster.GetHealthyManager(), command, args);
        }

        /// <summary>
        /// Executes a command on a specific cluster manager node using the root Vault token.
        /// </summary>
        /// <param name="manager">The target manager.</param>
        /// <param name="command">The command (including the <b>vault</b>).</param>
        /// <param name="args">The optional arguments.</param>
        /// <returns>The command response.</returns>
        /// <remarks>
        /// <note>
        /// This method faults and throws an exception if the command returns
        /// a non-zero exit code.
        /// </note>
        /// </remarks>
        public CommandResponse Command(SshProxy<NodeDefinition> manager, string command, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(manager != null);
            Covenant.Requires<ArgumentNullException>(command != null);

            VerifyToken();

            var scriptBundle = new CommandBundle(command, args);
            var bundle       = new CommandBundle("./vault-command.sh");

            bundle.AddFile("vault-command.sh",
$@"#!/bin/bash
export VAULT_TOKEN={cluster.ClusterLogin.VaultCredentials.RootToken}
{scriptBundle}
",
                isExecutable: true);

            var response = manager.SudoCommand(bundle, cluster.SecureRunOptions | RunOptions.FaultOnError);

            response.BashCommand = bundle.ToBash();

            return response;
        }

        /// <summary>
        /// Executes a command on a specific cluster manager node using the root Vault token.
        /// </summary>
        /// <param name="manager">The target manager.</param>
        /// <param name="command">The command (including the <b>vault</b>).</param>
        /// <param name="args">The optional arguments.</param>
        /// <returns>The command response.</returns>
        /// <remarks>
        /// <note>
        /// This method does not fault or throw an exception if the command returns
        /// a non-zero exit code.
        /// </note>
        /// </remarks>
        public CommandResponse CommandNoFault(SshProxy<NodeDefinition> manager, string command, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(manager != null);
            Covenant.Requires<ArgumentNullException>(command != null);

            VerifyToken();

            var scriptBundle = new CommandBundle(command, args);
            var bundle       = new CommandBundle("./vault-command.sh");

            bundle.AddFile("vault-command.sh",
$@"#!/bin/bash
export VAULT_TOKEN={cluster.ClusterLogin.VaultCredentials.RootToken}
{scriptBundle}
",
                isExecutable: true);

            var response = manager.SudoCommand(bundle, cluster.SecureRunOptions);

            response.BashCommand = bundle.ToBash();

            return response;
        }

        /// <summary>
        /// Executes a command on a healthy cluster manager node using the root Vault token.
        /// </summary>
        /// <param name="command">The command (including the <b>vault</b>).</param>
        /// <param name="args">The optional arguments.</param>
        /// <returns>The command response.</returns>
        /// <remarks>
        /// <note>
        /// This method does not fault or throw an exception if the command returns
        /// a non-zero exit code.
        /// </note>
        /// </remarks>
        public CommandResponse CommandNoFault(string command, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(command != null);

            return CommandNoFault(cluster.GetHealthyManager(), command, args);
        }

        /// <summary>
        /// Sets a Vault access control policy.
        /// </summary>
        /// <param name="policy">The policy.</param>
        /// <returns>The command response.</returns>
        public CommandResponse SetPolicy(VaultPolicy policy)
        {
            Covenant.Requires<ArgumentNullException>(policy != null);

            VerifyToken();

            var bundle = new CommandBundle("./create-vault-policy.sh");

            bundle.AddFile("create-vault-policy.sh",
$@"#!/bin/bash
export VAULT_TOKEN={cluster.ClusterLogin.VaultCredentials.RootToken}
vault policy-write {policy.Name} policy.hcl
",
                isExecutable: true);

            bundle.AddFile("policy.hcl", policy);

            var response = cluster.GetHealthyManager().SudoCommand(bundle,cluster.SecureRunOptions | RunOptions.FaultOnError);

            response.BashCommand = bundle.ToBash();

            return response;
        }

        /// <summary>
        /// Removes a Vault access control policy.
        /// </summary>
        /// <param name="policyName">The policy name.</param>
        /// <returns>The command response.</returns>
        public CommandResponse RemovePolicy(string policyName)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(policyName));

            return Command($"vault policy-delete {policyName}");
        }

        /// <summary>
        /// Sets a Vault AppRole.
        /// </summary>
        /// <param name="roleName">The role name.</param>
        /// <param name="policies">The policy names or HCL details.</param>
        /// <returns>The command response.</returns>
        public CommandResponse SetAppRole(string roleName, params string[] policies)
        {
            Covenant.Requires<ArgumentNullException>(roleName != null);
            Covenant.Requires<ArgumentNullException>(policies != null);

            var sbPolicies = new StringBuilder();

            if (sbPolicies != null)
            {
                foreach (var policy in policies)
                {
                    if (string.IsNullOrEmpty(policy))
                    {
                        throw new ArgumentNullException("Null or empty policy.");
                    }

                    sbPolicies.AppendWithSeparator(policy, ",");
                }
            }

            // Note that we have to escape any embedded double quotes in the policies
            // because they may include HCL rather than being just policy names.

            return Command($"vault write auth/approle/role/{roleName} \"policies={sbPolicies.Replace("\"", "\"\"")}\"");
        }

        /// <summary>
        /// Removes a Vault AppRole.
        /// </summary>
        /// <param name="roleName">The role name.</param>
        /// <returns>The command response.</returns>
        public CommandResponse RemoveAppRole(string roleName)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(roleName));

            return Command($"vault delete auth/approle/role/{roleName}");
        }

        /// <summary>
        /// Seals all Vault instances.
        /// </summary>
        /// <returns><c>true</c> if all instances were sealed.</returns>
        /// <exception cref="HiveException">Thrown if the current login doesn't have root privileges</exception>
        public bool Seal()
        {
            var failed = false;

            cluster.EnsureRootPrivileges();

            foreach (var manager in cluster.Managers)
            {
                var response = manager.SudoCommand($"vault-direct status");

                if (response.ExitCode != 0)
                {
                    continue;   // Already sealed
                }

                var vaultStatus = new VaultStatus(response.OutputText);

                if (vaultStatus.HAMode == "standby")
                {
                    // Restart to seal standby node

                    response = manager.SudoCommand($"systemctl restart vault");

                    if (response.ExitCode != 0)
                    {
                        failed = true;
                    }
                }
                else
                {
                    response = manager.SudoCommand($"export VAULT_TOKEN={cluster.ClusterLogin.VaultCredentials.RootToken} && vault-direct operator seal", RunOptions.Redact);

                    if (response.ExitCode != 0)
                    {
                        failed = true;
                    }
                }
            }

            return !failed;
        }

        /// <summary>
        /// Unseals all Vault instances.
        /// </summary>
        /// <returns><c>true</c> if all instances were unsealed.</returns>
        /// <exception cref="HiveException">Thrown if the current login doesn't have root privileges</exception>
        public bool Unseal()
        {
            var failed = false;

            cluster.EnsureRootPrivileges();

            foreach (var manager in cluster.Managers)
            {
                // Verify that the instance isn't already unsealed.

                var response = manager.SudoCommand($"vault-direct status");

                if (response.ExitCode == 0)
                {
                    continue;   // Already unsealed
                }
                else if (response.ExitCode != 2)
                {
                    failed = true;
                    continue;
                }

                // ExitCode==2 means the instance is sealed.

                // Note that we're passing the [-reset] option to ensure that 
                // any keys from previous attempts have been cleared.

                manager.SudoCommand($"vault-direct operator unseal -reset");

                foreach (var key in cluster.ClusterLogin.VaultCredentials.UnsealKeys)
                {
                    response = manager.SudoCommand($"vault-direct operator unseal {key}", RunOptions.None);

                    if (response.ExitCode != 0)
                    {
                        failed = true;

                        Console.WriteLine($"[{manager.Name}] unseal failed");
                    }
                }
            }

            return !failed;
        }
    }
}
