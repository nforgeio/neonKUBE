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
using System.Threading.Tasks;

using Couchbase;
using Couchbase.Linq;
using Couchbase.N1QL;
using Newtonsoft.Json.Linq;
using Xunit;

using Neon.Common;
using Neon.Data;
using Neon.Retry;

namespace Neon.Xunit.Couchbase
{
    /// <summary>
    /// Used to run the Docker <b>nhive.couchbase-test</b> container on 
    /// the current machine as a test fixture while tests are being performed 
    /// and then deletes the container when the fixture is disposed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This fixture assumes that Couchbase is not currently running on the
    /// local workstation or as a container that is named <b>cb-test</b>.
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
        private TimeSpan    warmupDelay = TimeSpan.FromSeconds(10);     // Time to allow Couchbase to start.
        private bool        createPrimaryIndex;

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
        /// <param name="image">Optionally specifies the Couchbase container image (defaults to <b>nhive/couchbase-test:latest</b>).</param>
        /// <param name="name">Optionally specifies the Couchbase container name (defaults to <c>cb-test</c>).</param>
        /// <param name="env">Optional environment variables to be passed to the Couchbase container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <param name="username">Optional Couchbase username (defaults to <b>Administrator</b>).</param>
        /// <param name="password">Optional Couchbase password (defaults to <b>password</b>).</param>
        /// <param name="noPrimary">Optionally disable creation of the primary bucket index.</param>
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
        /// This method creates a primary index by default because it's very common for 
        /// unit tests to require a primary index. You can avoid creating a primary index 
        /// by passing <paramref name="noPrimary"/><c>=true</c>.
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
        /// you can use the <see cref="Clear()"/> method to reset its
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
            string              image     = "nhive/couchbase-test:latest",
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
        /// <param name="image">Optionally specifies the Couchbase container image (defaults to <b>nhive/couchbase-test:latest</b>).</param>
        /// <param name="name">Optionally specifies the Couchbase container name (defaults to <c>cb-test</c>).</param>
        /// <param name="env">Optional environment variables to be passed to the Couchbase container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <param name="username">Optional Couchbase username (defaults to <b>Administrator</b>).</param>
        /// <param name="password">Optional Couchbase password (defaults to <b>password</b>).</param>
        /// <param name="noPrimary">Optionally disable creation of thea primary bucket index.</param>
        /// <returns>
        /// <c>true</c> if the fixture wasn't previously initialized and
        /// this method call initialized it or <c>false</c> if the fixture
        /// was already initialized.
        /// </returns>
        public void StartInAction(
            CouchbaseSettings   settings  = null,
            string              image     = "nhive/couchbase-test:latest",
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

            RunContainer(name, image, 
                new string[] 
                {
                    "--detach",
                    "--mount", "type=volume,target=/opt/couchbase/var",
                    "-p", "4369:4369",
                    "-p", "8091-8096:8091-8096",
                    "-p", "9100-9105:9100-9105",
                    "-p", "9110-9118:9110-9118",
                    "-p", "9120-9122:9120-9122",
                    "-p", "9999:9999",
                    "-p", "11207:11207",
                    "-p", "11209-11211:11209-11211",
                    "-p", "18091-18096:18091-18096",
                    "-p", "21100-21299:21100-21299"
                }, 
                env: env);

            Thread.Sleep(warmupDelay);

            settings = settings ?? new CouchbaseSettings();

            settings.Servers.Clear();
            settings.Servers.Add(new Uri("http://localhost:8091"));

            if (settings.Bucket == null)
            {
                settings.Bucket = "test";
            }

            Bucket   = null;
            Settings = settings;
            Username = username;
            Password = password;

            ConnectBucket();
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
        /// Establishes the bucket connection and waits until the Couchbase container is ready
        /// to start handling requests.
        /// </summary>
        private void ConnectBucket()
        {
            // Give the Couchbase container a chance to spin up.

            Thread.Sleep(warmupDelay);

            // Dispose any existing underlying cluster and bucket.

            if (Bucket != null)
            {
                var existingBucket = Bucket.GetInternalBucket();

                if (existingBucket != null)
                {
                    existingBucket.Cluster.CloseBucket(existingBucket);
                    existingBucket.Cluster.Dispose();
                    Bucket.SetInternalBucket(null);
                }
            }

            // It appears that it may take a bit of time for the Couchbase query
            // service to start in new container we started above.  We're going to
            // retry creating the primary index (or a dummy index) until it works.

            var bucket       = (NeonBucket)null;
            var indexCreated = false;
            var indexReady   = false;
            var queryReady   = false;

            NeonBucket.ReadyRetry.InvokeAsync(
                async () =>
                {
                    if (bucket == null)
                    {
                        bucket = Settings.OpenBucket(Username, Password);
                    }

                    try
                    {
                        if (createPrimaryIndex)
                        {
                            // Create the primary index if requested.

                            if (!indexCreated)
                            {
                                await bucket.QuerySafeAsync<dynamic>($"create primary index on {CbHelper.LiteralName(bucket.Name)} using gsi");
                                indexCreated = true;
                            }

                            if (!indexReady)
                            {
                                await bucket.WaitForIndexAsync("#primary");
                                indexReady = true;
                            }

                            // Ensure that the query service is running too.

                            if (!queryReady)
                            {
                                var query = new QueryRequest($"select meta({bucket.Name}).id, {bucket.Name}.* from {bucket.Name}")
                                    .ScanConsistency(ScanConsistency.RequestPlus);

                                await bucket.QuerySafeAsync<dynamic>(query);
                                queryReady = true;
                            }
                        }
                        else
                        {
                            // List the indexes to ensure the index service is ready when we didn't create a primary index.

                            if (!queryReady)
                            {
                                await bucket.ListIndexesAsync();
                                queryReady = true;
                            }
                        }
                    }
                    catch
                    {
                        // $hack(jeff.lill):
                        //
                        // It looks like we need to create a new bucket if the query service 
                        // wasn't ready.  I'm guessing that this is due to the Couchbase index
                        // service not being ready at the time the bucket was connected and
                        // the bucket isn't smart enough to retry looking for the index service
                        // afterwards.  This won't be a problem for most real-world scenarios
                        // because for those, Couchbase will have been started long ago and
                        // will continue running indefinitely.
                        //
                        // We'll dispose the old bucket and set it to NULL here and then
                        // open a fresh bucket above when the retry policy tries again.

                        bucket.Dispose();
                        bucket = null;

                        throw;
                    }

                }).Wait();

            // Use the new bucket if this is the first Couchbase container created or
            // else substitute the new underlying bucket into the existing bucket so
            // that unit tests don't need to be aware of the change.

            if (this.Bucket == null)
            {
                this.Bucket = bucket;
            }
            else
            {
                this.Bucket.SetInternalBucket(bucket.GetInternalBucket());
            }
        }

        /// <summary>
        /// Removes all data and indexes from the database bucket and then recreates the
        /// primary index if an index was specified when the fixture was started.
        /// </summary>
        public void Clear()
        {
            CheckDisposed();

            // $todo(jeff.lill):
            //
            // The code below was originally intended to clear the Couchbase bucket
            // in place and this seemed to work for several months and then it just stopped
            // working in Nov 2018 after I upgraded the CouchbaseNetClient nuget package.
            // The weird thing is that is still didn't work after I reverted.
            //
            // The problem seems to be due to a timing or race condition because if I pause
            // execution after clearing, the subsequent unit test passes.  Unfortunately, I 
            // haven't been able to figure out how to determine when everything is ready.
            // I even tried executing the unit test query that fails but it succeeded here
            // but still failed in the test.  I can't really explain that: perhaps 
            // Couchbase restarted one or more services sometime after I cleared the
            // bucket but after I checked for health below.
            //
            // It seems like the first test ran against a clean container always works
            // so I'm going to revert to simply restarting the container and come back
            // someday and remove this section.

#if DIDNT_WORK
            // Drop all of the bucket indexes.

            var existingIndexes = Bucket.ListIndexesAsync().Result;

            if (existingIndexes.Count > 0)
            {
                foreach (var index in existingIndexes)
                {
                    Bucket.QuerySafeAsync<dynamic>($"drop index {CbHelper.LiteralName(Bucket.Name)}.{CbHelper.LiteralName(index.Name)} using {index.Type}").Wait();
                }
            }

            // Flush the bucket data.

            using (var bucketManager = Bucket.CreateManager())
            {
                NeonBucket.ReadyRetry.InvokeAsync(
                    async () =>
                    {
                        bucketManager.Flush();
                        await Bucket.WaitUntilReadyAsync();

                    }).Wait();
            }

            // Wait until all of the indexes are actually deleted.

            NeonHelper.WaitFor(
                () =>
                {
                    var indexes = Bucket.ListIndexesAsync().Result;

                    return indexes.Count == 0;
                },
                timeout: NeonBucket.ReadyTimeout,
                pollTime: TimeSpan.FromMilliseconds(500));

            // Recreate the primary index if one was enabled when the fixture was started.

            if (createPrimaryIndex)
            {
                Bucket.QuerySafeAsync<dynamic>($"create primary index on {CbHelper.LiteralName(Bucket.Name)} using gsi").Wait();
                Bucket.WaitForIndexAsync("#primary").Wait();
            }
#endif
            base.Restart();
            Thread.Sleep(warmupDelay);
            ConnectBucket();
        }

        /// <summary>
        /// This method completely resets the fixture by removing the Couchbase 
        /// container from Docker.  Use <see cref="Clear"/> if you just want to 
        /// clear the database.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
        }
    }
}
