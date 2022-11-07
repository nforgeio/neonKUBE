//-----------------------------------------------------------------------------
// FILE:	    NeonDnsClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using DNS.Client;

using Neon.Common;
using Neon.Kube;
using Neon.Tasks;
using Neon.Time;

namespace Neon.Net
{
    /// <summary>
    /// Extends <see cref="DnsClient"/> to support resolution against 
    /// multiple nameservers.
    /// </summary>
    public class NeonDnsClient : IDisposable
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used to cache a DNS response.
        /// </summary>
        private class Answer
        {
            /// <summary>
            /// The host addresses.
            /// </summary>
            public List<IPAddress> Addresses { get; set; }

            /// <summary>
            /// The scheduled time (SYS) for this cached entry to expire.
            /// </summary>
            public DateTime TTD { get; set; }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Creates a DNS client that caches responses.
        /// </summary>
        /// <param name="nameservers">The nameservers specified as <see cref="IPAddress"/> instances.</param>
        /// <returns>The <see cref="NeonDnsClient"/>.</returns>
        public static NeonDnsClient CreateWithCaching(params IPAddress[] nameservers)
        {
            var client = new NeonDnsClient(nameservers);

            client.EnableCaching();
            return client;
        }

        /// <summary>
        /// Creates a DNS client that caches responses.
        /// </summary>
        /// <param name="nameservers">The nameservers specified as IP address strings.</param>
        /// <returns>The <see cref="NeonDnsClient"/>.</returns>
        public static NeonDnsClient CreateWithCaching(params string[] nameservers)
        {
            var client = new NeonDnsClient(nameservers);

            client.EnableCaching();
            return client;
        }

        /// <summary>
        /// Creates a DNS client that <b>does not</b> cache responses.
        /// </summary>
        /// <param name="nameservers">The nameservers specified as <see cref="IPAddress"/> instances.</param>
        /// <returns>The <see cref="NeonDnsClient"/>.</returns>
        public static NeonDnsClient Create(params IPAddress[] nameservers)
        {
            return new NeonDnsClient(nameservers);
        }

        /// <summary>
        /// Creates a DNS client that <b>does not</b> cache responses.
        /// </summary>
        /// <param name="nameservers">The nameservers specified as IP address strings.</param>
        /// <returns>The <see cref="NeonDnsClient"/>.</returns>
        public static NeonDnsClient Create(params string[] nameservers)
        {
            return new NeonDnsClient(nameservers);
        }

        //---------------------------------------------------------------------
        // Instance members

        private DnsClient[]                 clients;
        private Dictionary<string, Answer>  cache;
        private GatedTimer                  cacheTimer;

        /// <summary>
        /// Constructs an instance to query one or more nameserver IP addresses.
        /// </summary>
        /// <param name="nameservers">The name server IP addresses (at least one must be passed).</param>
        private NeonDnsClient(params IPAddress[] nameservers)
        {
            Covenant.Requires<ArgumentException>(nameservers != null && nameservers.Length != 0, nameof(nameservers), "At least one nameserver is required.");

            clients = new DnsClient[nameservers.Length];

            for (int i = 0; i < nameservers.Length; i++)
            {
                clients[i] = new DnsClient(nameservers[i]);
            }
        }

        /// <summary>
        /// Constructs an instance to query one or more nameserver IP address strings.
        /// </summary>
        /// <param name="nameservers">The name server IP addresses (at least one must be passed).</param>
        private NeonDnsClient(params string[] nameservers)
        {
            Covenant.Requires<ArgumentException>(nameservers != null && nameservers.Length != 0, nameof(nameservers), "At least one nameserver is required.");

            clients = new DnsClient[nameservers.Length];

            for (int i = 0; i < nameservers.Length; i++)
            {
                clients[i] = new DnsClient(nameservers[i]);
            }
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~NeonDnsClient()
        {
            Dispose(false);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases any important resources associated with the instance.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if the instance is being disposed as opposed to being finalized.</param>
        protected void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (cache)
                {
                    if (cacheTimer != null)
                    {
                        cacheTimer.Dispose();
                        cacheTimer = null;
                    }
                }

                GC.SuppressFinalize(this);
            }

            cacheTimer = null;
        }

        /// <summary>
        /// Specifies that responses are to be cached until the TTL expires.
        /// </summary>
        private void EnableCaching()
        {
            cache      = new Dictionary<string, Answer>(StringComparer.InvariantCultureIgnoreCase);
            cacheTimer = new GatedTimer(PurgeExpired, null, TimeSpan.FromSeconds(60));
        }

        /// <summary>
        /// Removes any expired answers from the cache.
        /// </summary>
        /// <param name="state">Not used.</param>
        private void PurgeExpired(object state)
        {
            var sysNow = SysTime.Now;

            lock (cache)
            {
                var delList = new List<string>();

                foreach (var item in cache)
                {
                    if (item.Value.TTD <= sysNow)
                    {
                        delList.Add(item.Key);
                    }
                }

                foreach (var host in delList)
                {
                    cache.Remove(host);
                }
            }
        }

        /// <summary>
        /// Clears any cached cached answers.
        /// </summary>
        public void ClearCache()
        {
            if (cache == null)
            {
                return;
            }

            lock (cache)
            {
                cache.Clear();
            }
        }

        /// <summary>
        /// Attempts to resolve an IP address or fully qualified domain name
        /// into host IP addresses.
        /// </summary>
        /// <param name="addressOrFQDN">The IP address or FQDN.</param>
        /// <param name="noCache">
        /// Optionally specify that the method is not to answer from the cache, 
        /// even if the cache is enabled.
        /// </param>
        /// <returns>An empty result set if the lookup failed.</returns>
        public async Task<IEnumerable<IPAddress>> LookupAsync(string addressOrFQDN, bool noCache = false)
        {
            await SyncContext.Clear;

            // We can short-circuit things if the parameter is an IP address.

            if (NetHelper.TryParseIPv4Address(addressOrFQDN, out var address))
            {
                return new IPAddress[] { address };
            }

            // Try to answer from the cache, if enabled.

            if (cache != null && !noCache)
            {
                lock (cache)
                {
                    if (cache.TryGetValue(addressOrFQDN, out var answer))
                    {
                        if (answer.TTD > SysTime.Now)
                        {
                            // The answer hasn't expired yet, so we'll return it.

                            return answer.Addresses;
                        }
                    }
                }
            }

            // We're going to submit requests to all DNS clients in parallel and
            // return replies from the first one that answers.

            var readyEvent = new AsyncAutoResetEvent();
            var sync       = new object();
            var pending    = clients.Length;
            var results    = (IList<IPAddress>)null;

            foreach (var client in clients)
            {
                var task = Task.Run(
                    async () =>
                    {
                        try
                        {
                            var addresses = await client.Lookup(addressOrFQDN);

                            if (addresses.Count > 0)
                            {
                                lock (sync)
                                {
                                    if (results == null)
                                    {
                                        results = addresses;
                                    }
                                }
                            }

                            Interlocked.Decrement(ref pending);
                            readyEvent.Set();
                        }
                        catch (ResponseException)
                        {
                            // $todo(jefflill): 
                            //
                            // I wish the underlying [DnsClient] didn't throw exceptions.  Perhaps
                            // I could extend the implementation to implement [TryResolve()].

                            Interlocked.Decrement(ref pending);
                            readyEvent.Set();
                        }
                    });
            }

            while (pending > 0)
            {
                await readyEvent.WaitAsync();

                if (results != null)
                {
                    return results;
                }
            }

            return null;
        }
    }
}
