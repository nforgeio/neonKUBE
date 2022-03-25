//-----------------------------------------------------------------------------
// FILE:	    CouchbaseFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
using Neon.Net;

namespace Neon.Xunit.Couchbase
{
    /// <summary>
    /// Used to run the Docker <b>couchbase-dev</b> container on the current 
    /// machine as a test fixture while tests are being performed and then 
    /// deletes the container when the fixture is disposed.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <para>
    /// <b>IMPORTANT:</b> The base Neon <see cref="TestFixture"/> implementation <b>DOES NOT</b>
    /// support parallel test execution.  You need to explicitly disable parallel execution in 
    /// all test assemblies that rely on thesex test fixtures by adding a C# file called 
    /// <c>AssemblyInfo.cs</c> with:
    /// </para>
    /// <code language="csharp">
    /// [assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
    /// </code>
    /// <para>
    /// and then define your test classes like:
    /// </para>
    /// <code language="csharp">
    /// public class MyTests
    /// {
    ///     [Collection(TestCollection.NonParallel)]
    ///     [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    ///     [Fact]
    ///     public void Test()
    ///     {
    ///     }
    /// }
    /// </code>
    /// </note>
    /// <para>
    /// This fixture assumes that Couchbase is not currently running on the
    /// local workstation or as a container named <b>cb-test</b>.
    /// You may see port conflict errors if either of these conditions
    /// are not true.
    /// </para>
    /// <para>
    /// A somewhat safer but slower alternative, is to use the <see cref="DockerFixture"/>
    /// instead and add <see cref="CouchbaseFixture"/> as a subfixture.  The 
    /// advantage is that <see cref="DockerFixture"/> will ensure that all
    /// (potentially conflicting) containers are removed before the Couchbase
    /// fixture is started.
    /// </para>
    /// <note>
    /// This fixture calls <see cref="RoundtripDataHelper.PersistableInitialize()"/> to ensure
    /// that any type filters for generated <see cref="IPersistableType"/> classes are automatically
    /// registered with <b>Linq2Couchbase</b>.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public sealed class CouchbaseFixture : ContainerFixture
    {
        private readonly TimeSpan   warmupDelay = TimeSpan.FromSeconds(2);      // Time to allow Couchbase to start.
        private readonly TimeSpan   retryDelay  = TimeSpan.FromSeconds(0.5);    // Time to wait after a failure.
        private bool                createPrimaryIndex;

        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public CouchbaseFixture()
        {
            // Ensure that any type filters for [IPersistableType] classes are
            // registered with Linq2Couchbase.

            RoundtripDataHelper.PersistableInitialize();
        }

        /// <summary>
        /// <para>
        /// Starts a Couchbase container if it's not already running.  You'll generally want
        /// to call this in your test class constructor instead of <see cref="ITestFixture.Start(Action)"/>.
        /// </para>
        /// <note>
        /// You'll need to call <see cref="StartAsComposed(CouchbaseSettings, string, string, string[], string, string, bool, string, ContainerLimits)"/>
        /// instead when this fixture is being added to a <see cref="ComposedFixture"/>.
        /// </note>
        /// </summary>
        /// <param name="settings">Optional Couchbase settings.</param>
        /// <param name="image">
        /// Optionally specifies the Couchbase container image.  This defaults to 
        /// <b>ghcr.io/neonrelease/couchbase-dev:latest</b> or <b>ghcr.io/neonrelease-dev/couchbase-dev:latest</b>
        /// depending on whether the assembly was built from a git release branch or not.
        /// </param>
        /// <param name="name">Optionally specifies the Couchbase container name (defaults to <c>cb-test</c>).</param>
        /// <param name="env">Optional environment variables to be passed to the Couchbase container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <param name="username">Optional Couchbase username (defaults to <b>Administrator</b>).</param>
        /// <param name="password">Optional Couchbase password (defaults to <b>password</b>).</param>
        /// <param name="noPrimary">Optionally disable creation of the primary bucket index.</param>
        /// <param name="hostInterface">
        /// Optionally specifies the host interface where the container public ports will be
        /// published.  This defaults to <see cref="ContainerFixture.DefaultHostInterface"/>
        /// but may be customized.  This needs to be an IPv4 address.
        /// </param>
        /// <param name="limits">
        /// Optionally specifies the Docker container limits to use for hosting Couchbase.  Note that
        /// this method will use reasonable default limits when this is <c>null</c>.
        /// </param>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> if the fixture wasn't previously started and
        /// this method call started it or <see cref="TestFixtureStatus.AlreadyRunning"/> if the 
        /// fixture was already running.
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
        public TestFixtureStatus Start(
            CouchbaseSettings   settings      = null,
            string              image         = null,
            string              name          = "cb-test",
            string[]            env           = null,
            string              username      = "Administrator",
            string              password      = "password",
            bool                noPrimary     = false,
            string              hostInterface = null,
            ContainerLimits     limits        = null)
        {
            return base.Start(
                () =>
                {
                    StartAsComposed(settings, image, name, env, username, password, noPrimary, hostInterface, limits);
                });
        }

        /// <summary>
        /// Used to start the fixture within a <see cref="ComposedFixture"/>.
        /// </summary>
        /// <param name="settings">Optional Couchbase settings.</param>
        /// <param name="image">
        /// Optionally specifies the Couchbase container image.  This defaults to 
        /// <b>ghcr.io/neonrelease/couchbase-dev:latest</b> or <b>ghcr.io/neonrelease-dev/couchbase-dev:latest</b>
        /// depending on whether the assembly was built from a git release branch or not.
        /// </param>
        /// <param name="name">Optionally specifies the Couchbase container name (defaults to <c>cb-test</c>).</param>
        /// <param name="env">Optional environment variables to be passed to the Couchbase container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <param name="username">Optional Couchbase username (defaults to <b>Administrator</b>).</param>
        /// <param name="password">Optional Couchbase password (defaults to <b>password</b>).</param>
        /// <param name="noPrimary">Optionally disable creation of thea primary bucket index.</param>
        /// <param name="hostInterface">
        /// Optionally specifies the host interface where the container public ports will be
        /// published.  This defaults to <see cref="ContainerFixture.DefaultHostInterface"/>
        /// but may be customized.  This needs to be an IPv4 address.
        /// </param>
        /// <param name="limits">
        /// Optionally specifies the Docker container limits to use for hosting Couchbase.  Note that
        /// this method will use reasonably small default limits when this is <c>null</c>.
        /// </param>
        public void StartAsComposed(
            CouchbaseSettings   settings      = null,
            string              image         = null,
            string              name          = "cb-test",
            string[]            env           = null,
            string              username      = "Administrator",
            string              password      = "password",
            bool                noPrimary     = false,
            string              hostInterface = null,
            ContainerLimits     limits        = null)
        {
            image = image ?? $"{NeonHelper.NeonLibraryBranchRegistry}/couchbase-dev:latest";

            base.CheckWithinAction();

            createPrimaryIndex = !noPrimary;

            if (!IsRunning)
            {
                // Use reasonable default limits.

                limits = limits ?? new ContainerLimits()
                {
                    Memory = "1 GiB"
                };

                base.StartAsComposed(name, image,
                    new string[]
                    {
                        "--detach",
                        "-p", $"{GetHostInterface(hostInterface)}:4369:4369",
                        "-p", $"{GetHostInterface(hostInterface)}:8091-8096:8091-8096",
                        "-p", $"{GetHostInterface(hostInterface)}:9100-9105:9100-9105",
                        "-p", $"{GetHostInterface(hostInterface)}:9110-9118:9110-9118",
                        "-p", $"{GetHostInterface(hostInterface)}:9120-9122:9120-9122",
                        "-p", $"{GetHostInterface(hostInterface)}:9999:9999",
                        "-p", $"{GetHostInterface(hostInterface)}:11207:11207",
                        "-p", $"{GetHostInterface(hostInterface)}:11209-11211:11209-11211",
                        "-p", $"{GetHostInterface(hostInterface)}:18091-18096:18091-18096",
                        "-p", $"{GetHostInterface(hostInterface)}:21100-21299:21100-21299"
                    },
                    env: env,
                    limits: limits);

                Thread.Sleep(warmupDelay);

                settings = settings ?? new CouchbaseSettings();

                settings.Servers.Clear();
                settings.Servers.Add(new Uri($"http://{GetHostInterface(hostInterface, forConnection: true)}:8091"));

                if (settings.Bucket == null)
                {
                    settings.Bucket = "test";
                }

                Bucket   = null;
                Settings = settings;
                Username = username;
                Password = password;

                jsonClient             = new JsonClient();
                jsonClient.BaseAddress = new Uri($"http://{GetHostInterface(hostInterface, forConnection: true)}:8094");
                jsonClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue(
                        "Basic",
                        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}")));
            }

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
        /// Returns the JsonClient.
        /// </summary>
        public JsonClient jsonClient { get; private set; }

        /// <summary>
        /// Establishes the bucket connection and waits until the Couchbase container is ready
        /// to start handling requests.
        /// </summary>
        private void ConnectBucket()
        {
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
            // service to start in the new container we started above.  We're going
            // to retry creating the primary index (or a dummy index) until it works.

            var bucket       = (NeonBucket)null;
            var indexCreated = false;
            var indexReady   = false;
            var queryReady   = false;
            var doRetry      = false;

            NeonBucket.ReadyRetry.InvokeAsync(
                async () =>
                {
                    if (doRetry)
                    {
                        Thread.Sleep(retryDelay);
                    }
                    else
                    {
                        doRetry = true;
                    }

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
                                await bucket.QuerySafeAsync<dynamic>($"create primary index on {CouchbaseHelper.LiteralName(bucket.Name)} using gsi");
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
                                var query = new QueryRequest($"select meta({bucket.Name}).id, {bucket.Name}.* from {bucket.Name}");

                                await bucket.QuerySafeAsync<dynamic>(query);

                                await bucket.ListIndexesAsync();

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
                        // $hack(jefflill):
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

                }).WaitWithoutAggregate();

            // Use the new bucket if this is the first Couchbase container initialization
            // or else substitute the new underlying bucket into the existing bucket so
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

            // Remove any full-text indexes.

            var fullTextIndexes = jsonClient.GetAsync<dynamic>("/api/index").Result.indexDefs;

            if (fullTextIndexes != null)
            {
                foreach (var _index in fullTextIndexes.indexDefs)
                {
                    jsonClient.DeleteAsync($"/api/index/{_index.Name}").WaitWithoutAggregate();
                }
            }

            // Remove all bucket indexes except for the primary index (if present).

            var existingIndexes = Bucket.ListIndexesAsync().Result;

            foreach (var index in existingIndexes.Where(index => index.Name != "#primary"))
            {
                Bucket.QuerySafeAsync<dynamic>($"drop index {CouchbaseHelper.LiteralName(Bucket.Name)}.{CouchbaseHelper.LiteralName(index.Name)} using {index.Type}").WaitWithoutAggregate();
            }

            // Flush the bucket data.

            using (var bucketManager = Bucket.CreateManager())
            {
                NeonBucket.ReadyRetry.InvokeAsync(
                    async () =>
                    {
                        bucketManager.Flush();
                        await Bucket.WaitUntilReadyAsync();

                    }).WaitWithoutAggregate();
            }

            // Wait until all of the indexes are actually deleted as well
            // as all of the bucket items.

            NeonHelper.WaitFor(
                () =>
                {
                    var indexes       = Bucket.ListIndexesAsync().Result;
                    var expectedCount = createPrimaryIndex ? 1 : 0;
                    var countResult   = Bucket.QuerySafeAsync<JObject>($"select count(*) from `{Bucket.Name}`;").Result.First();
                    var docCount      = (long)countResult["$1"];

                    return indexes.Count == expectedCount && docCount == 0;
                },
                timeout:      NeonBucket.ReadyTimeout,
                pollInterval: retryDelay);
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
