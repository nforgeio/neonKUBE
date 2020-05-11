//-----------------------------------------------------------------------------
// FILE:	    TemporalFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Neon.Temporal;
using Neon.Temporal.Internal;
using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Retry;
using Neon.Net;

using Newtonsoft.Json.Linq;
using Xunit;

namespace Neon.Xunit.Temporal
{
    /// <summary>
    /// Used to run Temporal server and it's related database and services as
    /// a Docker stack on  the current machine as a test fixture while tests 
    /// are being performed  and then deletes the stack when the fixture is
    /// disposed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This fixture assumes that Temporal is not currently running on the
    /// local workstation or as a stack named <b>temporal-dev</b>.
    /// You may see port conflict errors if either of these conditions
    /// are not true.
    /// </para>
    /// <para>
    /// See <see cref="Start(TemporalSettings, string, string, string, LogLevel, bool, bool, bool, bool)"/>
    /// for more information about how this works.
    /// </para>
    /// <note>
    /// You can persist <see cref="TemporalClient"/> instances to the underlying <see cref="TestFixture.State"/>
    /// dictionary to make these clients available across all test methods.  <see cref="TemporalFixture"/>
    /// ensures that any of these clients will be disposed when the fixture is disposed,
    /// reset, or restarted.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public sealed class TemporalFixture : StackFixture
    {
        /// <summary>
        /// The default Docker compose file text used to spin up Temporal and it's related services
        /// by the <see cref="TemporalFixture"/>.
        /// </summary>
        public const string DefaultStackDefinition =
@"version: '3.5'

services:
  cassandra:
    image: cassandra:3.11
    ports:
      - ""9042:9042""
  temporal:
    image: temporalio/auto-setup:0.21.1
    ports:
     - ""7233:7233""
    environment:
      - ""CASSANDRA_SEEDS=cassandra""
      - ""DYNAMIC_CONFIG_FILE_PATH=config/dynamicconfig/development.yaml""
    depends_on:
      - cassandra
  temporal-web:
    image: temporalio/web:0.21.1
    environment:
      - ""TEMPORAL_GRPC_ENDPOINT=temporal:7233""
    ports:
      - ""8088:8088""
    depends_on:
      - temporal
";

        private readonly TimeSpan   warmupDelay = TimeSpan.FromSeconds(2);      // Time to allow Temporal server to start.
        private TemporalSettings    settings;
        private TemporalClient      client;
        private bool                keepConnection;
        private bool                noReset;

        /// <summary>
        /// The default namespace configured for <see cref="TemporalFixture"/> clients.
        /// </summary>
        public const string DefaultNamespace = "test-namespace";

        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public TemporalFixture()
        {
        }

        /// <summary>
        /// <para>
        /// Starts a Temporal stack if it's not already running.  You'll generally want
        /// to call this in your test class constructor instead of <see cref="ITestFixture.Start(Action)"/>.
        /// </para>
        /// <note>
        /// You'll need to call <see cref="StartAsComposed(TemporalSettings, string, string, string, LogLevel, bool, bool, bool, bool)"/>
        /// instead when this fixture is being added to a <see cref="ComposedFixture"/>.
        /// </note>
        /// </summary>
        /// <param name="settings">Optional Temporal settings.</param>
        /// <param name="stackDefinition">
        /// <para>
        /// Optionally specifies the Temporal Docker compose file text.  This defaults to
        /// <see cref="DefaultStackDefinition"/> which configures Temporal server to start with
        /// a new Cassandra database instance listening on port <b>9042</b> as well as the
        /// Temporal web UI running on port <b>8088</b>.  Temporal server is listening on
        /// its standard gRPC port <b>7233</b>.
        /// </para>
        /// <para>
        /// You may specify your own Docker compose text file to customize this by configuring
        /// a different backend database, etc.
        /// </para>
        /// </param>
        /// <param name="name">Optionally specifies the Temporal stack name (defaults to <c>temporal-dev</c>).</param>
        /// <param name="defaultNamespace">Optionally specifies the default namespace for the fixture's client.  This defaults to <b>test-namespace</b>.</param>
        /// <param name="logLevel">Specifies the Temporal log level.  This defaults to <see cref="LogLevel.None"/>.</param>
        /// <param name="keepConnection">
        /// Optionally specifies that a new Temporal connection <b>should not</b> be established for each
        /// unit test case.  By default, the same connection will be reused which will save about a second per test.
        /// </param>
        /// <param name="keepOpen">
        /// Optionally indicates that the stack should remain running after the fixture is disposed.
        /// This is handy for using the Temporal web UI for port mortems after tests have completed.
        /// </param>
        /// <param name="noClient">
        /// Optionally disables establishing a client connection when <c>true</c>
        /// is passed.  The <see cref="Client"/> and <see cref="HttpClient"/> properties
        /// will be set to <c>null</c> in this case.
        /// </param>
        /// <param name="noReset">
        /// Optionally prevents the fixture from calling <see cref="TemporalClient.Reset()"/> to
        /// put the Temporal client library into its initial state before the fixture starts as well
        /// as when the fixture itself is reset.
        /// </param>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> if the fixture wasn't previously started and
        /// this method call started it or <see cref="TestFixtureStatus.AlreadyRunning"/> if the 
        /// fixture was already running.
        /// </returns>
        /// <remarks>
        /// <note>
        /// Some of the <paramref name="settings"/> properties will be ignored including 
        /// <see cref="TemporalSettings.HostPort"/>.  This will be replaced by the local
        /// endpoint for the Temporal stack.  Also, the fixture will connect to the 
        /// <b>default</b> Temporal namespace by default (unless another is specified).
        /// </note>
        /// <note>
        /// A fresh Temporal client <see cref="Client"/> will be established every time this
        /// fixture is started, regardless of whether the fixture has already been started.  This
        /// ensures that each unit test will start with a client in the default state.
        /// </note>
        /// </remarks>
        public TestFixtureStatus Start(
            TemporalSettings    settings         = null,
            string              stackDefinition  = DefaultStackDefinition,
            string              name             = "temporal-dev",
            string              defaultNamespace = DefaultNamespace,
            LogLevel            logLevel         = LogLevel.None,
            bool                keepConnection   = false,
            bool                keepOpen         = false,
            bool                noClient         = false,
            bool                noReset          = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(stackDefinition), nameof(stackDefinition));

            return base.Start(
                () =>
                {
                    StartAsComposed(
                        settings:         settings, 
                        stackDefinition:  stackDefinition, 
                        name:             name, 
                        defaultNamespace: defaultNamespace, 
                        logLevel:         logLevel,
                        keepConnection:   keepConnection,
                        keepOpen:         keepOpen, 
                        noClient:         noClient, 
                        noReset:          noReset);
                });
        }

