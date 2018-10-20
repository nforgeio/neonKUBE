//-----------------------------------------------------------------------------
// FILE:	    Program.SecretPurger.cs
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
        /// Handles purging of old <b>neon-secret-retriever-*</b> service instances as well
        /// as any persisted secrets.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task SecretPurgerAsync()
        {
            var periodicTask =
                new AsyncPeriodicTask(
                    secretPurgeInterval,
                    onTaskAsync:
                        async () =>
                        {
                            if (IsSetupPending)
                            {
                                log.LogInfo(() => "SECRET-PURGER: Delaying because hive setup is still in progress.");
                                return false;
                            }

                            // Commpute the minimum creation time for the retriever service and
                            // the retrieved Consul key.  We're hardcoding the maximum age to
                            // 30 minutes.

                            var utcNow        = DateTime.UtcNow;
                            var minCreateTime = utcNow - TimeSpan.FromMinutes(30);

                            // Scan for and remove old [neon-service-retriever] services.

                            log.LogDebug(() => "SECRET-PURGER: Scanning for old [neon-secret-retriver] services ready for removal.");

                            var retrieverServices = hive.Docker.ListServices()
                                .Where(l => l.StartsWith("neon-secret-retriever-"))
                                .ToList();

                            if (retrieverServices.Count > 0)
                            {
                                log.LogInfo($"SECRET-PURGER: Discovered [{retrieverServices.Count}] services named like [neon-secret-retriever-*].");

                                foreach (var service in retrieverServices)
                                {
                                    // Inspect the service to obtain its creation date.

                                    var serviceDetails = hive.Docker.InspectService(service);

                                    if (serviceDetails.CreatedAtUtc < minCreateTime)
                                    {
                                        log.LogInfo($"Removing service [service].");

                                        var response = hive.GetReachableManager().SudoCommand($"docker service rm {service}");

                                        if (response.ExitCode != 0)
                                        {
                                            throw new HiveException(response.ErrorSummary);
                                        }
                                    }
                                }
                            }

                            // Scan for and remove old retrieved secrets persisted as Consul
                            // keys under [neon/service/neon-secret-retriever].

                            log.LogDebug(() => "SECRET-PURGER: Scanning for old [neon-secret-retriver] secrets persisted to Consul.");

                            var secretKeyPaths = consul.KV.ListKeys("neon/service/neon-secret-retriever").Result
                                .Where(k => k.Contains('~'))    // Secret keys use "~" to separate the timestamp and GUID
                                .ToList();

                            if (secretKeyPaths.Count > 0)
                            {
                                log.LogInfo($"SECRET-PURGER: Discovered [{secretKeyPaths.Count}] keys under [neon/service/neon-secret-retriever].");

                                foreach (var keyPath in secretKeyPaths)
                                {
                                    // Strip off the leading path to leave only the key.

                                    var key          = keyPath;
                                    var lastSlashPos = key.LastIndexOf('/');

                                    if (lastSlashPos == -1)
                                    {
                                        continue;
                                    }

                                    key = key.Substring(lastSlashPos + 1);

                                    // Split the key on the '~' character and parse the first
                                    // field as the timestamp.  We're going to ignore keys that
                                    // can't be parsed for resilience.
                                    //
                                    // NOTE: 
                                    //
                                    // The timestamp replaced colon (:) characters with underscore (_) to
                                    // prevent Consul from escaping these so they'll be easier to read.
                                    // We need to reverse this before parsing the timestamp.

                                    var timestampPart = key.Split('~')[0];

                                    timestampPart = timestampPart.Replace('_', ':');

                                    if (DateTime.TryParseExact(timestampPart, NeonHelper.DateFormatTZ, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp) &&
                                        timestamp < minCreateTime)
                                    {
                                        log.LogInfo($"SECRET-PURGER: Removing Consul key [{keyPath}].");
                                        consul.KV.Delete(keyPath).Wait();
                                    }
                                }
                            }

                            log.LogDebug(() => "SECRET-PURGER: Scan finished.");
                            return await Task.FromResult(false);
                        },
                    onExceptionAsync:
                        async e =>
                        {
                            log.LogError("SECRET-PURGER", e);
                            return await Task.FromResult(false);
                        },
                    onTerminateAsync:
                        async () =>
                        {
                            log.LogInfo(() => "SECRET-PURGER: Terminating");
                            await Task.CompletedTask;
                        });

            terminator.AddDisposable(periodicTask);
            await periodicTask.Run();
        }
    }
}
