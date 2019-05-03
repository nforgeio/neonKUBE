//-----------------------------------------------------------------------------
// FILE:	    NetHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Retry;

namespace Neon.Net
{
    /// <summary>
    /// Useful network related utilities.
    /// </summary>
    public static class NetHelper
    {
        // Retry [hosts] file munging operations for up to 10 seconds at 100ms intervals.

        private static readonly TimeSpan maxRetryTime  = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan retryInterval = TimeSpan.FromMilliseconds(100);
        private static readonly int maxAttempts        = (int)Math.Max(1, maxRetryTime.TotalMilliseconds / retryInterval.TotalMilliseconds);

        private static LinearRetryPolicy retryFile     = new LinearRetryPolicy(typeof(IOException), maxAttempts: maxAttempts, retryInterval: retryInterval);
        private static LinearRetryPolicy retryReady    = new LinearRetryPolicy(typeof(NotReadyException), maxAttempts: maxAttempts, retryInterval: retryInterval);

        /// <summary>
        /// Regex for verifying DNS hostnames.
        /// </summary>
        public static Regex DnsHostRegex { get; private set; } = new Regex(@"^(([a-z0-9]|[a-z0-9][a-z0-9\-_]){1,61})(\.([a-z0-9]|[a-z0-9][a-z0-9\-_]){1,61})*$", RegexOptions.IgnoreCase);

        /// <summary>
        /// Verifies that a string is a valid DNS hostname.
        /// </summary>
        /// <param name="host">The string being tested.</param>
        /// <returns><c>true</c> if the hostname is valid.</returns>
        public static bool IsValidHost(string host)
        {
            if (string.IsNullOrEmpty(host) || host.Length > 255)
            {
                return false;
            }

            return DnsHostRegex.IsMatch(host);
        }

