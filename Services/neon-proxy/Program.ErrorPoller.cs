//-----------------------------------------------------------------------------
// FILE:	    Program.WarningPoller.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Docker;
using Neon.Hive;
using Neon.HiveMQ;
using Neon.Tasks;
using Neon.Time;

namespace NeonProxy
{
    public static partial class Program
    {
        /// <summary>
        /// Handles the periodic logging of error messages when <see cref="errorTimeUtc"/> is set
        /// to something greater than <see cref="DateTime.MinValue"/>, indicating that the service
        /// has been unable to update the HAProxy configuration and is currently running with 
        /// out-of-date settings.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task ErrorPollerAsync()
        {
            var periodicTask =
                new AsyncPeriodicTask(
                    warnInterval,
                    onTaskAsync:
                        async () =>
                        {
                            if (errorTimeUtc > DateTime.MinValue)
                            {
                                log.LogError(() => $"HAProxy is running with an out-of-date configuration due to a previous error at [{errorTimeUtc}] UTC.");
                            }

                            return await Task.FromResult(false);
                        },
                    onExceptionAsync:
                        async e =>
                        {
                            log.LogError("ERROR-POLLER", e);
                            return await Task.FromResult(false);
                        },
                    onTerminateAsync:
                        async () =>
                        {
                            log.LogInfo(() => "ERROR-POLLER: Terminating");
                            await Task.CompletedTask;
                        });

            terminator.AddDisposable(periodicTask);
            await periodicTask.Run();
        }
    }
}
