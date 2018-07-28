//-----------------------------------------------------------------------------
// FILE:	    HealthResolver.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
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
using Neon.DnsTools;
using Neon.Hive;
using Neon.Net;

namespace NeonDnsMon
{
    /// <summary>
    /// Performs DNS name resolutions and health endpoint checks, caching results
    /// for better performance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It will be very common for hives to have DNS host entries that reference
    /// the same DNS names, hive groups, and ultimate endpoint servers.  Rather
    /// than repeat the DNS lookups and pings for the same endpoints, we're going
    /// to do this only once per health check pass.
    /// </para>
    /// <para>
    /// This class will be used to hold any of this state throughout the health checks.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public class HealthResolver
    {
        private object                                  syncRoot = new object();
        private NeonDnsClient                           dns;
        private Pinger                                  pinger;
        private Dictionary<string, List<IPAddress>>     hostToAddresses;
        private Dictionary<string, bool>                addressToHealth;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="nameservers">Array of nameserver IP addresses.</param>
        public HealthResolver(string[] nameservers)
        {
            dns             = NeonDnsClient.CreateWithCaching(nameservers);
            pinger          = new Pinger();
            hostToAddresses = new Dictionary<string, List<IPAddress>>(StringComparer.InvariantCultureIgnoreCase);
            addressToHealth = new Dictionary<string, bool>();

        }

        /// <summary>
        /// Clears the resolver state to prepare for the next health check pass.
        /// </summary>
        public void Clear()
        {
            lock (syncRoot)
            {
                hostToAddresses.Clear();
                addressToHealth.Clear();
            }
        }

        /// <summary>
        /// Resolves a hostname into the list of assigned IP addresses.
        /// </summary>
        /// <param name="hostname">The hostname.</param>
        /// <returns>
        /// The IP address list or an empty list if the hostname did not
        /// resolve to anything.
        /// </returns>
        public async Task<List<IPAddress>> LookupAsync(string hostname)
        {
            List<IPAddress> addresses;

            // Try the cache first,

            lock (syncRoot)
            {
                if (hostToAddresses.TryGetValue(hostname, out addresses))
                {
                    return addresses;
                }
            }

            // Perform an actual DNS resolution and cache the results.

            addresses = (await dns.LookupAsync(hostname)).ToList();

            lock (syncRoot)
            {
                hostToAddresses[hostname] = addresses;
            }

            return addresses;
        }

        /// <summary>
        /// Verifies that the endpoint at an IP address is healthy by sending it
        /// a ping, caching the response.
        /// </summary>
        /// <param name="address">The endpoint IP address.</param>
        /// <param name="pingTimeout">Maximum time to wait for a ping reply.</param>
        /// <returns><c>true</c> if the endpoint is healthy.</returns>
        public async Task<bool> SendPingAsync(IPAddress address, TimeSpan pingTimeout)
        {
            var addressString = address.ToString();
            var health        = false;

            // Try the cache first.

            lock (syncRoot)
            {
                if (addressToHealth.TryGetValue(addressString, out health))
                {
                    return health;
                }
            }

            // Perform an actual ping and cache the result.

            var reply = await pinger.SendPingAsync(address, (int)pingTimeout.TotalMilliseconds);

            health = reply.Status == IPStatus.Success;

            lock (syncRoot)
            {
                addressToHealth[addressString] = health;
            }

            return health;
        }
    }
}
