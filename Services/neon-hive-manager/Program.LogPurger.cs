//-----------------------------------------------------------------------------
// FILE:	    Program.LogPurger.cs
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
        /// Handles purging of old <b>logstash</b> and <b>metricbeat</b> Elasticsearch indexes.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task LogPurgerAsync()
        {
            using (var jsonClient = new JsonClient())
            {
                var periodicTask =
                    new AsyncPeriodicTask(
                        logPurgerInterval,
                        onTaskAsync:
                            async () =>
                            {
                                if (IsSetupPending)
                                {
                                    log.LogInfo(() => "LOG-PURGER: Delaying because hive setup is still in progress.");
                                    return false;
                                }

                                var manager = hive.GetReachableManager();

                                log.LogDebug(() => "LOG-PURGER: Scanning for old Elasticsearch indexes ready for removal.");

                                // We're going to list the indexes and look for [logstash]
                                // and [metricbeat] indexes that encode the index date like:
                                //
                                //      logstash-2018.06.06
                                //      metricbeat-6.1.1-2018.06.06
                                //
                                // The date is simply encodes the day covered by the index.

                                if (!hive.Globals.TryGetInt(HiveGlobals.UserLogRetentionDays, out var retentionDays))
                                {
                                    retentionDays = 14;
                                }

                                var utcNow           = DateTime.UtcNow;
                                var deleteBeforeDate = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day) - TimeSpan.FromDays(retentionDays);

                                var indexList = await jsonClient.GetAsync<JObject>($"http://{manager.PrivateAddress}:{HiveHostPorts.ProxyPrivateHttpLogEsData}/_aliases");

                                foreach (var indexProperty in indexList.Properties())
                                {
                                    var indexName = indexProperty.Name;

                                    // We're only purging [logstash] and [metricbeat] indexes.

                                    if (!indexName.StartsWith("logstash-") && !indexName.StartsWith("metricbeat-"))
                                    {
                                        continue;
                                    }

                                    // Extract the date from the index name.

                                    var pos = indexName.LastIndexOf('-');

                                    if (pos == -1)
                                    {
                                        log.LogWarn(() => $"LOG-PURGER: Cannot extract date from index named [{indexName}].");
                                        continue;
                                    }

                                    var date      = indexName.Substring(pos + 1);
                                    var fields    = date.Split('.');
                                    var indexDate = default(DateTime);

                                    try
                                    {
                                        indexDate = new DateTime(int.Parse(fields[0]), int.Parse(fields[1]), int.Parse(fields[2]));
                                    }
                                    catch
                                    {
                                        log.LogWarn(() => $"LOG-PURGER: Cannot extract date from index named [{indexName}].");
                                        continue;
                                    }

                                    if (indexDate < deleteBeforeDate)
                                    {
                                        log.LogInfo(() => $"LOG-PURGER: Deleting index [{indexName}].");
                                        await jsonClient.DeleteAsync<JObject>($"http://{manager.PrivateAddress}:{HiveHostPorts.ProxyPrivateHttpLogEsData}/{indexName}");
                                        log.LogInfo(() => $"LOG-PURGER: [{indexName}] was deleted.");
                                    }
                                }

                                log.LogDebug("LOG-PURGER: Scan finished.");
                                return await Task.FromResult(false);
                            },
                        onExceptionAsync:
                            async e =>
                            {
                                log.LogError("LOG-PURGER", e);
                                return await Task.FromResult(false);
                            },
                        onTerminateAsync:
                            async () =>
                            {
                                log.LogInfo(() => "LOG-PURGER: Terminating");
                                await Task.CompletedTask;
                            });

                terminator.AddDisposable(periodicTask);
                await periodicTask.Run();
            }
        }
    }
}
