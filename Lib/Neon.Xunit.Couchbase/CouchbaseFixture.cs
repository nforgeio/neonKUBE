//-----------------------------------------------------------------------------
// FILE:	    CouchbaseFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;

using Couchbase;
using Couchbase.N1QL;
using Newtonsoft.Json.Linq;
using Xunit;

using Neon.Common;
using Neon.Data;
using Neon.Retry;
using Couchbase.Linq;
using System.Threading.Tasks;
using System.Text;

namespace Neon.Xunit.Couchbase
{
    /// <summary>
    /// Used to run a Docker container on the current machine as a test 
    /// fixture while tests are being performed and then deletes the
    /// container when the fixture is disposed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This fixture assumes that Couchbase is not currently running on the
    /// local workstation or as a container that is not named <b>cb-test</b>.
    /// You may see port conflict errors if either of these assumptions are
    /// not true.
    /// </para>
    /// <para>
    /// A somewhat safer but slower alternative, is to use the <see cref="DockerFixture"/>
    /// instead and add <see cref="CouchbaseFixture"/> as a subfixture.  The 
    /// advantage is that <see cref="DockerFixture"/> will ensure that all
    /// (potentially conflicting) containers are removed before the Couchbase
    /// fixture is started.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public sealed class CouchbaseFixture : ContainerFixture
    {
        private bool createPrimaryIndex;

        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public CouchbaseFixture()
        {
        }

        /// <summary>
        /// Starts a Couchbase container if it's not already running.  You'll generally want
        /// to call this in your test class constructor instead of <see cref="ITestFixture.Initialize(Action)"/>.
        /// </summary>
        /// <param name="settings">Optional Couchbase settings.</param>
        /// <param name="image">Optionally specifies the Couchbase container image (defaults to <b>neoncluster/couchbase-test:latest</b>).</param>
        /// <param name="name">Optionally specifies the Couchbase container name (defaults to <c>cb-test</c>).</param>
        /// <param name="env">Optional environment variables to be passed to the Couchbase container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <param name="username">Optional Couchbase username (defaults to <b>Administrator</b>).</param>
        /// <param name="password">Optional Couchbase password (defaults to <b>password</b>).</param>
        /// <param name="noPrimary">Optionally disable creation of a primary bucket index.</param>
        /// <returns>
        /// <c>true</c> if the fixture wasn't previously initialized and
        /// this method call initialized it or <c>false</c> if the fixture
        /// was already initialized.
        /// </returns>
        /// <remarks>
        /// <note>
        /// Some of the <paramref name="settings"/> properties will be ignored including 
        /// <see cref="CouchbaseSettings.Servers"/>.  This will be replaced by the local
        /// endpoint for the Couchbase container.  Also, the fixture will connect to the 
        /// <b>test</b> bucket by default (unless another is specified).
        /// </note>
        /// <para>
        /// This method creates a primary index by default because
        /// its very common for unit test to require a primary index.
        /// You can avoid creating a primary index by passing
        /// <paramref name="noPrimary"/><c>=true</c>.
        /// </para>
        /// <para>
        /// There are three basic patterns for using this fixture.
        /// </para>
        /// <list type="table">
        /// <item>
        /// <term><b>initialize once</b></term>
        /// <description>
        /// <para>
        /// The basic idea here is to have your test class initialize Couchbase
        /// once within the test class constructor inside of the initialize action
        /// with common state that all of the tests can access.
        /// </para>
        /// <para>
        /// This will be quite a bit faster than reconfiguring Couchbase at the
        /// beginning of every test and can work well for many situations.
        /// </para>
        /// </description>
        /// </item>
        /// <item>
        /// <term><b>initialize every test</b></term>
        /// <description>
        /// For scenarios where Couchbase must be cleared before every test,
        /// you can use the <see cref="Flush()"/> method to reset its
        /// state within each test method, populate the database as necessary,
        /// and then perform your tests.
        /// </description>
        /// </item>
        /// <item>
        /// <term><b>docker integrated</b></term>
        /// <description>
        /// The <see cref="CouchbaseFixture"/> can also be added to the <see cref="DockerFixture"/>
        /// and used within a swarm.  This is useful for testing multiple services and
        /// also has the advantage of ensure that swarm/node state is fully reset
        /// before the database container is started.
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        public bool Start(
            CouchbaseSettings   settings  = null,
            string              image     = "neoncluster/couchbase-test:latest",
            string              name      = "cb-test",
            string[]            env       = null,
            string              username  = "Administrator",
            string              password  = "password",
            bool                noPrimary = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(image));

            createPrimaryIndex = !noPrimary;

            return base.Initialize(
                () =>
                {
                    StartInAction(settings, image, name, env, username, password, noPrimary);
                });
        }

