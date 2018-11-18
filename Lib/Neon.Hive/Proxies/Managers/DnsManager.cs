//-----------------------------------------------------------------------------
// FILE:	    DnsManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
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

namespace Neon.Hive
{
    /// <summary>
    /// Handles local hive DNS related operations for a <see cref="HiveProxy"/>.
    /// </summary>
    public sealed class DnsManager
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

            return HiveDefinition.DnsHostRegex.IsMatch(name);
        }

        //---------------------------------------------------------------------
        // Instance members

        private HiveProxy hive;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="hive">The parent <see cref="HiveProxy"/>.</param>
        internal DnsManager(HiveProxy hive)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            this.hive = hive;
        }

        /// <summary>
        /// Returns a named hive DNS host entry.
        /// </summary>
        /// <param name="hostname">The DNS hostname (case insenstive).</param>
        /// <returns>The <see cref="DnsEntry"/> or <c>null</c> if it doesn't exist.</returns>
        public DnsEntry Get(string hostname)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hostname));
            Covenant.Requires<ArgumentException>(IsValidName(hostname));

            hostname = hostname.ToLowerInvariant();

            return hive.Consul.Client.KV.GetObjectOrDefault<DnsEntry>($"{HiveConst.ConsulDnsEntriesKey}/{hostname}").Result;
        }

        /// <summary>
        /// Lists the hive DNS host entries.
        /// </summary>
        /// <param name="includeSystem">Optionally include built-in system entries.</param>
        /// <returns>The list of name/entry values.</returns>
        public List<DnsEntry> List(bool includeSystem = false)
        {
            var list = new List<DnsEntry>();

            foreach (var key in hive.Consul.Client.KV.ListKeys(HiveConst.ConsulDnsEntriesKey, ConsulListMode.PartialKey).Result)
            {
                var entry = hive.Consul.Client.KV.GetObjectOrDefault<DnsEntry>($"{HiveConst.ConsulDnsEntriesKey}/{key}").Result;

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
        /// Proactively command the hive managers to reload the local DNS host definitions
        /// and optionally have all hive nodes wipe all cached DNS records.
        /// </summary>
        /// <param name="wipe">Optionally command all nodes to wipe all cached DNS records.</param>
        public void Reload(bool wipe = false)
        {
            // $hack(jeff.lill): 
            //
            // We're going to wait 15 seconds to give the following services a
            // chance to process any pending DNS host changes written to Consul.  This 
            // is hardcoded, assuming 5 second polling intervals and also giving the
            // [neon-dns-mon] service a chance to perform health checks.
            //
            //      neon-dns-mon:       5 second poll
            //                         +5 seconds for health checks
            //      neon-dns            5 second poll
            //                       -------------------
            //                         15 seconds

            Thread.Sleep(TimeSpan.FromSeconds(15));

            // Handle the managers first.

            var actions = new List<Action>();

            foreach (var manager in hive.Managers)
            {
                actions.Add(
                    () =>
                    {
                        using (var sshClient = manager.CloneSshClient())
                        {
                            if (wipe)
                            {
                                sshClient.RunCommand($"sudo {HiveHostFolders.Bin}/neon-dns-reload wipe");
                            }
                            else
                            {
                                sshClient.RunCommand($"sudo {HiveHostFolders.Bin}/neon-dns-reload");
                            }
                        }
                    });
            }

            NeonHelper.WaitForParallel(actions);

            // Wipe the non-manager nodes if requested.

            if (wipe)
            {
                actions.Clear();

                foreach (var node in hive.Nodes.Where(n => n.Metadata.Role != NodeRole.Manager))
                {
                    actions.Add(
                        () =>
                        {
                            using (var sshClient = node.CloneSshClient())
                            {
                                sshClient.RunCommand($"sudo {HiveHostFolders.Bin}/neon-dns-reload wipe");
                            }
                        });
                }

                NeonHelper.WaitForParallel(actions);
            }
        }

        /// <summary>
        /// Removes a hive DNS host entry.
        /// </summary>
        /// <param name="hostname">The DNS hostname (case insenstive).</param>
        /// <param name="waitUntilPropagated">
        /// Optionally signals hive nodes to wipe their DNS cache and reload local hosts
        /// so the changes will be proactively  propagated across the hive.  This defaults
        /// to <c>false</c>.
        /// </param>
        public void Remove(string hostname, bool waitUntilPropagated = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hostname));
            Covenant.Requires<ArgumentException>(IsValidName(hostname));

            hostname = hostname.ToLowerInvariant();

            hive.Consul.Client.KV.Delete($"{HiveConst.ConsulDnsEntriesKey}/{hostname}").Wait();

            if (waitUntilPropagated)
            {
                Reload(wipe: true);
            }

            SetChangeTime();
        }

        /// <summary>
        /// Sets a hive DNS host entry.
        /// </summary>
        /// <param name="entry">The entry definition.</param>
        /// <param name="waitUntilPropagated">
        /// Optionally signals hive nodes to wipe their DNS cache and reload local hosts
        /// so the changes will be proactively  propagated across the hive.  This defaults
        /// to <c>false</c>.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if an existing entry already exists and its <see cref="DnsEntry.IsSystem"/>
        /// value doesn't match that for the new entry.
        /// </exception>
        /// <remarks>
        /// <note>
        /// <para>
        /// This method will not allow a SYSTEM entry to overwrite a non-SYSTEM entry
        /// or vice versa.  This helps prevent accidentially impacting important hive 
        /// services (like the local registry).
        /// </para>
        /// <para>
        /// If you really need to do this, you can remove the existing entry first.
        /// </para>
        /// </note>
        /// </remarks>
        public void Set(DnsEntry entry, bool waitUntilPropagated = false)
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

            hive.Consul.Client.KV.PutObject($"{HiveConst.ConsulDnsEntriesKey}/{hostname}", entry).Wait();

            if (waitUntilPropagated)
            {
                Reload(wipe: true);
            }

            SetChangeTime();
        }

        /// <summary>
        /// Returns the current DNS host/answers as a dictionary.
        /// </summary>
        /// <returns>The answers dictionary.</returns>
        public Dictionary<string, List<string>> GetAnswers()
        {
            var hosts   = hive.Consul.Client.KV.GetStringOrDefault(HiveConst.ConsulDnsHostsKey).Result;
            var answers = new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);

            if (hosts == null)
            {
                return answers;
            }

            var unhealthyPrefix = "# unhealthy:";

            using (var reader = new StringReader(hosts))
            {
                foreach (var line in reader.Lines())
                {
                    if (line.StartsWith(unhealthyPrefix))
                    {
                        // Comment lines formatted like:
                        //
                        //      # unhealthy: HOSTNAME
                        //
                        // Have no health endpoints.  We're going to add an empty
                        // list for these and highlight these below.

                        var host = line.Substring(unhealthyPrefix.Length).Trim();

                        if (!answers.TryGetValue(host, out var addresses))
                        {
                            addresses = new List<string>();
                            answers.Add(host, addresses);
                        }
                    }
                    else if (line.StartsWith("#"))
                    {
                        // Ignore other comment lines.
                    }
                    else
                    {
                        var fields = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        if (fields.Length != 2)
                        {
                            continue;
                        }

                        var address = fields[0];
                        var host    = fields[1];

                        if (!answers.TryGetValue(host, out var addresses))
                        {
                            addresses = new List<string>();
                            answers.Add(host, addresses);
                        }

                        addresses.Add(address);
                    }
                }
            }

            return answers;
        }

        /// <summary>
        /// Sets the DNS <b>change-time-utc</b> Consul key to the current hive time.
        /// </summary>
        private void SetChangeTime()
        {
            var timeUtc = hive.GetTimeUtc();

            hive.Consul.Client.KV.PutString($"{HiveConst.ConsulDnsRootKey}/change-time-utc", timeUtc.ToString(NeonHelper.DateFormatTZ)).Wait();
        }

        /// <summary>
        /// Returns the time (UTC) when the hive DNS settings were last modified.
        /// </summary>
        /// <returns>The <see cref="DateTime"/> (UTC).</returns>
        public DateTime GetChangeTime()
        {
            var changeTimeUtcString = hive.Consul.Client.KV.GetStringOrDefault($"{HiveConst.ConsulDnsRootKey}/change-time-utc").Result;

            if (changeTimeUtcString != null && DateTime.TryParseExact(changeTimeUtcString, NeonHelper.DateFormatTZ, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var changeTimeUtc))
            {
                return changeTimeUtc;
            }
            else
            {
                // Return the current hive time when there is no [change-time-utc] Consul key or it can't be parsed
                // and also set the change time to the current time.

                SetChangeTime();

                return hive.GetTimeUtc();
            }
        }
    }
}
