//-----------------------------------------------------------------------------
// FILE:	    Program.ProxyUpdater.cs
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
        /// Periodically broadcasts a <see cref="ProxyRegenerateMessage"/> to the <b>neon-proxy-manager</b>
        /// service which will then regenerate the public and private proxy related configurations.  This
        /// is a fail-safe that ensures that the proxy configurations will eventually converge, even when
        /// proxy change notifications may have been lost somehow.  This also provides an opportunity for
        /// <b>neon-proxy-manager</b> to verify the traffic manager rules for correctness and also to check
        /// for expired or expiring TLS certificates so that warnings can be logged.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task ProxyUpdaterAsync()
        {
            var periodicTask =
                new AsyncPeriodicTask(
                    proxyUpdateInterval,
                    onTaskAsync:
                        async () =>
                        {
                            log.LogInfo(() => $"PROXY-UPDATER: Publish: [{nameof(ProxyRegenerateMessage)}(\"fail-safe\") --> {proxyNotifyChannel.Name}]");
                            proxyNotifyChannel.Publish(new ProxyRegenerateMessage() { Reason = "[neon-hive-manager]: fail-safe" });

                            return await Task.FromResult(false);
                        },
                    onExceptionAsync:
                        async e =>
                        {
                            log.LogError("PROXY-UPDATER", e);
                            return await Task.FromResult(false);
                        },
                    onTerminateAsync:
                        async () =>
                        {
                            log.LogInfo(() => "PROXY-UPDATER: Terminating");
                            await Task.CompletedTask;
                        });

            terminator.AddDisposable(periodicTask);
            await periodicTask.Run();
        }
    }
}