        /// <summary>
        /// Used to start the fixture within a <see cref="ComposedFixture"/>.
        /// </summary>
        /// <param name="settings">Optional Temporal settings.</param>
        /// <param name="stackDefinition">
        /// <para>
        /// Optionally specifies the Temporal Docker compose file text.  This defaults to
        /// <see cref="DefaultStackDefinition"/> which configures Temporal server to start with
        /// a new Cassandra database instance listening on port <b>9042</b> as well as the
        /// Temporal web UI running on port <b>8088</b>.  Temporal server is listening on
        /// its standard gRPC port <b>7233</b>.
        /// </para>
        /// <para>
        /// You may specify your own Docker compose text file to customize this by configuring
        /// a different backend database, etc.
        /// </para>
        /// <param name="name">Optionally specifies the Temporal stack name (defaults to <c>cb-test</c>).</param>
        /// <param name="defaultNamespace">Optionally specifies the default namespace for the fixture's client.  This defaults to <b>test-namespace</b>.</param>
        /// <param name="logLevel">Specifies the Temporal log level.  This defaults to <see cref="LogLevel.None"/>.</param>
        /// <param name="keepConnection">
        /// Optionally specifies that a new Temporal connection <b>should not</b> be established for each
        /// unit test case.  The same connection will be reused which will save about a second per test.
        /// </param>
        /// <param name="keepOpen">
        /// Optionally indicates that the stack should remain running after the fixture is disposed.
        /// This is handy for using the Temporal web UI for port mortems after tests have completed.
        /// </param>
        /// <param name="noClient">
        /// Optionally disables establishing a client connection when <c>true</c>
        /// is passed.  The <see cref="Client"/> and <see cref="HttpClient"/> properties
        /// will be set to <c>null</c> in this case.
        /// </param>
        /// <param name="noReset">
        /// Optionally prevents the fixture from calling <see cref="TemporalClient.Reset()"/> to
        /// put the Temporal client library into its initial state before the fixture starts as well
        /// as when the fixture itself is reset.
        /// </param>
        /// <remarks>
        /// <note>
        /// A fresh Temporal client <see cref="Client"/> will be established every time this
        /// fixture is started, regardless of whether the fixture has already been started.  This
        /// ensures that each unit test will start with a client in the default state.
        /// </note>
        /// </remarks>
        public void StartAsComposed(
            TemporalSettings    settings         = null,
            string              stackDefinition  = DefaultStackDefinition,
            string              name             = "temporal-dev",
            string              defaultNamespace = DefaultNamespace,
            LogLevel            logLevel         = LogLevel.None,
            bool                keepConnection   = false,
            bool                keepOpen         = false,
            bool                noClient         = false,
            bool                noReset          = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(stackDefinition), nameof(stackDefinition));

