//-----------------------------------------------------------------------------
// FILE:	    Program.VaultUnsealer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using ICSharpCode.SharpZipLib.Zip;
using EasyNetQ.Management.Client.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Docker;
using Neon.Hive;
using Neon.HiveMQ;
using Neon.Net;
using Neon.Tasks;

namespace NeonHiveManager
{
    public static partial class Program
    {
        /// <summary>
        /// Returns the list of URIs targeting Vault on each current manager node.
        /// </summary>
        /// <returns>The Vault URIs.</returns>
        private static async Task<List<string>> GetVaultUrisAsync()
        {
            var vaultUris = new List<string>();

            if (NeonHelper.IsWindows)
            {
                // Assume that we're running in development mode if we're on Windows.

                vaultUris.Add(Environment.GetEnvironmentVariable("VAULT_DIRECT_ADDR"));
                return vaultUris;
            }

            // Vault runs on the hive managers so add a URI for each manager.
            // Note that we also need to ensure that each Vault manager hostname
            // has an entry in [/etc/hosts].
            //
            // Note that we need to use the direct Vault port rather than the 
            // Vault proxy port because we need to be able to address these
            // individually.

            var swarmNodes = await docker.NodeListAsync();
            var hosts      = File.ReadAllText("/etc/hosts");

            foreach (var managerNode in swarmNodes.Where(n => n.Role == "manager")
                .OrderBy(n => n.Hostname))
            {
                var vaultHostname = $"{managerNode.Hostname}.{hive.Definition.Hostnames.Vault}";

                vaultUris.Add($"https://{vaultHostname}:{NetworkPorts.Vault}");

                if (!hosts.Contains($"{vaultHostname} "))
                {
                    File.AppendAllText("/etc/hosts", $"{vaultHostname} {managerNode.Addr}\n");
                }
            }

            return vaultUris;
        }

        /// <summary>
        /// Handles polling of Vault seal status and automatic unsealing if enabled.
        /// </summary>
        /// <param name="vaultUri">The URI for the Vault instance being managed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task VaultUnsealerAsync(string vaultUri)
        {
            var lastVaultStatus = (VaultHealthStatus)null;

            // We're going to periodically log Vault status even
            // when there is no status changes.

            var statusUpdateTimeUtc  = DateTime.UtcNow;
            var statusUpdateInterval = TimeSpan.FromMinutes(30);

            log.LogInfo(() => $"VAULT-UNSEALER: Opening [{vaultUri}]");

            using (var vault = VaultClient.OpenWithToken(new Uri(vaultUri)))
            {
                var periodicTask =
                    new AsyncPeriodicTask(
                        vaultUnsealInterval,
                        onTaskAsync:
                            async () =>
                            {
                                if (IsSetupPending)
                                {
                                    log.LogInfo(() => "VAULT-UNSEALER: Delaying because hive setup is still in progress.");
                                    return false;
                                }

                                log.LogDebug(() => $"VAULT-UNSEALER: Polling [{vaultUri}]");

                                // Monitor Vault for status changes and handle unsealing if enabled.

                                log.LogDebug(() => $"VAULT-UNSEALER: Querying [{vaultUri}]");

                                var newVaultStatus     = await vault.GetHealthAsync(terminator.CancellationToken);
                                var autoUnsealDisabled = consul.KV.GetBoolOrDefault($"{HiveConst.GlobalKey}/{HiveGlobals.UserDisableAutoUnseal}").Result;
                                var changed            = false;

                                if (lastVaultStatus == null)
                                {
                                    changed = true;
                                }
                                else
                                {
                                    changed = !lastVaultStatus.Equals(newVaultStatus);
                                }

                                if (changed)
                                {
                                    if (!newVaultStatus.IsInitialized || newVaultStatus.IsSealed)
                                    {
                                        log.LogError(() => $"VAULT-UNSEALER: status CHANGED [{vaultUri}]");
                                    }
                                    else
                                    {
                                        log.LogInfo(() => $"VAULT-UNSEALER: status CHANGED [{vaultUri}]");
                                    }

                                    statusUpdateTimeUtc = DateTime.UtcNow; // Force logging status below
                                }

                                if (DateTime.UtcNow >= statusUpdateTimeUtc)
                                {
                                    if (!newVaultStatus.IsInitialized || newVaultStatus.IsSealed)
                                    {
                                        log.LogError(() => $"VAULT-UNSEALER: status={newVaultStatus} [{vaultUri}]");
                                    }
                                    else
                                    {
                                        log.LogInfo(() => $"VAULT-UNSEALER: status={newVaultStatus} [{vaultUri}]");
                                    }

                                    if (newVaultStatus.IsSealed && autoUnsealDisabled)
                                    {
                                        log.LogInfo(() => $"VAULT-UNSEALER: AUTO-UNSEAL is temporarily DISABLED because Consul [{HiveConst.GlobalKey}/{HiveGlobals.UserDisableAutoUnseal}=true].");
                                    }

                                    statusUpdateTimeUtc = DateTime.UtcNow + statusUpdateInterval;
                                }

                                lastVaultStatus = newVaultStatus;

                                // Attempt to unseal the Vault if it's sealed and we have the keys.

                                if (newVaultStatus.IsSealed && vaultCredentials != null)
                                {
                                    if (autoUnsealDisabled)
                                    {
                                        return await Task.FromResult(false);    // Don't unseal.
                                    }

                                    log.LogInfo(() => $"VAULT-UNSEALER: UNSEALING [{vaultUri}]");
                                    await vault.UnsealAsync(vaultCredentials, terminator.CancellationToken);
                                    log.LogInfo(() => $"VAULT-UNSEALER: UNSEALED [{vaultUri}]");

                                    // Schedule a status update on the next loop
                                    // and then loop immediately so we'll log the
                                    // updated status.

                                    statusUpdateTimeUtc = DateTime.UtcNow;
                                    return await Task.FromResult(false);
                                }

                                return await Task.FromResult(false);
                            },
                        onExceptionAsync:
                            async e =>
                            {
                                log.LogError("VAULT-UNSEALER", e);
                                return await Task.FromResult(false);
                            },
                        onTerminateAsync:
                            async () =>
                            {
                                log.LogInfo(() => "VAULT-UNSEALER: Terminating");
                                await Task.CompletedTask;
                            });

                terminator.AddDisposable(periodicTask);
                await periodicTask.Run();
            }
        }
    }
}