        /// <summary>
        /// Actually starts Couchbase within the initialization <see cref="Action"/>.  You'll
        /// generally want to use <see cref="Start(CouchbaseSettings, string, string, string[], string, string, bool)"/>
        /// but this method is used internally or for special situations.
        /// </summary>
        /// <param name="settings">Optional Couchbase settings.</param>
        /// <param name="image">Optionally specifies the Couchbase container image (defaults to <b>neoncluster/couchbase-test:latest</b>).</param>
        /// <param name="name">Optionally specifies the Couchbase container name (defaults to <c>cb-test</c>).</param>
        /// <param name="env">Optional environment variables to be passed to the Couchbase container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <param name="username">Optional Couchbase username (defaults to <b>Administrator</b>).</param>
        /// <param name="password">Optional Couchbase password (defaults to <b>password</b>).</param>
        /// <param name="noPrimary">Optionally disable creation of a primary bucket index.</param>
        /// <returns>
        /// <c>true</c> if the fixture wasn't previously initialized and
        /// this method call initialized it or <c>false</c> if the fixture
        /// was already initialized.
        /// </returns>
        public void StartInAction(
            CouchbaseSettings   settings  = null,
            string              image     = "neoncluster/couchbase-test:latest",
            string              name      = "cb-test",
            string[]            env       = null,
            string              username  = "Administrator",
            string              password  = "password",
            bool                noPrimary = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(image));

            base.CheckWithinAction();

            if (IsInitialized)
            {
                return;
            }

            RunContainer(name, image, new string[] { "--detach", "-p", "8091-8094:8091-8094", "-p", "11210:11210" }, env: env);

            settings = settings ?? new CouchbaseSettings();

            settings.Servers.Clear();
            settings.Servers.Add(new Uri("http://localhost:8091"));

            if (settings.Bucket == null)
            {
                settings.Bucket = "test";
            }

            Bucket   = settings.OpenBucket(username, password);
            Settings = settings;
            Username = username;
            Password = password;

            // Wait for the cluster to warm up.

            Bucket.WaitUntilReadyAsync(TimeSpan.FromSeconds(10)).Wait();

            // It appears that it may take a bit of time for the Couchbase query
            // service to start in new container we started above.  We're going to
            // retry creating the primary index (or a dummy index) until it works.

            var timeout = TimeSpan.FromMinutes(2);
            var retry = new LinearRetryPolicy(TransientDetector.Always, maxAttempts: (int)timeout.TotalSeconds, retryInterval: TimeSpan.FromSeconds(1));

            retry.InvokeAsync(
                async () =>
                {
                    if (Bucket == null)
                    {
                        Bucket = settings.OpenBucket(username, password);
                    }

                    try
                    {
                        if (!noPrimary)
                        {
                            await Bucket.QuerySafeAsync<dynamic>($"create primary index on {CbHelper.LiteralName(Bucket.Name)} using gsi");
                            await Bucket.WaitForIndexAsync("#primary");
                        }
                        else
                        {
                            // Create a dummy index to ensure that the query service is ready
                            // and then remove it.

                            var dummyName = "idx_couchbase_test_fixture";

                            await Bucket.QuerySafeAsync<dynamic>($"create index {CbHelper.LiteralName(dummyName)} on {CbHelper.LiteralName(Bucket.Name)} ({CbHelper.LiteralName("Field")}) using gsi");
                            await Bucket.QuerySafeAsync<dynamic>($"drop index {CbHelper.LiteralName(Bucket.Name)}.{CbHelper.LiteralName(dummyName)} using gsi");
                        }
                    }
                    catch
                    {
                        // It looks like we need to open a new bucket if the query service wasn't
                        // ready.  We'll dispose the old bucket and set it to NULL here and then
                        // open a fresh bucket above when the retry policy tries again.

                        Bucket.Dispose();
                        Bucket = null;

                        throw;
                    }

                }).Wait();
        }

        /// <summary>
        /// Returns the Couchbase <see cref="Bucket"/> to be used to interact with Couchbase.
        /// </summary>
        public NeonBucket Bucket { get; private set; }

        /// <summary>
        /// Returns the <see cref="CouchbaseSettings"/> used to connect to the bucket.
        /// </summary>
        public CouchbaseSettings Settings { get; private set; }

        /// <summary>
        /// Returns the Couchbase username.
        /// </summary>
        public string Username { get; private set; }

        /// <summary>
        /// Returns the Couchbase password.
        /// </summary>
        public string Password { get; private set; }

        /// <summary>
        /// Removes all data and indexes from the database bucket and then recreates the
        /// primary index if an index was specified when the fixture was started.
        /// </summary>
        public void Flush()
        {
            CheckDisposed();

            // Flush the bucket data.

            using (var bucketManager = Bucket.CreateManager())
            {
                bucketManager.Flush();
                Bucket.WaitUntilReadyAsync(TimeSpan.FromSeconds(30)).Wait();
            }

            // Drop all of the bucket indexes.

            var existingIndexes = Bucket.ListIndexesAsync().Result;

            if (existingIndexes.Count > 0)
            {
                foreach (var index in existingIndexes)
                {
                    Bucket.QuerySafeAsync<dynamic>($"drop index {CbHelper.LiteralName(Bucket.Name)}.{CbHelper.LiteralName(index.Name)} using {index.Type}").Wait();
                }
            }

            // Create the primary index if this was enabled when the fixture was started.

            if (createPrimaryIndex)
            {
                Bucket.QuerySafeAsync<dynamic>($"create primary index on {CbHelper.LiteralName(Bucket.Name)} using gsi").Wait();
                Bucket.WaitForIndexAsync("#primary").Wait();
            }
        }

        /// <summary>
        /// This method completely resets the fixture by removing the Couchbase 
        /// container from Docker.  Use <see cref="Flush"/> if you just want to 
        /// clear the database.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
        }
    }
}