            base.CheckWithinAction();

            if (!IsRunning)
            {
                // The old [cadence-dev] container started by [CadenceFixture] has conflicting ports
                // for Cassandra and the web UI, so we're going to stop that container if it's running.

                NeonHelper.Execute("docker", new object[] { "rm", "--force", "cadence-dev" });

                // Reset TemporalClient to its initial state.

                this.noReset = noReset;

                if (!noReset)
                {
                    TemporalClient.Reset();
                }

                // Start the Temporal stack.

                base.StartAsComposed(name, stackDefinition, keepOpen);
                Thread.Sleep(warmupDelay);

                // Initialize the settings.

                settings = settings ?? new TemporalSettings()
                {
                    HostPort         = $"127.0.0.1:{NetworkPorts.Temporal}",
                    CreateNamespace  = true,
                    DefaultNamespace = defaultNamespace,
                    LogLevel         = logLevel
                };

                this.settings       = settings;
                this.keepConnection = keepConnection;

                if (!noClient)
                {
                    // Establish the Temporal connection.

                    Client = TemporalClient.ConnectAsync(settings).Result;

                    HttpClient = new HttpClient()
                    {
                        BaseAddress = Client.ListenUri
                    };
                }
            }
        }

        /// <summary>
        /// Returns the settings used to connect to the Temporal cluster.
        /// </summary>
        public TemporalSettings Settings => settings;

        /// <summary>
        /// Returns the <see cref="TemporalClient"/> to be used to interact with Temporal.
        /// </summary>
        public TemporalClient Client
        {
            get
            {
                if (client == null)
                {
                    throw new Exception("Temporal client could not be connected to the Temporal cluster.");
                }

                return client;
            }

            set => client = value;
        }

        /// <summary>
        /// Returns a <see cref="System.Net.Http.HttpClient"/> suitable for submitting requests to the
        /// <see cref="HttpClient"/> instance web server.
        /// </summary>
        public HttpClient HttpClient { get; private set; }

        /// <summary>
        /// <para>
        /// Returns a <see cref="System.Net.Http.HttpClient"/> suitable for submitting requests to the
        /// associated <b>temporal-proxy</b> process.
        /// </para>
        /// <note>
        /// This will return <c>null</c> if the <b>temporal-proxy</b> process was disabled by
        /// the settings.
        /// </note>
        /// </summary>
        public HttpClient ProxyClient { get; private set; }

        /// <summary>
        /// Closes the existing Temporal connection and restarts the Cadence
        /// server and then establishes a new connection.
        /// </summary>
        public new void Restart()
        {
            // Disconnect.

            Client.Dispose();
            Client = null;

            HttpClient.Dispose();
            HttpClient = null;

            // Restart the Temporal stack.

            base.Restart();

            // Reconnect.

            Client = TemporalClient.ConnectAsync(settings).Result;

            HttpClient = new HttpClient()
            {
                BaseAddress = Client.ListenUri
            };
        }

        /// <summary>
        /// This method completely resets the fixture by removing the Temporal 
        /// stack from Docker.  Use <see cref="StackFixture.Restart"/> 
        /// if you just want to restart a fresh Temporal instance.
        /// </summary>
        public override void Reset()
        {
            if (!noReset)
            {
                TemporalClient.Reset();
            }

            if (Client != null)
            {
                Client.Dispose();
                Client = null;
            }

            // $hack(jefflill): 
            //
            // We're also going to dispose any clients saved in the
            // State dictionary because some Temporal unit tests 
            // persist client instances there.

            foreach (var value in State.Values
                .Where(v => v != null && v.GetType() == typeof(TemporalClient)))
            {
                var client = (TemporalClient)value;

                client.Dispose();
            }

            if (HttpClient != null)
            {
                HttpClient.Dispose();
                HttpClient = null;
            }

            if (ProxyClient != null)
            {
                ProxyClient.Dispose();
                ProxyClient = null;
            }

            base.Reset();
        }

        /// <summary>
        /// Called when an already started fixture is being restarted.  This 
        /// establishes a fresh Temporal connection.
        /// </summary>
        public override void OnRestart()
        {
            if (keepConnection)
            {
                // We're going to continue using the same connection.

                return;
            }

            // Close any existing connection related objects.

            if (Client != null)
            {
                Client.Dispose();
                Client = null;
            }

            if (HttpClient != null)
            {
                HttpClient.Dispose();
                HttpClient = null;
            }

            // Establish fresh connections.

            Client = TemporalClient.ConnectAsync(settings).Result;

            HttpClient = new HttpClient()
            {
                BaseAddress = Client.ListenUri
            };
        }
    }
}
