//-----------------------------------------------------------------------------
// FILE:	    Program.ManagerPoller.cs
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
        /// Handles detection of changes to the hive's manager nodes.  The process will
        /// be terminated when manager nodes are added or removed so that Docker will restart
        /// the service to begin handling the changes.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task ManagerPollerAsync()
        {
            var periodicTask =
                new AsyncPeriodicTask(
                    managerPollInterval,
                    onTaskAsync:
                        async () =>
                        {
                            log.LogDebug(() => "MANAGER-POLLER: Polling for hive manager changes.");

                            var latestVaultUris = await GetVaultUrisAsync();
                            var changed         = vaultUris.Count != latestVaultUris.Count;

                            if (!changed)
                            {
                                for (int i = 0; i < vaultUris.Count; i++)
                                {
                                    if (vaultUris[i] != latestVaultUris[i])
                                    {
                                        changed = true;
                                        break;
                                    }
                                }
                            }

                            if (changed)
                            {
                                log.LogInfo("MANAGER-POLLER: Detected one or more hive manager node changes.");
                                log.LogInfo("MANAGER-POLLER: Exiting the service so that Docker will restart it to pick up the manager node changes.");
                                terminator.Exit();
                            }
                            else
                            {
                                log.LogDebug(() => "MANAGER-POLLER: No manager changes detected.");
                            }

                            log.LogDebug(() => "MANAGER-POLLER: Poll finished.");
                            return await Task.FromResult(false);
                        },
                    onExceptionAsync:
                        async e =>
                        {
                            log.LogError("MANAGER-POLLER", e);
                            return await Task.FromResult(false);
                        },
                    onTerminateAsync:
                        async () =>
                        {
                            log.LogInfo(() => "MANAGER-POLLER: Terminating");
                            await Task.CompletedTask;
                        });

                    terminator.AddDisposable(periodicTask);
                    await periodicTask.RunAsync();
        }
    }
}
