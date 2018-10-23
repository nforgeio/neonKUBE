//-----------------------------------------------------------------------------
// FILE:	    Program.FailsafeBroadcasterAsync.cs
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
using Neon.Time;
using Neon.Tasks;

namespace NeonProxyManager
{
    public static partial class Program
    {
        /// <summary>
        /// Periodically broadcasts failsafe <see cref="ProxyUpdateMessage"/> messages commanding
        /// the proxy and proxy bridge instances to ensure that they're running with the current
        /// HAProxy and Varnish configurations.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task FailsafeBroadcasterAsync()
        {
            var periodicTask =
                new AsyncPeriodicTask(
                    failsafeInterval,
                    onTaskAsync:
                        async () =>
                        {
                            var message = new ProxyUpdateMessage(all: true)
                            {
                                Reason = "fail-safe"
                            };

                            log.LogInfo(() => $"FAILSAFE-BROADCASTER: Broadcasting [{message}].");

                            await proxyNotifyChannel.PublishAsync(message);

                            return false;
                        },
                    onExceptionAsync:
                        async e =>
                        {
                            log.LogError("FAILSAFE-BROADCASTER", e);
                            return await Task.FromResult(false);
                        },
                    onTerminateAsync:
                        async () =>
                        {
                            log.LogInfo(() => "FAILSAFE-BROADCASTER: Terminating");
                            await Task.CompletedTask;
                        },
                    cancellationTokenSource: terminator.CancellationTokenSource);

            terminator.AddDisposable(periodicTask);
            await periodicTask.Run();
        }
    }
}
