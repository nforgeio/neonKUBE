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
            public WarmTargetStatus(TrafficWarmTarget target)
            {
                Covenant.Requires<ArgumentNullException>(target != null);

                this.Target           = target;
                this.LastFetchTimeUtc = DateTime.MinValue;
                this.NextFetchTimeUtc = DateTime.UtcNow;
            }

            /// <summary>
            /// Returns the cache warming target.
            /// </summary>
            public TrafficWarmTarget Target { get; private set; }

            /// <summary>
            /// The last time (UTC) this target was fetched or <see cref="DateTime.MinValue"/>
            /// if it hasn't been fetched yet.
            /// </summary>
            public DateTime LastFetchTimeUtc { get; set; }

            /// <summary>
            /// The next scheduled time (UTC) when the target should be fetched.
            /// </summary>
            public DateTime NextFetchTimeUtc { get; set; }

            /// <summary>
            /// Schedules the next fetch.
            /// </summary>
            public void Reschedule()
            {
                NextFetchTimeUtc = DateTime.UtcNow + TimeSpan.FromSeconds(Target.UpdateSeconds);
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private static object                   warmLock    = new object();
        private static List<WarmTargetStatus>   warmTargets = new List<WarmTargetStatus>();
        private static CancellationTokenSource  warmCts     = new CancellationTokenSource();
        private static TimeSpan                 warmJitter  = TimeSpan.FromMilliseconds(100);

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
                    try
                    {
                        List<WarmTargetStatus>      targets;
                        CancellationTokenSource     fetchCts;

                        lock (warmLock)
                        {
                            targets  = warmTargets;
                            fetchCts = warmCts;
                        }

                        if (targets != null && targets.Count > 0)
                        {
                            // We're going to submit GET requests in parallel for all warming targets that
                            // are currently scheduled to be fetched.  [warmCts] will be cancelled whenever
                            // the VarnishShim fetches a new proxy configuration with new warm targets.

                            var fetchTasks = new List<Task>();
                            var utcNow     = DateTime.UtcNow;

                            foreach (var item in targets.Where(t => t.NextFetchTimeUtc <= utcNow))
                            {
                                fetchTasks.Add(Task.Run(
                                    async () =>
                                    {
                                        try
                                        {
                                            // Add some random jitter before making the request to avoid
                                            // slamming Varnish with a bunch of traffic at the same time.

                                            await Task.Delay(NeonHelper.RandTimespan(warmJitter));

                                            // Varnish is listening locally on [*:80] so we're going to send the 
                                            // request to the loopback address on this port, setting the [Host] 
                                            // header to the HOST from the target URI, the port to [80].
                                            //
                                            // We'll also set the [User-Agent] and [X-Neon-Frontend] headers.

                                            var targetUri = new Uri(item.Target.Uri);
                                            var request   = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:80{targetUri.PathAndQuery}");

                                            request.Headers.Host = targetUri.Host;
                                            request.Headers.Add("User-Agent", item.Target.UserAgent);
                                            request.Headers.Add("X-Neon-Frontend", item.Target.FrontendHeader);

                                            await client.SendAsync(request);

                                        }
                                        catch
                                        {
                                            // Intentionally ignoring all fetch related exceptions.
                                        }
                                        finally
                                        {
                                            item.Reschedule();
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
                    }
                    catch (Exception e)
                    {
                        log.LogError(e);
                    }

                    if (!terminator.CancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                }
            }
        }

        /// <summary>
        /// Called when the <see cref="VarnishShim"/> related code has downloaded and configured 
        /// a proxy configuration update.  The cache warmer will begin pulling any new content.
        /// </summary>
        /// <param name="cacheSettings">The new cache settings.</param>
        private static void UpdateCacheWarmer(TrafficCacheSettings cacheSettings)
        {
            Covenant.Requires<ArgumentNullException>(cacheSettings != null);

            //-----------------------------------------------------------------
            // Load the new targets, while attempting to retain the scheduling for
            // existing targets whose update interval hasn't changed.  We're trying 
            // to avoid the situation where we have an existing set of cache warm
            // targets that have just been pulled into the cache, say with an
            // update interval of 5 minutes.
            //
            // Then an unrelated rule is published that has other warm targets.
            // We don't want to immediately reschedule the original warm target.
            // We'd much prefer to have it pinged again on its original schedule.
            //
            // The code below handles this and also reschedules targets whose
            // fetch interval has changed.

            var newTargets = new List<WarmTargetStatus>();

            foreach (var target in cacheSettings.WarmTargets)
            {
                newTargets.Add(new WarmTargetStatus(target));
            }

            // Build a dictionary of the current targets keyed by URI.

            var curTargets = new Dictionary<string, WarmTargetStatus>();

            foreach (var item in warmTargets)
            {
                curTargets[item.Target.Uri] = item;
            }

            // Update the new targets to retain the fetch schedule where possible.

            foreach (var newItem in newTargets)
            {
                if (curTargets.TryGetValue(newItem.Target.Uri, out var curItem))
                {
                    // The new target updates an existing one.

                    if (curItem.LastFetchTimeUtc == DateTime.MinValue)
                    {
                        // The existing target has never been fetched, so we'll leave the schedule alone.

                        continue;
                    }

                    if (curItem.Target.UpdateSeconds != newItem.Target.UpdateSeconds)
                    {
                        // The update interval has changed so we'll explicitly schedule 
                        // the new target.

                        newItem.NextFetchTimeUtc = curItem.LastFetchTimeUtc + TimeSpan.FromSeconds(newItem.Target.UpdateSeconds);
                    }
                    else
                    {
                        // Retain the original scheduled time.

                        newItem.NextFetchTimeUtc = curItem.NextFetchTimeUtc;
                    }
                }
            }

            //-----------------------------------------------------------------
            // Signal the [CacheWarmer] method so that it will pick up the new targets.

            var orgWarmCts = warmCts;

            lock (warmLock)
            {
                warmTargets = newTargets;
                warmCts     = new CancellationTokenSource();
            }

            orgWarmCts.Cancel();
        }
    }
}