        /// <summary>
        /// Determines whether two IP addresses are equal.
        /// </summary>
        /// <param name="address1">Address 1.</param>
        /// <param name="address2">Address 2.</param>
        /// <returns><c>true</c> if the addresses are equal.</returns>
        public static bool AddressEquals(IPAddress address1, IPAddress address2)
        {
            if (address1.AddressFamily != address2.AddressFamily)
            {
                return false;
            }

            var bytes1 = address1.GetAddressBytes();
            var bytes2 = address2.GetAddressBytes();

            if (bytes1.Length != bytes2.Length)
            {
                return false;
            }

            for (int i = 0; i < bytes1.Length; i++)
            {
                if (bytes1[i] != bytes2[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Increments an IPv4 address by adding an integer value.
        /// </summary>
        /// <param name="address">The input IPv4 address.</param>
        /// <param name="incrementBy">The increment value (defaults to <b>+1</b>).</param>
        /// <returns>The next address or <b>0.0.0.0</b> when we wrap-around the address space.</returns>
        /// <exception cref="NotSupportedException">Thrown for non-IPv4 addresses.</exception>
        public static IPAddress AddressIncrement(IPAddress address, int incrementBy = 1)
        {
            if (address.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new NotSupportedException("Only IPv4 addresses are supported.");
            }

            var     addressBytes = address.GetAddressBytes();
            uint    addressValue;

            addressValue  = (uint)addressBytes[0] << 24;
            addressValue |= (uint)addressBytes[1] << 16;
            addressValue |= (uint)addressBytes[2] << 8;
            addressValue |= (uint)addressBytes[3];

            addressValue += (uint)incrementBy;

            addressBytes[0] = (byte)(addressValue >> 24);
            addressBytes[1] = (byte)(addressValue >> 16);
            addressBytes[2] = (byte)(addressValue >> 8);
            addressBytes[3] = (byte)(addressValue);

            return IPAddress.Parse($"{addressBytes[0]}.{addressBytes[1]}.{addressBytes[2]}.{addressBytes[3]}");
        }

        /// <summary>
        /// Converts an IPv4 address into a32-bit unsigned integer equivalent.
        /// </summary>
        /// <param name="address">The input IPv4 address.</param>
        /// <returns>The 32-bit unsigned integer equivalent.</returns>
        public static uint AddressToUint(IPAddress address)
        {
            var     addressBytes = address.GetAddressBytes();
            uint    addressValue;

            addressValue  = (uint)addressBytes[0] << 24;
            addressValue |= (uint)addressBytes[1] << 16;
            addressValue |= (uint)addressBytes[2] << 8;
            addressValue |= (uint)addressBytes[3];

            return addressValue;
        }

        /// <summary>
        /// Converts an unsigned 32-bit integer into an IPv4 address.
        /// </summary>
        /// <param name="value">The input value.</param>
        /// <returns>The <see cref="IPAddress"/>.</returns>
        public static IPAddress UintToAddress(uint value)
        {
            var addressBytes = new byte[4];

            addressBytes[0] = (byte)(value >> 24);
            addressBytes[1] = (byte)(value >> 16);
            addressBytes[2] = (byte)(value >> 8);
            addressBytes[3] = (byte)(value);

            return new IPAddress(addressBytes);
        }

        /// <summary>
        /// Determines whether an integer is a valid network port number.
        /// </summary>
        /// <param name="port">The port.</param>
        /// <returns><c>true</c> if the port is valid.</returns>
        public static bool IsValidPort(int port)
        {
            return 0 < port && port <= ushort.MaxValue;
        }

        /// <summary>
        /// Returns a usable random IP address for use for DNS reolutions.
        /// </summary>
        /// <returns>The generated <see cref="IPAddress"/>.</returns>
        private static IPAddress GetRandomAddress()
        {
            // For some reason, the Windows DNS resolver doesn't resolve hostname with
            // IP addresses greater than or equal to [240.0.0.0].  I've also seen the Windows
            // DNS resolver fail for host addresses with like [0.x.x.x].
            //
            // We're going to mitigate each this by generating a new address
            // until we get a good one.

            while (true)
            {
                var addressBytes = NeonHelper.GetCryptoRandomBytes(4);

                if (addressBytes[0] == 0 || addressBytes[0] >= 240)
                {
                    continue;   // Try again.
                }

                return new IPAddress(addressBytes);
            }
        }

        /// <summary>
        /// <para>
        /// Used to temporarily modify the <b>hosts</b> file used by the DNS resolver
        /// for debugging or other purposes.
        /// </para>
        /// <note>
        /// <b>WARNING:</b> Modifying the <b>hosts</b> file will impact all processes
        /// on the system, not just the current one and this is designed to be used by
        /// a single process at a time.
        /// </note>
        /// </summary>
        /// <param name="hostEntries">A dictionary mapping the hostnames to an IP address or <c>null</c>.</param>
        /// <param name="section">
        /// <para>
        /// Optionally specifies the string to use to mark the hostnames section.  This
        /// defaults to <b>MODIFY</b> which will delimit the section with <b># NEON-BEGIN-MODIFY</b>
        /// and <b># NEON-END-MODIFY</b>.  You may pass a different string to identify a custom section.
        /// </para>
        /// <note>
        /// The string passed must be a valid DNS hostname label that must begin with a letter
        /// followed by letters, digits or dashes.  The maximum length is 63 characters.
        /// </note>
        /// </param>
        /// <remarks>
        /// <note>
        /// This method requires elevated administrative privileges.
        /// </note>
        /// <para>
        /// This method adds or removes a temporary section of host entry definitions
        /// delimited by special comment lines.  When <paramref name="hostEntries"/> is 
        /// non-null and non-empty, the section will be added or updated.  Otherwise, the
        /// section will be removed.
        /// </para>
        /// <para>
        /// You can remove all host sections by passing both <paramref name="hostEntries"/> 
        /// and <paramref name="section"/> as <c>null</c>.
        /// </para>
        /// </remarks>
        public static void ModifyLocalHosts(Dictionary<string, IPAddress> hostEntries = null, string section = "MODIFY")
        {
#if XAMARIN
            throw new NotSupportedException();
#else
            if (hostEntries != null && string.IsNullOrWhiteSpace(section))
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (section != null)
            {
                var sectionOK = char.IsLetter(section[0]) && section.Length <= 63;

                if (sectionOK)
                {
                    foreach (var ch in section)
                    {
                        if (!char.IsLetterOrDigit(ch) && ch != '-')
                        {
                            sectionOK = false;
                            break;
                        }
                    }
                }

                if (!sectionOK)
                {
                    throw new ArgumentException("Suffix is not a valid DNS host name label.", nameof(section));
                }

                section = section.ToUpperInvariant();
            }

            string hostsPath;

            if (NeonHelper.IsWindows)
            {
                hostsPath = Path.Combine(Environment.GetEnvironmentVariable("windir"), "System32", "drivers", "etc", "hosts");
            }
            else if (NeonHelper.IsLinux || NeonHelper.IsOSX)
            {
                hostsPath = "/etc/hosts";
            }
            else
            {
                throw new NotSupportedException();
            }

            // We're seeing transient file locked errors when trying to update the [hosts] file.
            // My guess is that this is cause by the Window DNS resolver opening the file as
            // READ/WRITE to prevent it from being modified while the resolver is reading any
            // changes.
            //
            // We're going to mitigate this by retrying a few times.
            //
            // It can take a bit of time for the Windows DNS resolver to pick up the change.
            //
            //      https://github.com/nforgeio/neonKUBE/issues/244
            //
            // We're going to mitigate this by writing a [neon-modify-local-hosts.nhive.io] record with
            // a random IP address and then wait for for the DNS resolver to report the correct address.
            //
            // Note that this only works on Windows and perhaps OSX.  This doesn't work on
            // Linux because there's no central DNS resolver there.  See the issue below for
            // more information:
            //
            //      https://github.com/nforgeio/neonKUBE/issues/271

            var updateHost    = section != null ? $"{section.ToLowerInvariant()}.neonforge-marker" : $"H-{Guid.NewGuid().ToString("D")}.neonforge-marker";
            var addressBytes  = NeonHelper.GetCryptoRandomBytes(4);
            var updateAddress = GetRandomAddress();
            var lines         = new List<string>();
            var existingHosts = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            var different     = false;

            retryFile.InvokeAsync(
                async () =>
                {
                    var beginMarker = $"# NEON-BEGIN-";
                    var endMarker   = $"# NEON-END-";

                    if (section != null)
                    {
                        beginMarker += section;
                        endMarker   += section;
                    }

                    var inputLines = File.ReadAllLines(hostsPath);
                    var inSection  = false;

                    // Load lines of text from the current [hosts] file, without
                    // any lines for the named section.  We're going to parse those
                    // lines instead, so we can compare them against the [hostEntries]
                    // passed to determine whether we actually need to update the
                    // [hosts] file.

                    lines.Clear();
                    existingHosts.Clear();

                    foreach (var line in inputLines)
                    {
                        var trimmed = line.Trim();

                        if (trimmed == beginMarker || (section == null && trimmed.StartsWith(beginMarker)))
                        {
                            inSection = true;
                        }
                        else if (trimmed == endMarker || (section == null && trimmed.StartsWith(endMarker)))
                        {
                            inSection = false;
                        }
                        else
                        {
                            if (inSection)
                            {
                                // The line is within the named section, so we're going to parse
                                // the host entry (if any) and add it to [existingHosts].

                                if (trimmed.Length == 0 || trimmed.StartsWith("#"))
                                {
                                    // Ignore empty or comment lines (just to be safe).

                                    continue;
                                }

                                // We're going to simply assume that the address and hostname
                                // are separated by whitespace and that there's no other junk
                                // on the line (like comments added by the operator).  If there
                                // is any junk, we'll capture that too and then the entries
                                // won't match and we'll just end up rewriting the section
                                // (which is reasonable).
                                //
                                // Note that we're going to ignore the special marker entry.

                                var fields   = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                var address  = fields[0];
                                var hostname = fields.Length > 1 ? fields[1] : string.Empty;

                                if (!hostname.EndsWith(".neonforge-marker"))
                                {
                                    existingHosts[hostname] = address;
                                }
                            }
                            else
                            {
                                // The line is not in the named section, so we'll
                                // include it as as.

                                lines.Add(line);
                            }
                        }
                    }

                    // Compare the existing entries against the new ones and rewrite
                    // the [hosts] file only if they are different.

                    if (hostEntries != null && hostEntries.Count == existingHosts.Count)
                    {
                        foreach (var item in hostEntries)
                        {
                            if (!existingHosts.TryGetValue(item.Key, out var existingAddress) ||
                                item.Value.ToString() != existingAddress)
                            {
                                different = true;
                                break;
                            }
                        }

                        if (!different)
                        {
                            return;
                        }
                    }

                    // Append the section if it has any host entries.

                    if (hostEntries?.Count > 0)
                    {
                        lines.Add(beginMarker);

                        // Append the special update host with a random IP address.

                        var address = updateAddress.ToString();

                        lines.Add($"        {address}{new string(' ', 16 - address.Length)}    {updateHost}");

                        // Append the new entries.

                        foreach (var item in hostEntries)
                        {
                            address = item.Value.ToString();

                            lines.Add($"        {address}{new string(' ', 16 - address.Length)}    {item.Key}");
                        }

                        lines.Add(endMarker);
                    }

                    File.WriteAllLines(hostsPath, lines.ToArray());
                    await Task.CompletedTask;
                    
                }).Wait();

            if (!different)
            {
                // We didn't detect any changes to the section above so we're going to
                // exit without rewriting the [hosts] file.

                return;
            }

            if (NeonHelper.IsWindows)
            {
                // Flush the DNS cache (and I believe this reloads the [hosts] file too).

                var response = NeonHelper.ExecuteCapture("ipconfig", "/flushdns");

                if (response.ExitCode != 0)
                {
                    throw new ToolException($"ipconfig [exitcode={response.ExitCode}]: {response.ErrorText}");
                }
            }
            else if (NeonHelper.IsOSX)
            {
                // $todo(jeff.lill):
                //
                // We may need to clear the OSX DNS cache here.  Here's some information on 
                // how to do this:
                //
                //      https://help.dreamhost.com/hc/en-us/articles/214981288-Flushing-your-DNS-cache-in-Mac-OS-X-and-Linux

                throw new NotImplementedException("$todo(jeff.lill): Purge the OSX DNS cache.");
            }

            if (NeonHelper.IsWindows || NeonHelper.IsOSX)
            {
                // Poll the local DNS resolver until it reports the correct address for the
                // [neon-modify-local-hosts.nhive.io].
                //
                // If [hostEntries] is not null and contains at least one entry, we'll lookup
                // [neon-modify-local-hosts.neon] and compare the IP address to ensure that the 
                // resolver has loaded the new entries.
                //
                // If [hostEntries] is null or empty, we'll wait until there are no records
                // for [neon-modify-local-hosts.neon] to ensure that the resolver has reloaded
                // the hosts file after we removed the entries.
                //
                // Note that we're going to count the retries and after the 20th (about 2 second's
                // worth of 100ms polling), we're going to rewrite the [hosts] file.  I've seen
                // situations where at appears that the DNS resolver isn't re-reading [hosts]
                // after it's been updated.  I believe this is due to the file being written 
                // twice, once to remove the section and then shortly again there after to
                // write the section again.  I believe there's a chance that the resolver may
                // miss the second file change notification.  Writing the file again should
                // trigger a new notification.

                var retryCount = 0;

                retryReady.InvokeAsync(
                    async () =>
                    {
                        var addresses = await GetHostAddressesAsync(updateHost);

                        if (hostEntries?.Count > 0)
                        {
                            // Ensure that the new records have been loaded by the resolver.

                            if (addresses.Length != 1)
                            {
                                RewriteOn20thRetry(hostsPath, lines, ref retryCount);
                                throw new NotReadyException($"[{updateHost}] lookup is returning [{addresses.Length}] results.  There should be [1].");
                            }

                            if (addresses[0].ToString() != updateAddress.ToString())
                            {
                                RewriteOn20thRetry(hostsPath, lines, ref retryCount);
                                throw new NotReadyException($"DNS is [{updateHost}={addresses[0]}] rather than [{updateAddress}].");
                            }
                        }
                        else
                        {
                            // Ensure that the resolver recognizes that we removed the records.

                            if (addresses.Length != 0)
                            {
                                RewriteOn20thRetry(hostsPath, lines, ref retryCount);
                                throw new NotReadyException($"[{updateHost}] lookup is returning [{addresses.Length}] results.  There should be [0].");
                            }
                        }

                    }).Wait();
            }
#endif
        }

        /// <summary>
        /// Lists the names of the local host sections.
        /// </summary>
        /// <returns>The section names converted to uppercase.</returns>
        public static IEnumerable<string> ListLocalHostsSections()
        {
            string hostsPath;

            if (NeonHelper.IsWindows)
            {
                hostsPath = Path.Combine(Environment.GetEnvironmentVariable("windir"), "System32", "drivers", "etc", "hosts");
            }
            else if (NeonHelper.IsLinux || NeonHelper.IsOSX)
            {
                hostsPath = "/etc/hosts";
            }
            else
            {
                throw new NotSupportedException();
            }

            var sections = new List<string>();

            using (var reader = new StringReader(File.ReadAllText(hostsPath)))
            {
                foreach (var rawLine in reader.Lines())
                {
                    var line = rawLine.Trim();

                    if (line.StartsWith("# NEON-BEGIN-"))
                    {
                        var sectionName = line.Substring("# NEON-BEGIN-".Length).Trim();

                        if (!string.IsNullOrEmpty(sectionName))
                        {
                            sections.Add(sectionName.ToUpperInvariant());
                        }
                    }
                }
            }

            return sections;
        }

        /// <summary>
        /// Rewrites the hosts file on the 20th retry.
        /// </summary>
        /// <param name="hostsPath">Path to the hosts file.</param>
        /// <param name="lines">The host file lines.</param>
        /// <param name="retryCount">The retry count.</param>
        private static void RewriteOn20thRetry(string hostsPath, List<string> lines, ref int retryCount)
        {
            if (retryCount++ != 20)
            {
                return;
            }

            File.WriteAllLines(hostsPath, lines);
        }

        /// <summary>
        /// Performs a DNS lookup.
        /// </summary>
        /// <param name="hostname">The target hostname.</param>
        /// <returns>The array of IP addresses resolved or an empty array if the hostname lookup failed.</returns>
        private static async Task<IPAddress[]> GetHostAddressesAsync(string hostname)
        {
            try
            {
                return await Dns.GetHostAddressesAsync(hostname);
            }
            catch (SocketException)
            {
                return await Task.FromResult(new IPAddress[0]);
            }
        }

        /// <summary>
        /// Pings one or more hostnames or IP addresses in parallel to identify one that
        /// appears to be online and reachable via the network (because it answers a ping).
        /// </summary>
        /// <param name="hosts">The hostname or IP addresses to be tested.</param>
        /// <param name="failureMode">
        /// Specifies what should happen when there are no reachable hosts.  
        /// This defaults to <see cref="ReachableHostMode.ReturnFirst"/>.
        /// </param>
        /// <returns>A <see cref="ReachableHost"/> instance describing the host or <c>null</c>.</returns>
        /// <exception cref="NetworkException">
        /// Thrown if no hosts are reachable and <paramref name="failureMode"/> is 
        /// passed as <see cref="ReachableHostMode.Throw"/>.
        /// </exception>
        public static ReachableHost GetReachableHost(IEnumerable<string> hosts, ReachableHostMode failureMode = ReachableHostMode.ReturnFirst)
        {
            Covenant.Requires<ArgumentNullException>(hosts != null);
            Covenant.Requires<ArgumentNullException>(hosts.Count() > 0);

            var reachableHosts = GetReachableHosts(hosts);

            // We want to favor reachable hosts that appear earlier in the
            // hosts list passed over hosts that appear later.

            if (!reachableHosts.IsEmpty())
            {
                foreach (var host in hosts)
                {
                    foreach (var reachableHost in reachableHosts)
                    {
                        if (host == reachableHost.Host)
                        {
                            return reachableHost;
                        }
                    }
                }
            }

            // None of the hosts responded so the result is determined by the
            // failure mode.

            switch (failureMode)
            {
                case ReachableHostMode.ReturnFirst:

                    var firstHost = hosts.First();

                    return new ReachableHost(firstHost, null, TimeSpan.Zero, unreachable: true);

                case ReachableHostMode.ReturnNull:

                    return null;

                case ReachableHostMode.Throw:

                    throw new NetworkException("None of the hosts responded.");

                default:

                    throw new NotImplementedException($"Unexpected failure [mode={failureMode}].");
            }
        }


        /// <summary>
        /// Pings one or more hostnames or IP addresses in parallel to identify those that
        /// appear to be online and reachable via the network (because it answers a ping).
        /// </summary>
        /// <param name="hosts">The hostname or IP addresses to be tested.</param>
        /// <returns>The <see cref="ReachableHost"/> instances describing the reachable hosts (if any).</returns>
        public static IEnumerable<ReachableHost> GetReachableHosts(IEnumerable<string> hosts)
        {
            Covenant.Requires<ArgumentNullException>(hosts != null);

            if (hosts.IsEmpty())
            {
                return new List<ReachableHost>();   // No hosts were passed.
            }

            // Try sending up to three pings to each host in parallel to get a 
            // list of the reachable ones.

            const int tryCount = 3;

            var reachableHosts = new Dictionary<string, ReachableHost>();
            var pingOptions    = new PingOptions(ttl: 32, dontFragment: true);
            var pingTimeout    = TimeSpan.FromSeconds(1);

            for (int i = 0; i < tryCount; i++)
            {
                var remainingHosts = hosts.Where(h => !reachableHosts.ContainsKey(h));

                if (remainingHosts.Count() == 0)
                {
                    break;  // All of the hosts have already answered.
                }

                Parallel.ForEach(remainingHosts,
                    host =>
                    {
                        using (var ping = new Ping())
                        {
                            try
                            {
                                var reply = ping.Send(host, (int)pingTimeout.TotalMilliseconds);

                                if (reply.Status == IPStatus.Success)
                                {
                                    lock (reachableHosts)
                                    {
                                        reachableHosts.Add(host, new ReachableHost(host, reply));
                                    }
                                }
                            }
                            catch
                            {
                                // Intentionally ignoring these.
                            }
                        }
                    });
            }

            return reachableHosts.Values;
        }

        /// <summary>
        /// Computes the TCP maximum segment size for a given MTU, optionally taking a
        /// VXLAN wrapper headers into account.
        /// </summary>
        /// <param name="mtu">Specifies the target MTU (defaults to 1500).</param>
        /// <param name="vxLan">Optionally indicates that traffic is routed via a VXLAN.</param>
        /// <returns>The maximum segment size in bytes.</returns>
        public static int ComputeMSS(int mtu = NetConst.DefaultMTU, bool vxLan = false)
        {
            var mss = mtu - NetConst.TCPHeader;

            if (vxLan)
            {
                mss -= NetConst.VXLANHeader;
            }

            return mss;
        }

        /// <summary>
        /// Attempts to parse an IPv4 network endpoint.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="endpoint">Returns as the parsed endpoint.</param>
        /// <returns><c>true</c> on success.</returns>
        public static bool TryParseIPv4Endpoint(string input, out IPEndPoint endpoint)
        {
            endpoint = null;

            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            var fields = input.Split(new char[] { ':' }, 2);

            if (fields.Length != 2)
            {
                return false;
            }

            if (!IPAddress.TryParse(fields[0], out var address) || address.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            if (!int.TryParse(fields[1], out var port) || !IsValidPort(port))
            {
                return false;
            }

            endpoint = new IPEndPoint(address, port);

            return true;
        }

        /// <summary>
        /// Parses an IPv4 endpoint from a string.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns>The parsed <see cref="IPEndPoint"/>.</returns>
        /// <exception cref="FormatException">Thrown if the input is not valid.</exception>
        public static IPEndPoint ParseIPv4Endpoint(string input)
        {
            if (TryParseIPv4Endpoint(input, out var endpoint))
            {
                return endpoint;
            }
            else
            {
                throw new FormatException($"[{input}] is not a valid IPv4 endpoint.");
            }
        }

        /// <summary>
        /// Returns a free TCP port for a local IP address.
        /// </summary>
        /// <param name="address">The IP address.</param>
        /// <returns>The free port number.</returns>
        /// <exception cref="NetworkException">Thrown when there are no available ports.</exception>
        public static int GetUnusedTcpPort(IPAddress address)
        {
            Covenant.Requires<ArgumentNullException>(address != null);

            try
            {
                var listener = new TcpListener(address, 0);

                listener.Start();

                var port = ((IPEndPoint)listener.LocalEndpoint).Port;

                listener.Stop();

                return port;
            }
            catch (Exception e)
            {
                throw new NetworkException($"Cannot obtain a free port for [{address}].", e);
            }
        }
    }
}


