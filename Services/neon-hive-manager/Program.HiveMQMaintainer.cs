//-----------------------------------------------------------------------------
// FILE:	    Program.HiveMQMaintainer.cs
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
        /// Periodically performs HiveMQ related maintenance activities such as ensuring
        /// that the [sysadmin] account has full permissions for all virtual hosts.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task HiveMQMaintainerAsync()
        {
            using (var hivemqManager = hive.HiveMQ.ConnectHiveMQManager(useBootstrap: true))
            {
                var periodicTask = 
                    new AsyncPeriodicTask(
                        hivemqMantainInterval,
                        onTaskAsync:
                            async () =>
                            {
                                log.LogDebug(() => $"HIVEMQ-MAINTAINER: Checking [{HiveConst.HiveMQSysadminUser}] permissions.");

                                // Build the set of virtual host names where [sysadmin] already has
                                // full permissions.

                                var sysadminVHosts = new HashSet<string>();

                                foreach (var permissions in await hivemqManager.GetPermissionsAsync())
                                {
                                    if (permissions.User == HiveConst.HiveMQSysadminUser &&
                                        permissions.Configure == ".*" &&
                                        permissions.Read == ".*" &&
                                        permissions.Write == ".*")
                                    {
                                        sysadminVHosts.Add(permissions.Vhost);
                                    }
                                }

                                // List the vhosts and set full permissions for [sysadmin] for any
                                // virtual hosts where [sysadmin] doesn't already have full permissions.

                                var sysadminUser          = await hivemqManager.GetUserAsync(HiveConst.HiveMQSysadminUser);
                                var addedVHostPermissions = new List<string>();

                                foreach (var vhost in await hivemqManager.GetVHostsAsync())
                                {
                                    if (!sysadminVHosts.Contains(vhost.Name))
                                    {
                                        addedVHostPermissions.Add(vhost.Name);
                                        await hivemqManager.CreatePermissionAsync(new PermissionInfo(sysadminUser, vhost));
                                    }
                                }

                                if (addedVHostPermissions.Count > 0)
                                {
                                    var sbVHostList = new StringBuilder();

                                    foreach (var vhost in addedVHostPermissions)
                                    {
                                        sbVHostList.AppendWithSeparator(vhost, ", ");
                                    }

                                    log.LogInfo(() => $"HIVEMQ-MAINTAINER: Granted [{HiveConst.HiveMQSysadminUser}] full permissions for vhosts: {sbVHostList}");
                                }

                                log.LogDebug(() => $"HIVEMQ-MAINTAINER: Check completed.");
                                return await Task.FromResult(false);
                            },
                        onExceptionAsync:
                            async e =>
                            {
                                log.LogError("HIVEMQ-MAINTAINER", e);
                                return await Task.FromResult(false);
                            },
                        onTerminateAsync:
                            async () =>
                            {
                                log.LogInfo(() => "HIVEMQ-MAINTAINER: Terminating");
                                await Task.CompletedTask;
                            });

                terminator.AddDisposable(periodicTask);
                await periodicTask.Run();
            }
        }
    }
}
