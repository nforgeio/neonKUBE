//-----------------------------------------------------------------------------
// FILE:	    NetHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
                var addressBytes = NeonHelper.RandBytes(4);

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
        /// defaults to <b>NEON-MODIFY</b> which will delimit the section with <b># BEGIN-NEON-MODIFY</b>
        /// and <b># END-NEON-MODIFY</b>.  You may pass a different string to identify a custom section.
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
        /// </remarks>
        public static void ModifyLocalHosts(Dictionary<string, IPAddress> hostEntries = null, string section = "NEON-MODIFY")
        {
#if XAMARIN
            throw new NotSupportedException();
#else
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(section));

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

            // $todo(jeff.lill):
            //
            // This method could be optimized by first reading the section hostname/address
            // mappings and then comparing these to desired host entries and writing/verifying
            // the entries only if they have changed.
            //
            // Perhaps this is something to revisit when implementing OSX support.

            // We're seeing transient file locked errors when trying to update the [hosts] file.
            // My guess is that this is cause by the Window DNS resolver opening the file as
            // READ/WRITE to prevent it from being modified while the resolver is reading any
            // changes.
            //
            // We're going to mitigate this by retrying a few times.
            //
            // It can take a bit of time for the Windows DNS resolver to pick up the change.
            //
            //      https://github.com/jefflill/NeonForge/issues/244
            //
            // We're going to mitigate this by writing a [neon-modify-local-hosts.hive] record with
            // a random IP address and then wait for for the DNS resolver to report the correct address.
            //
            // Note that this only works on Windows and perhaps OSX.  This doesn't work on
            // Linux because there's no central DNS resolver there.  See the issue below for
            // more information:
            //
            //      https://github.com/jefflill/NeonForge/issues/271

            var updateHost    = $"{section.ToLowerInvariant()}.neonforge";
            var addressBytes  = NeonHelper.RandBytes(4);
            var updateAddress = GetRandomAddress();
            var lines         = new List<string>();

            retryFile.InvokeAsync(
                async () =>
                {
                    var beginMarker = $"# BEGIN-{section}";
                    var endMarker   = $"# END-{section}";

                    var inputLines  = File.ReadAllLines(hostsPath);
                    var tempSection = false;

                    lines.Clear();

                    // Strip out any existing temporary sections.

                    foreach (var line in inputLines)
                    {
                        var trimmed = line.Trim();

                        if (trimmed == beginMarker)
                        {
                            tempSection = true;
                        }
                        else if (trimmed == endMarker)
                        {
                            tempSection = false;
                        }
                        else
                        {
                            if (!tempSection)
                            {
                                lines.Add(line);
                            }
                        }
                    }

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

            if (NeonHelper.IsWindows)
            {
                // Flush the DNS cache (and I believe this reloads the [hosts] file too).

                var response = NeonHelper.ExecuteCaptureStreams("ipconfig", "/flushdns");

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
                // [neon-modify-local-hosts.hive].
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
    }
}


