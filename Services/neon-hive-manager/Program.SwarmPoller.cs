//-----------------------------------------------------------------------------
// FILE:	    Program.SwarmPoller.cs
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
        /// Handles polling of Docker swarm about the hive nodes and updating the hive
        /// definition and hash when changes are detected.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task SwarmPollerAsync()
        {
            var periodicTask =
                new AsyncPeriodicTask(
                    swarmPollInterval,
                    onTaskAsync:
                        async () =>
                        {
                            try
                            {
                                log.LogDebug(() => "SWARM-POLLER: Polling");

                                // Retrieve the current hive definition from Consul if we don't already
                                // have it or if it's different from what we've cached.

                                cachedHiveDefinition = await HiveHelper.GetDefinitionAsync(cachedHiveDefinition, terminator.CancellationToken);

                                // Retrieve the swarm nodes from Docker.

                                log.LogDebug(() => $"SWARM-POLLER: Querying [{docker.Settings.Uri}]");

                                var swarmNodes = await docker.NodeListAsync();

                                // Parse the node definitions from the swarm nodes and build a new definition with
                                // using the new nodes.  Then compare the hashes of the cached and new hive definitions
                                // and then update Consul if they're different.

                                var currentHiveDefinition = NeonHelper.JsonClone<HiveDefinition>(cachedHiveDefinition);

                                currentHiveDefinition.NodeDefinitions.Clear();

                                foreach (var swarmNode in swarmNodes)
                                {
                                    var nodeDefinition = NodeDefinition.ParseFromLabels(swarmNode.Labels);

                                    nodeDefinition.Name = swarmNode.Hostname;

                                    currentHiveDefinition.NodeDefinitions.Add(nodeDefinition.Name, nodeDefinition);
                                }

                                log.LogDebug(() => $"SWARM-POLLER: [{currentHiveDefinition.Managers.Count()}] managers and [{currentHiveDefinition.Workers.Count()}] workers in current hive definition.");

                                // Hive pets are not part of the Swarm, so Docker won't return any information
                                // about them.  We'll read the pet definitions from [neon/global/pets-definition] in 
                                // Consul.  We'll assume that there are no pets if this key doesn't exist for
                                // backwards compatibility and robustness.

                                var petsJson = await HiveHelper.Consul.KV.GetStringOrDefault($"{HiveConst.GlobalKey}/{HiveGlobals.PetsDefinition}", terminator.CancellationToken);

                                if (petsJson == null)
                                {
                                    log.LogDebug(() => $"SWARM-POLLER: [{HiveConst.GlobalKey}/{HiveGlobals.PetsDefinition}] Consul key not found.  Assuming no pets.");
                                }
                                else
                                {
                                    if (!string.IsNullOrWhiteSpace(petsJson))
                                    {
                                        // Parse the pet node definitions and add them to the hive definition.

                                        var petDefinitions = NeonHelper.JsonDeserialize<Dictionary<string, NodeDefinition>>(petsJson);

                                        foreach (var item in petDefinitions)
                                        {
                                            currentHiveDefinition.NodeDefinitions.Add(item.Key, item.Value);
                                        }

                                        log.LogDebug(() => $"SWARM-POLLER: [{HiveConst.GlobalKey}/{HiveGlobals.PetsDefinition}] defines [{petDefinitions.Count}] pets.");
                                    }
                                    else
                                    {
                                        log.LogDebug(() => $"SWARM-POLLER: [{HiveConst.GlobalKey}/{HiveGlobals.PetsDefinition}] is empty.");
                                    }
                                }

                                // Fetch the hive summary and add it to the hive definition.

                                currentHiveDefinition.Summary = HiveSummary.FromHive(hive, currentHiveDefinition);

                                // Determine if the definition has changed.

                                currentHiveDefinition.ComputeHash();

                                if (currentHiveDefinition.Hash != cachedHiveDefinition.Hash)
                                {
                                    log.LogInfo(() => "SWARM-POLLER: Hive definition has CHANGED.  Updating Consul.");

                                    await HiveHelper.PutDefinitionAsync(currentHiveDefinition, cancellationToken: terminator.CancellationToken);

                                    cachedHiveDefinition = currentHiveDefinition;
                                }
                                else
                                {
                                    log.LogDebug(() => "SWARM-POLLER: Hive definition is UNCHANGED.");
                                }
                            }
                            catch (KeyNotFoundException)
                            {
                                // We'll see this when no hive definition has been persisted to the
                                // hive.  This is a serious problem.  This is configured during setup
                                // and there should always be a definition in Consul.

                                log.LogError(() => $"SWARM-POLLER: No hive definition has been found at [{hiveDefinitionKey}] in Consul.  This is a serious error that will have to be corrected manually.");
                            }

                            log.LogDebug(() => "SWARM-POLLER: Finished Poll");
                            return await Task.FromResult(false);
                        },
                    onExceptionAsync:
                        async e =>
                        {
                            log.LogError("SWARM-POLLER", e);
                            return await Task.FromResult(false);
                        },
                    onTerminateAsync:
                        async () =>
                        {
                            log.LogInfo(() => "SWARM-POLLER: Terminating");
                            await Task.CompletedTask;
                        });

            terminator.AddDisposable(periodicTask);
            await periodicTask.Run();
        }
    }
}
