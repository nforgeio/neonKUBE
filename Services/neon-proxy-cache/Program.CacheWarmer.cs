//-----------------------------------------------------------------------------
// FILE:	    Program.CacheWarmer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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
using Neon.IO;
using Neon.Tasks;
using Neon.Time;

namespace NeonProxyCache
{
    public static partial class Program
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used to keep track of the warming status for a cache warming target.
        /// </summary>
        private class WarmTargetStatus
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="target">The associated target.</param>
            public WarmTargetStatus(TrafficDirectorWarmTarget target)
            {
                Covenant.Requires<ArgumentNullException>(target != null);

                this.FetchTimeUtc = DateTime.UtcNow;
            }

            /// <summary>
            /// Returns the cache warming target.
            /// </summary>
            public TrafficDirectorWarmTarget Target { get; private set; }

            /// <summary>
            /// Returns the next scheduled time (UTC) when the target should be fetched.
            /// </summary>
            public DateTime FetchTimeUtc { get; private set; }

            /// <summary>
            /// Schedules the next fetch.
            /// </summary>
            public void Reschedule()
            {
                FetchTimeUtc = DateTime.UtcNow + TimeSpan.FromSeconds(Target.UpdateSeconds);
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private static List<WarmTargetStatus>   warmTargets = new List<WarmTargetStatus>();
        private static CancellationTokenSource  warmCts     = new CancellationTokenSource();

        /// <summary>
        /// Implements the cache warmer that proactively loads content into the cache.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async static Task CacheWarmer()
        {
            using (var client = new HttpClient())
            {
                while (!terminator.CancellationToken.IsCancellationRequested)
                {
                    var targets  = warmTargets;
                    var fetchCts = warmCts;

                    if (targets != null && targets.Count > 0)
                    {
                        // We're going to submit GET requests in parallel for all warming targets that
                        // are currently scheduled to be fetched.  [warmCts] will be cancelled whenever
                        // the VarnishShim fetches a new proxy configuration with new warm targets.

                        var fetchTasks = new List<Task>();
                        var utcNow     = DateTime.UtcNow;

                        foreach (var target in targets.Where(t => t.FetchTimeUtc <= utcNow))
                        {
                            fetchTasks.Add(Task.Run(
                                async () =>
                                {
                                    try
                                    {
                                        // Varnish is listening on [*:80] so we're going to send the request
                                        // to the loopback address on this port, setting the [Host] header
                                        // to the HOST from the target URI, the port to [80].
                                        //
                                        // We'll also set the [User-Agent] and [X-Neon-Frontend] headers.

                                        var targetUri = new Uri(target.Target.Uri);
                                        var request   = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:80{targetUri.PathAndQuery}");

                                        request.Headers.Host = targetUri.Host;
                                        request.Headers.Add("User-Agent", target.Target.UserAgent);
                                        request.Headers.Add("X-Neon-Frontend", target.Target.FrontendHeader);

                                        await client.SendAsync(request, fetchCts.Token);
                                    }
                                    catch
                                    {
                                        // Intentionally ignoring all exceptions.
                                    }
                                    finally
                                    {
                                        target.Reschedule();
                                    }
                                }));
                        }

                        try
                        {
                            await NeonHelper.WaitAllAsync(fetchTasks);
                        }
                        catch (TaskCanceledException)
                        {
                            // Just loop to pick up any new targets.

                            continue;
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1), terminator.CancellationToken);
                }
            }
        }

        /// <summary>
        /// Called when the <see cref="VarnishShim"/> related code has downloaded and configured 
        /// a proxy configuration update.  The cache warmer will begin pulling any new content.
        /// </summary>
        /// <param name="cacheSettings">The new cache settings.</param>
        private static void UpdateCacheSettings(TrafficDirectorCacheSettings cacheSettings)
        {
            Covenant.Requires<ArgumentNullException>(cacheSettings != null);

            var newTargets = new List<WarmTargetStatus>();

            foreach (var target in cacheSettings.WarmTargets)
            {
                newTargets.Add(new WarmTargetStatus(target));
            }

            warmTargets = newTargets;
            warmCts.Cancel();
        }
    }
}
