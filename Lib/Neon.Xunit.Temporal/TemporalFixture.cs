//-----------------------------------------------------------------------------
// FILE:	    TemporalFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
    /// Used to run Temporal server and its related database and services as
    /// a Docker compose application on  the current machine as a test fixture while tests 
    /// are being performed  and then deletes the application when the fixture is
    /// disposed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This fixture assumes that Temporal is not currently running on the
    /// local workstation or is running as as a application named <b>temporal-dev</b>.
    /// You may see port conflict errors if either of these conditions
    /// are not true.
    /// </para>
    /// <para>
    /// See <see cref="Start(TemporalSettings, string, string, string, Neon.Diagnostics.LogLevel, bool, bool, bool, bool)"/>
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
    public sealed class TemporalFixture : DockerComposeFixture
    {
        /// <summary>
        /// The default Docker compose file text used to spin up Temporal and its related services
        /// by the <see cref="TemporalFixture"/>.
        /// </summary>
        public const string DefaultComposeFile =
@"version: '3.5'

# IMPORTANT NOTE: 
#
# We're setting the Cassandra container memory limit to 1024M below and
# then passing the [MAX_HEAP_SIZE=924M] environment variable which tells
# Cassandra how much memory it can allocate.  This number is 100M lower
# than the container limit to account for additional RAM overhead for
# Bash, etc.
#
# This means that you'll need to adjust [MAX_HEAP_SIZE] whenever you
# change the memory limit.

services:
  cassandra:
    image: ghcr.io/neonrelease-dev/cassandra:3.11.10
    environment:
      - HEAP_NEWSIZE=1M
      - MAX_HEAP_SIZE=1000M
    deploy:
      resources:
        limits:
          memory: 1024M
  temporal:
    image: temporalio/auto-setup:1.1.0
    ports:
      - '7233-7235:7233-7235'
    environment:
      - 'CASSANDRA_SEEDS=cassandra'
      - 'DYNAMIC_CONFIG_FILE_PATH=config/dynamicconfig/development.yaml'
    depends_on:
      - cassandra
  temporal-web:
    image: temporalio/web:1.1.0
    environment:
      - 'TEMPORAL_GRPC_ENDPOINT=temporal:7233'
      - 'TEMPORAL_PERMIT_WRITE_API=true'
    ports:
      - '8088:8088'
    depends_on:
      - temporal
";
        private TemporalSettings    settings;
        private TemporalClient      client;
        private bool                reconnect;
        private bool                noReset;

        /// <summary>
        /// The default namespace configured for <see cref="TemporalFixture"/> clients.
        /// </summary>
        public const string Namespace = "test-namespace";

        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public TemporalFixture()
        {
        }

        /// <summary>
        /// <para>
        /// Starts a Temporal compose application if it's not already running.  You'll generally want
        /// to call this in your test class constructor instead of <see cref="ITestFixture.Start(Action)"/>.
        /// </para>
        /// <note>
        /// You'll need to call <see cref="StartAsComposed(TemporalSettings, string, string, string, Neon.Diagnostics.LogLevel, bool, bool, bool, bool)"/>
        /// instead when this fixture is being added to a <see cref="ComposedFixture"/>.
        /// </note>
        /// </summary>
        /// <param name="settings">Optional Temporal settings.</param>
        /// <param name="name">Optionally specifies the Docker compose application name (defaults to <c>temporal-dev</c>).</param>
        /// <param name="composeFile">
        /// <para>
        /// Optionally specifies the Temporal Docker compose file text.  This defaults to
        /// <see cref="DefaultComposeFile"/> which configures Temporal server to start with
        /// a new Cassandra database instance listening on port <b>9042</b> as well as the
        /// Temporal web UI running on port <b>8088</b>.  Temporal server is listening on
        /// its standard gRPC port <b>7233</b>.
        /// </para>
        /// <para>
        /// You may specify your own Docker compose text file to customize this by configuring
        /// a different backend database, etc.
        /// </para>
        /// </param>
        /// <param name="defaultNamespace">Optionally specifies the default namespace for the fixture's client.  This defaults to <b>test-namespace</b>.</param>
        /// <param name="logLevel">Specifies the Temporal log level.  This defaults to <see cref="Neon.Diagnostics.LogLevel.None"/>.</param>
        /// <param name="reconnect">
        /// Optionally specifies that a new Temporal connection <b>should</b> be established for each
        /// unit test case.  By default, the same connection will be reused which will save about a 
        /// second per test case.
        /// </param>
        /// <param name="keepRunning">
        /// Optionally indicates that the application should remain running after the fixture is disposed.
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
        /// endpoint for the Temporal application.  Also, the fixture will connect to the 
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
            string              name             = "temporal-dev",
            string              composeFile      = DefaultComposeFile,
            string              defaultNamespace = Namespace,
            LogLevel            logLevel         = LogLevel.None,
            bool                reconnect        = false,
            bool                keepRunning      = false,
            bool                noClient         = false,
            bool                noReset          = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(composeFile), nameof(composeFile));

            return base.Start(
                () =>
                {
                    StartAsComposed(
                        settings:         settings, 
                        name:             name, 
                        composeFile:      composeFile, 
                        defaultNamespace: defaultNamespace, 
                        logLevel:         logLevel,
                        reconnect:        reconnect,
                        keepRunning:      keepRunning, 
                        noClient:         noClient, 
                        noReset:          noReset);
                });
        }

        /// <summary>
        /// Used to start the fixture within a <see cref="ComposedFixture"/>.
        /// </summary>
        /// <param name="settings">Optional Temporal settings.</param>
        /// <param name="name">Optionally specifies the Docker compose application name (defaults to <c>temporal-dev</c>).</param>
        /// <param name="composeFile">
        /// <para>
        /// Optionally specifies the Temporal Docker compose file text.  This defaults to
        /// <see cref="DefaultComposeFile"/> which configures Temporal server to start with
        /// a new Cassandra database instance listening on port <b>9042</b> as well as the
        /// Temporal web UI running on port <b>8088</b>.  Temporal server is listening on
        /// its standard gRPC port <b>7233</b>.
        /// </para>
        /// <para>
        /// You may specify your own Docker compose text file to customize this by configuring
        /// a different backend database, etc.
        /// </para>
        /// </param>
        /// <param name="defaultNamespace">Optionally specifies the default namespace for the fixture's client.  This defaults to <b>test-namespace</b>.</param>
        /// <param name="logLevel">Specifies the Temporal log level.  This defaults to <see cref="Neon.Diagnostics.LogLevel.None"/>.</param>
        /// <param name="reconnect">
        /// Optionally specifies that a new Temporal connection <b>should</b> be established for each
        /// unit test case.  By default, the same connection will be reused which will save about a 
        /// second per test.
        /// </param>
        /// <param name="keepRunning">
        /// Optionally indicates that the compose application should remain running after the fixture is disposed.
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
            string              name             = "temporal-dev",
            string              composeFile      = DefaultComposeFile,
            string              defaultNamespace = Namespace,
            LogLevel            logLevel         = LogLevel.None,
            bool                reconnect        = false,
            bool                keepRunning      = false,
            bool                noClient         = false,
            bool                noReset          = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(composeFile), nameof(composeFile));

            base.CheckWithinAction();

            if (!IsRunning)
            {
                // The [cadence-dev] container started by [CadenceFixture] has conflicting ports
                // for Cassandra and the web UI, so we're going to stop that container if it's running.

                NeonHelper.Execute(NeonHelper.DockerCli, new object[] { "rm", "--force",
                    new string[]
                    {
                        "cadence-dev_cadence_1",
                        "cadence-dev_cadence-web_1",
                        "cadence-dev_cassandra_1",
                        "cadence-dev_statsd_1"
                    } });

                // Reset TemporalClient to its initial state.

                this.noReset = noReset;

                if (!noReset)
                {
                    TemporalClient.Reset();
                }

                // Start the Temporal Docker compose application.

                base.StartAsComposed(name, composeFile, keepRunning);

                // It can take Temporal server some time to start.  Rather than relying on [temporal-proxy]
                // to handle retries (which may take longer than the connect timeout), we're going to wait
                // up to 4 minutes for Temporal to start listening on its RPC socket.

                var retry = new LinearRetryPolicy(e => true, maxAttempts: int.MaxValue, retryInterval: TimeSpan.FromSeconds(0.5), timeout: TimeSpan.FromMinutes(4));

                retry.Invoke(
                    () =>
                    {
                        // The [socket.Connect()] calls below will throw [SocketException] until
                        // Temporal starts listening on its RPC socket.

                        var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

                        socket.Connect(IPAddress.IPv6Loopback, NetworkPorts.Temporal);
                        socket.Close();
                    });

                Thread.Sleep(TimeSpan.FromSeconds(5));  // Wait a bit longer for luck!

                // Initialize the settings.

                settings = settings ?? new TemporalSettings()
                {
                    HostPort        = $"localhost:{NetworkPorts.Temporal}",
                    CreateNamespace = true,
                    Namespace       = defaultNamespace,
                    ProxyLogLevel   = logLevel,
                };

                this.settings  = settings;
                this.reconnect = reconnect;

                if (!noClient)
                {
                    // Establish the Temporal connection.

                    Client     = TemporalClient.ConnectAsync(settings).Result;
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
                    throw new Exception("Temporal client could connect to the Temporal cluster.");
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
        /// Closes the existing Temporal connection and restarts the Temporal
        /// server and then establishes a new connection.
        /// </summary>
        public new void Restart()
        {
            // Disconnect.

            Client.Dispose();
            Client = null;

            HttpClient.Dispose();
            HttpClient = null;

            // Restart the Temporal Docker compose application.

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
        /// compose application from Docker.  Use <see cref="DockerComposeFixture.Restart"/> 
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
            // We're also going to dispose/remove any clients saved in the
            // State dictionary because some Temporal unit tests 
            // persist client instances there.

            var delList = new List<string>();

            foreach (var item in State
                .Where(item => item.Value.GetType() == typeof(TemporalClient)))
            {
                var client = (TemporalClient)item.Value;

                client.Dispose();
                delList.Add(item.Key);
            }

            foreach (var key in delList)
            {
                State.Remove(key);
            }

            // Dispose the clients.

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
            if (reconnect)
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
