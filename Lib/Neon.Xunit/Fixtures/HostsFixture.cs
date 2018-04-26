//-----------------------------------------------------------------------------
// FILE:	    HostsFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Text;

using Neon.Common;

namespace Xunit
{
    /// <summary>
    /// Used to manage the local DNS resolver<b>hosts</b>. file.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <para>
    /// <b>IMPORTANT:</b> The Neon <see cref="TestFixture"/> implementation <b>DOES NOT</b>
    /// support parallel test execution because fixtures may impact global machine state
    /// like starting a Couchbase Docker container, modifying the local DNS <b>hosts</b>
    /// file or managing a Docker Swarm or neonCLUSTER.
    /// </para>
    /// <para>
    /// You should explicitly disable parallel execution in all test assemblies that
    /// rely on test fixtures by adding a C# file called <c>AssemblyInfo.cs</c> with:
    /// </para>
    /// <code language="csharp">
    /// [assembly: CollectionBehavior(DisableTestParallelization = true)]
    /// </code>
    /// </note>
    /// </remarks>
    /// <threadsafety instance="false"/>
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

        //---------------------------------------------------------------------
        // Static members

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
            var sb      = new StringBuilder();
            var changed = false;

            if (File.Exists(HostsPath))
            {
                using (var reader = new StreamReader(new FileStream(HostsPath, FileMode.Open, FileAccess.ReadWrite)))
                {
                    var guid        = fixtureId ?? string.Empty;
                    var startMarker = $"# START-HostsFixture-{guid}";
                    var endMarker   = $"# END-HostsFixture-{guid}";
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
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The GUID used to mark this fixture instance's entries in the hosts file.
        /// </summary>
        private readonly string fixtureId = Guid.NewGuid().ToString("D");

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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hostname));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(address));

            lock (base.SyncRoot)
            {
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
        }

        /// <summary>
        /// Commits the DNS records to the hosts file.
        /// </summary>
        public void Commit()
        {
            lock (base.SyncRoot)
            {
                // Remove any existing section for this instance.

                RemoveSection(fixtureId);

                // Append any records to the end of the [hosts] file.

                if (records.Count > 0)
                {
                    var sb = new StringBuilder();

                    sb.AppendLine($"# START-HostsFixture-{fixtureId}");

                    foreach (var record in records)
                    {
                        sb.AppendLine($"{record.Item2,-15} {record.Item1}");
                    }

                    sb.AppendLine($"# END-HostsFixture-{fixtureId}");

                    File.AppendAllText(HostsPath, sb.ToString());
                }
            }
        }

        /// <imheritdoc/>
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
            if (!base.IsDisposed)
            {
                // Ensure that there are no remaining temporary records
                // in the hosts file.

                RemoveSection(fixtureId);
            }
        }
    }
}
