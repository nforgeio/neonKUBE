//-----------------------------------------------------------------------------
// FILE:	    Program.LogPurger.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE LLC.  All rights reserved.

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

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Net;
using Neon.Service;
using Neon.Tasks;

using ICSharpCode.SharpZipLib.Zip;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using k8s;

namespace NeonClusterManager
{
    public partial class NeonClusterManager : NeonService
    {
        /// <summary>
        /// Handles purging of old <b>logstash</b> and <b>metricbeat</b> Elasticsearch indexes.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task LogPurgerAsync(TimeSpan logPurgerInterval, int retentionDays)
        {
            using (var jsonClient = new JsonClient())
            {
                jsonClient.BaseAddress = KubernetesClientConfiguration.IsInCluster()
                    ? this.ServiceMap[NeonServices.Elasticsearch].Endpoints.Default.Uri 
                    : new Uri($"http://localhost:{this.ServiceMap[NeonServices.Elasticsearch].Endpoints.Default.Port}");

                var periodicTask =
                    new AsyncPeriodicTask(
                        logPurgerInterval,
                        onTaskAsync:
                            async () =>
                            {
                                // We're going to list the indexes and look for [logstash]
                                // and [metricbeat] indexes that encode the index date like:
                                //
                                //      logstash-2018.06.06
                                //      metricbeat-6.1.1-2018.06.06
                                //
                                // The date is simply encodes the day covered by the index.

                                var utcNow = DateTime.UtcNow;
                                var deleteBeforeDate = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day) - TimeSpan.FromDays(retentionDays);

                                var indexList = await jsonClient.GetAsync<JObject>("_aliases");

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
                                        Log.LogWarn(() => $"LOG-PURGER: Cannot extract date from index named [{indexName}].");
                                        continue;
                                    }

                                    var date = indexName.Substring(pos + 1);
                                    var fields = date.Split('.');
                                    var indexDate = default(DateTime);

                                    try
                                    {
                                        indexDate = new DateTime(int.Parse(fields[0]), int.Parse(fields[1]), int.Parse(fields[2]));
                                    }
                                    catch
                                    {
                                        Log.LogWarn(() => $"LOG-PURGER: Cannot extract date from index named [{indexName}].");
                                        continue;
                                    }

                                    if (indexDate < deleteBeforeDate)
                                    {
                                        Log.LogInfo(() => $"LOG-PURGER: Deleting index [{indexName}].");
                                        await jsonClient.DeleteAsync<JObject>(indexName);
                                        Log.LogInfo(() => $"LOG-PURGER: [{indexName}] was deleted.");
                                    }
                                }

                                Log.LogDebug("LOG-PURGER: Scan finished.");
                                return await Task.FromResult(false);
                            },
                        onExceptionAsync:
                            async e =>
                            {
                                Log.LogError("LOG-PURGER", e);
                                return await Task.FromResult(false);
                            },
                        onTerminateAsync:
                            async () =>
                            {
                                Log.LogInfo(() => "LOG-PURGER: Terminating");
                                await Task.CompletedTask;
                            });

                await periodicTask.Run();
            }
        }
    }
}
