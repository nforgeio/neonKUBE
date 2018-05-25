//-----------------------------------------------------------------------------
// FILE:	    HostsManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Cluster
{
    /// <summary>
    /// Handles local cluster DNS hosts krelated operations for a <see cref="ClusterProxy"/>.
    /// </summary>
    public sealed class HostsManager
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Determines whether a DNS entry name is valid.
        /// </summary>
        /// <param name="name">The name being validated.</param>
        /// <returns><c>true</c> if the name is valid.</returns>
        [Pure]
        public static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return ClusterDefinition.DnsHostRegex.IsMatch(name);
        }

        //---------------------------------------------------------------------
        // Instance members

        private ClusterProxy cluster;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="cluster">The parent <see cref="ClusterProxy"/>.</param>
        internal HostsManager(ClusterProxy cluster)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            this.cluster = cluster;
        }

        /// <summary>
        /// Returns a named cluster DNS host entry.
        /// </summary>
        /// <param name="hostname">The DNS hostname (case insenstive).</param>
        /// <returns>The <see cref="DnsEntry"/> or <c>null</c> if it doesn't exist.</returns>
        public DnsEntry Get(string hostname)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hostname));
            Covenant.Requires<ArgumentException>(IsValidName(hostname));

            hostname = hostname.ToLowerInvariant();

            return cluster.Consul.KV.GetObjectOrDefault<DnsEntry>($"{NeonClusterConst.ConsulDnsEntriesKey}/{hostname}").Result;
        }

        /// <summary>
        /// Lists the cluster DNS host entries.
        /// </summary>
        /// <param name="includeSystem">Optionally include built-in system entries.</param>
        /// <returns>The list of name/entry values.</returns>
        public List<DnsEntry> List(bool includeSystem = false)
        {
            var list = new List<DnsEntry>();

            foreach (var key in cluster.Consul.KV.ListKeys(NeonClusterConst.ConsulDnsEntriesKey, ConsulListMode.PartialKey).Result)
            {
                var entry = cluster.Consul.KV.GetObjectOrDefault<DnsEntry>($"{NeonClusterConst.ConsulDnsEntriesKey}/{key}").Result;

                if (entry == null)
                {
                    return null;    // It's possible for the key to have been removed since it was listed.
                }

                if (!entry.IsSystem || includeSystem)
                {
                    list.Add(entry);
                }
            }

            return list;
        }

        /// <summary>
        /// Removes a cluster DNS host entry.
        /// </summary>
        /// <param name="hostname">The DNS hostname (case insenstive).</param>
        public void Remove(string hostname)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hostname));
            Covenant.Requires<ArgumentException>(IsValidName(hostname));

            hostname = hostname.ToLowerInvariant();

            cluster.Consul.KV.Delete($"{NeonClusterConst.ConsulDnsEntriesKey}/{hostname}").Wait();
        }

        /// <summary>
        /// Sets a cluster DNS host entry.
        /// </summary>
        /// <param name="entry">The entry definition.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if an existing entry already exists and its <see cref="DnsEntry.IsSystem"/>
        /// value doesn't match that for the new entry.
        /// </exception>
        /// <remarks>
        /// <note>
        /// <para>
        /// This method will not allow a SYSTEM entry to overwrite a non-SYSTEM entry
        /// or vice versa.  This helps prevent accidentially impacting important cluster 
        /// services (like the local registry).
        /// </para>
        /// <para>
        /// If you really need to do this, you can remove the existing entry first.
        /// </para>
        /// </note>
        /// </remarks>
        public void Set(DnsEntry entry)
        {
            Covenant.Requires<ArgumentNullException>(entry != null);
            Covenant.Requires<ArgumentException>(IsValidName(entry.Hostname));

            var existing = Get(entry.Hostname);

            if (existing != null && existing.IsSystem != entry.IsSystem)
            {
                if (existing.IsSystem)
                {
                    throw new InvalidOperationException($"Cannot overwrite existing SYSTEM DNS entry [{entry.Hostname}] with a non-SYSTEM entry.");
                }
                else
                {
                    throw new InvalidOperationException($"Cannot overwrite existing non-SYSTEM DNS entry [{entry.Hostname}] with a SYSTEM entry.");
                }
            }

            var hostname = entry.Hostname.ToLowerInvariant();

            cluster.Consul.KV.PutObject($"{NeonClusterConst.ConsulDnsEntriesKey}/{hostname}", entry).Wait();
        }
    }
}
