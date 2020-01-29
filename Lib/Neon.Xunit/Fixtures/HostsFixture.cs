//-----------------------------------------------------------------------------
// FILE:	    HostsFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Retry;

namespace Neon.Xunit
{
    /// <summary>
    /// Used to manage the local DNS resolver<b>hosts</b> file on the current computer.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <para>
    /// <b>IMPORTANT:</b> The Neon <see cref="TestFixture"/> implementation <b>DOES NOT</b>
    /// support parallel test execution because fixtures may impact global machine state
    /// like starting a Couchbase Docker container, modifying the local DNS <b>hosts</b>
    /// file or managing a Docker Swarm or cluster.
    /// </para>
    /// <para>
    /// You should explicitly disable parallel execution in all test assemblies that
    /// rely on test fixtures by adding a C# file called <c>AssemblyInfo.cs</c> with:
    /// </para>
    /// <code language="csharp">
    /// [assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
    /// </code>
    /// </note>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public class HostsFixture : TestFixture
    {
        // Implementation Note:
        // --------------------
        //
        // We're going to be adding records to the local DNS resolver hosts
        // file within a section that looks like:
        //
        //      # START-HostsFixture-GUID
        //      1.2.3.4 test.com
        //      5.6.7.8 foo.com
        //      # END-HostsFixture-GUID
        //
        // where GUID is generated for each [HostsFixture] instance so that
        // each instance will have it's own section in the hosts file.
        //
        // The class maintains a static reference count that tracks the how many
        // instances have been created by the test runner for the current test
        // class.  We're depending on the test runner to dispose all any fixtures
        // after a test completes before starting another test.
        //
        // We use this reference count to ensure that any records from a previous
        // test run have been removed before adding any new records for the
        // current run.  We'll also ensure that we've removed all records when
        // the last [HostFixture] is disposed.
        //
        // It's possible that a test may be interrupted before being allowed to
        // complete and dispose the fixtures.  The base [TextFixture] class 
        // addresses this by calling [EnsureReset()] as necessary.
        //
        // NOTE:
        //
        // We're seeing transient file locked errors when trying to open the [hosts] file
        // on Windows.  My theory is that this is cause by the Window DNS resolver opening 
        // the file as READ/WRITE to prevent it from being modified while the resolver is
        // reading any changes.  We're going to mitigate this by retrying a few times.
        //
        // It can take a bit of time for the Windows DNS resolver to pick up the change.
        //
        //      https://github.com/nforgeio/neonKUBE/issues/244
        //
        // We're going to mitigate this by writing a [neon-GUID.nhive.io] record with the
        // [1.2.3.4] address and then wait the local DNS resolver to resolve or not
        // resolve this host based on whether we've added or remove a section.

        //---------------------------------------------------------------------
        // Static members

        private const string dummyIP = "1.2.3.4";

        private static object               syncLock   = new object();
        private static LinearRetryPolicy    retryFile  = new LinearRetryPolicy(typeof(IOException), maxAttempts: 50, retryInterval: TimeSpan.FromMilliseconds(100));
        private static LinearRetryPolicy    retryReady = new LinearRetryPolicy(typeof(NotReadyException), maxAttempts: 50, retryInterval: TimeSpan.FromMilliseconds(100));

        /// <summary>
        /// Path to the local DNS resolver's [hosts] file.
        /// </summary>
        private static readonly string HostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "system32", "drivers", "etc", "hosts");

        /// <summary>
        /// Called by <see cref="TestFixture"/> to ensure that the hosts file
        /// contains no DNS records remaining after an interrupted test run.
        /// </summary>
        public static void EnsureReset()
        {
            RemoveSection();
        }

        /// <summary>
        /// Removes a specific fixture section from the <b>hosts</b> file or all
        /// fixture sections if <paramref name="fixtureId"/> is <c>null</c>.
        /// </summary>
        /// <param name="fixtureId">
        /// Identifies the fixture section to be removed or <c>null</c> to 
        /// remove all fixture sections.
        /// </param>
        private static void RemoveSection(string fixtureId = null)
        {
            var sb           = new StringBuilder();
            var changed      = false;
            var sectionGuids = new HashSet<string>();

            // Update the [hosts] file.

            retryFile.InvokeAsync(
                async () =>
                {
                    if (File.Exists(HostsPath))
                    {
                        using (var reader = new StreamReader(new FileStream(HostsPath, FileMode.Open, FileAccess.ReadWrite)))
                        {
                            var guid        = fixtureId ?? string.Empty;
                            var startMarker = $"# START-NEON-HOSTS-FIXTURE-{guid}";
                            var endMarker   = $"# END-NEON-HOSTS-FIXTURE-{guid}";
                            var inSection   = false;

                            foreach (var line in reader.Lines())
                            {
                                if (inSection)
                                {
                                    if (line.StartsWith(endMarker))
                                    {
                                        inSection = false;
                                        changed   = true;
                                    }
                                }
                                else
                                {
                                    if (line.StartsWith(startMarker))
                                    {
                                        // Extract the section GUID from the marker because we'll need
                                        // these below when we verify that the resolver has picked up
                                        // the changes.

                                        var posGuid     = line.LastIndexOf('-') + 1;
                                        var sectionGuid = line.Substring(posGuid);

                                        if (!sectionGuids.Contains(sectionGuid))
                                        {
                                            sectionGuids.Add(sectionGuid);
                                        }

                                        inSection = true;
                                        changed   = true;
                                    }
                                    else
                                    {
                                        if (!inSection)
                                        {
                                            sb.AppendLine(line);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (changed)
                    {
                        File.WriteAllText(HostsPath, sb.ToString());
                    }

                    await Task.CompletedTask;

                }).Wait();

            if (changed)
            {
                // We need to verify that the local DNS resolver has picked up the change
                // by verifying that none of the removed section hostnames resolve.

                retryReady.InvokeAsync(
                    async () =>
                    {
                        foreach (var sectionGuid in sectionGuids)
                        {
                            var hostname  = GetSectionHostname(sectionGuid);
                            var addresses = await GetHostAddressesAsync(hostname);

                            if (addresses.Length > 0)
                            {
                                throw new NotReadyException($"Waiting for [{hostname}] to be removed by the local DNS resolver.");
                            }
                        }

                    }).Wait();
            }
        }

        /// <summary>
        /// Returns the hostname for a section GUID.
        /// </summary>
        /// <param name="guid">The section GUID string.</param>
        /// <returns>The section hostname.</returns>
        private static string GetSectionHostname(string guid)
        {
            return $"neon-{guid}.nhive.io";
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

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The GUID used to mark this fixture instance's entries in the hosts file.
        /// </summary>
        private readonly string fixtureId = Guid.NewGuid().ToString("d");

        /// <summary>
        /// The DNS records.
        /// </summary>
        private List<Tuple<string, string>> records = new List<Tuple<string, string>>();

        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public HostsFixture()
        {
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~HostsFixture()
        {
            Dispose(false);
        }

        /// <summary>
        /// Adds an IP address to the local DNS resolver's hosts file.
        /// </summary>
        /// <param name="hostname">The hostname.</param>
        /// <param name="address">The IP address.</param>
        /// <param name="deferCommit">
        /// Optionally indicates that the change will not be committed to the hosts
        /// until <see cref="Commit"/> is called.  This defaults to <c>falsae</c>.
        /// </param>
        /// <remarks>
        /// <note>
        /// This method will not add the duplicate hostname/address mappings
        /// to the fixture.
        /// </note>
        /// </remarks>
        public void AddHostAddress(string hostname, string address, bool deferCommit = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hostname), nameof(hostname));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(address), nameof(address));

            foreach (var record in records)
            {
                if (record.Item1.Equals(hostname, StringComparison.InvariantCultureIgnoreCase) &&
                    record.Item2.Equals(address, StringComparison.InvariantCultureIgnoreCase))
                {
                    return;     // Don't add a duplicate record.
                }
            }

            records.Add(new Tuple<string, string>(hostname, address));

            if (!deferCommit)
            {
                Commit();
            }
        }

        /// <summary>
        /// Commits the DNS records to the hosts file.
        /// </summary>
        public void Commit()
        {
            // Use a static lock to ensure that only once fixture instance
            // at a time can munge the [hosts] file.

            lock (syncLock)
            {
                var sectionHostname = GetSectionHostname(fixtureId);

                // Remove any existing section for this instance.

                RemoveSection(fixtureId);

                // Append the fixture section to the end of the [hosts] file.

                var sb = new StringBuilder();

                sb.AppendLine($"# START-NEON-HOSTS-FIXTURE-{fixtureId}");

                sb.AppendLine($"{dummyIP, -15} {sectionHostname}");

                foreach (var record in records)
                {
                    sb.AppendLine($"{record.Item2, -15} {record.Item1}");
                }

                sb.AppendLine($"# END-NEON-HOSTS-FIXTURE-{fixtureId}");

                retryFile.InvokeAsync(
                    async () =>
                    {
                        File.AppendAllText(HostsPath, sb.ToString());
                        await Task.CompletedTask;

                    }).Wait();

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
                    // $todo(jefflill):
                    //
                    // We may need to clear the OSX DNS cache here.
                    //
                    // Here's some information on how to do this:
                    //
                    //      https://help.dreamhost.com/hc/en-us/articles/214981288-Flushing-your-DNS-cache-in-Mac-OS-X-and-Linux

                    throw new NotImplementedException("$todo(jefflill): Purge the OSX DNS cache.");
                }

                // Wait for the local DNS resolver to indicate that it's picked
                // up the changes by verifying that the section hostname resolves.

                retryReady.InvokeAsync(
                    async () =>
                    {
                        var addresses = await GetHostAddressesAsync(sectionHostname);

                        if (addresses.Length == 0)
                        {
                            throw new NotReadyException($"Waiting for [{sectionHostname}] to resolve by the local DNS resolver.");
                        }

                    }).Wait();
            }
        }

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Resets the fixture state.
        /// </summary>
        public override void Reset()
        {
            records.Clear();
            RemoveSection(fixtureId);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!base.IsDisposed)
                {
                    // Ensure that there are no remaining temporary records
                    // in the hosts file.

                    RemoveSection(fixtureId);
                    GC.SuppressFinalize(this);
                }
            }
        }
    }
}
