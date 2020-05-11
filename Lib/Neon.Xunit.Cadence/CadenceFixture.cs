//-----------------------------------------------------------------------------
// FILE:	    CadenceFixture.cs
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

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Retry;
using Neon.Net;

using Newtonsoft.Json.Linq;
using Xunit;

namespace Neon.Xunit.Cadence
{
    /// <summary>
    /// Used to run the Docker <b>nkubeio/cadence-dev</b> container on 
    /// the current machine as a test fixture while tests are being performed 
    /// and then deletes the container when the fixture is disposed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This fixture assumes that Cadence is not currently running on the
    /// local workstation or as a container named <b>cadence-dev</b>.
    /// You may see port conflict errors if either of these conditions
    /// are not true.
    /// </para>
    /// <para>
    /// See <see cref="Start(CadenceSettings, string, string, string[], string, LogLevel, bool, string, bool, bool, bool)"/>
    /// for more information about how this works.
    /// </para>
    /// <note>
    /// You can persist <see cref="CadenceClient"/> instances to the underlying <see cref="TestFixture.State"/>
    /// dictionary to make these clients available across all test methods.  <see cref="CadenceFixture"/>
    /// ensures that any of these clients will be disposed when the fixture is disposed,
    /// reset, or restarted.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public sealed class CadenceFixture : ContainerFixture
    {
        private readonly TimeSpan   warmupDelay = TimeSpan.FromSeconds(2);      // Time to allow Cadence to start.
        private CadenceSettings     settings;
        private CadenceClient       client;
        private bool                reconnect;
        private bool                noReset;

        /// <summary>
        /// The default domain configured for <see cref="CadenceFixture"/> clients.
        /// </summary>
        public const string DefaultDomain = "test-domain";

        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public CadenceFixture()
        {
        }

        /// <summary>
        /// <para>
        /// Starts a Cadence container if it's not already running.  You'll generally want
        /// to call this in your test class constructor instead of <see cref="ITestFixture.Start(Action)"/>.
        /// </para>
        /// <note>
        /// You'll need to call <see cref="StartAsComposed(CadenceSettings, string, string, string[], string, LogLevel, bool, bool, string, bool, bool)"/>
        /// instead when this fixture is being added to a <see cref="ComposedFixture"/>.
        /// </note>
        /// </summary>
        /// <param name="settings">Optional Cadence settings.</param>
        /// <param name="image">Optionally specifies the Cadence container image (defaults to <b>nkubeio/cadence-dev:latest</b>).</param>
        /// <param name="name">Optionally specifies the Cadence container name (defaults to <c>cadence-dev</c>).</param>
        /// <param name="env">Optional environment variables to be passed to the Cadence container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <param name="defaultDomain">Optionally specifies the default domain for the fixture's client.  This defaults to <b>test-domain</b>.</param>
        /// <param name="logLevel">Specifies the Cadence log level.  This defaults to <see cref="LogLevel.None"/>.</param>
        /// <param name="reconnect">
        /// Optionally specifies that a new Cadence connection <b>should</b> be established for each
        /// unit test case.  By default, the same connection will be reused which will save about a 
        /// second per test.
        /// </param>
        /// <param name="keepRunning">
        /// Optionally indicates that the container should remain running after the fixture is disposed.
        /// This is handy for using the Temporal web UI for port mortems after tests have completed.
        /// </param>
        /// <param name="hostInterface">
        /// Optionally specifies the host interface where the container public ports will be
        /// published.  This defaults to <see cref="ContainerFixture.DefaultHostInterface"/>
        /// but may be customized.  This needs to be an IPv4 address.
        /// </param>
        /// <param name="noClient">
        /// Optionally disables establishing a client connection when <c>true</c>
        /// is passed.  The <see cref="Client"/> and <see cref="HttpClient"/> properties
        /// will be set to <c>null</c> in this case.
        /// </param>
        /// <param name="noReset">
        /// Optionally prevents the fixture from calling <see cref="CadenceClient.Reset()"/> to
        /// put the Cadence client library into its initial state before the fixture starts as well
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
        /// <see cref="CadenceSettings.Servers"/>.  This will be replaced by the local
        /// endpoint for the Cadence container.  Also, the fixture will connect to the 
        /// <b>default</b> Cadence domain by default (unless another is specified).
        /// </note>
        /// <note>
        /// A fresh Cadence client <see cref="Client"/> will be established every time this
        /// fixture is started, regardless of whether the fixture has already been started.  This
        /// ensures that each unit test will start with a client in the default state.
        /// </note>
        /// </remarks>
        public TestFixtureStatus Start(
            CadenceSettings     settings      = null,
            string              image         = "nkubeio/cadence-dev:latest",
            string              name          = "cadence-dev",
            string[]            env           = null,
            string              defaultDomain = DefaultDomain,
            LogLevel            logLevel      = LogLevel.None,
            bool                reconnect     = false,
            string              hostInterface = null,
            bool                keepRunning   = false,
            bool                noClient      = false,
            bool                noReset       = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(image), nameof(image));

            return base.Start(
                () =>
                {
                    StartAsComposed(
                        settings:        settings, 
                        image:           image, 
                        name:            name, 
                        env:             env, 
                        defaultDomain:   defaultDomain, 
                        logLevel:        logLevel,
                        reconnect:       reconnect,
                        keepRunning:     keepRunning, 
                        hostInterface:   hostInterface,
                        noClient:        noClient, 
                        noReset:         noReset);
                });
        }

        /// <summary>
        /// Used to start the fixture within a <see cref="ComposedFixture"/>.
        /// </summary>
        /// <param name="settings">Optional Cadence settings.</param>
        /// <param name="image">Optionally specifies the Cadence container image (defaults to <b>nkubeio/cadence-dev:latest</b>).</param>
        /// <param name="name">Optionally specifies the Cadence container name (defaults to <c>cb-test</c>).</param>
        /// <param name="env">Optional environment variables to be passed to the Cadence container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <param name="defaultDomain">Optionally specifies the default domain for the fixture's client.  This defaults to <b>test-domain</b>.</param>
        /// <param name="logLevel">Specifies the Cadence log level.  This defaults to <see cref="LogLevel.None"/>.</param>
        /// <param name="reconnect">
        /// Optionally specifies that a new Cadence connection <b>should</b> be established for each
        /// unit test case.  By default, the same connection will be reused which will save about a 
        /// second per test.
        /// </param>
        /// <param name="keepRunning">
        /// Optionally indicates that the container should remain running after the fixture is disposed.
        /// This is handy for using the Temporal web UI for port mortems after tests have completed.
        /// </param>
        /// <param name="hostInterface">
        /// Optionally specifies the host interface where the container public ports will be
        /// published.  This defaults to <see cref="ContainerFixture.DefaultHostInterface"/>
        /// but may be customized.  This needs to be an IPv4 address.
        /// </param>
        /// <param name="noClient">
        /// Optionally disables establishing a client connection when <c>true</c>
        /// is passed.  The <see cref="Client"/> and <see cref="HttpClient"/> properties
        /// will be set to <c>null</c> in this case.
        /// </param>
        /// <param name="noReset">
        /// Optionally prevents the fixture from calling <see cref="CadenceClient.Reset()"/> to
        /// put the Cadence client library into its initial state before the fixture starts as well
        /// as when the fixture itself is reset.
        /// </param>
        /// <remarks>
        /// <note>
        /// A fresh Cadence client <see cref="Client"/> will be established every time this
        /// fixture is started, regardless of whether the fixture has already been started.  This
        /// ensures that each unit test will start with a client in the default state.
        /// </note>
        /// </remarks>
        public void StartAsComposed(
            CadenceSettings     settings      = null,
            string              image         = "nkubeio/cadence-dev:latest",
            string              name          = "cadence-dev",
            string[]            env           = null,
            string              defaultDomain = DefaultDomain,
            LogLevel            logLevel      = LogLevel.None,
            bool                reconnect     = false,
            bool                keepRunning   = false,
            string              hostInterface = null,
            bool                noClient      = false,
            bool                noReset       = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(image), nameof(image));

            base.CheckWithinAction();

            if (!IsRunning)
            {
                if (string.IsNullOrEmpty(hostInterface))
                {
                    hostInterface = ContainerFixture.DefaultHostInterface;
                }
                else
                {
                    Covenant.Requires<ArgumentException>(IPAddress.TryParse(hostInterface, out var address) && address.AddressFamily == AddressFamily.InterNetwork, nameof(hostInterface), $"[{hostInterface}] is not a valid IPv4 address.");
                }

                // Reset CadenceClient to its initial state.

                this.noReset = noReset;

                if (!noReset)
                {
                    CadenceClient.Reset();
                }

                // [TemporalFixture] deploys a stack that exposes some of the same
                // ports as [CadenceFixture], so we're going ensure that any running
                // [TemporalFixture] stack is stopped first.
                //
                // Most users won't run into this because Cadence eventually will be
                // depreciated but neonKUBE unit tests will require this.

                NeonHelper.Execute("docker",
                    new string[]
                    {
                        "stack",
                        "rm",
                        "temporal-dev"
                    });

                // Start the Cadence container.

                base.StartAsComposed(name, image,
                    new string[]
                    {
                        "--detach",
                        "-p", $"{GetHostInterface(hostInterface)}:7933-7939:7933-7939",
                        "-p", $"{GetHostInterface(hostInterface)}:8088:8088"
                    },
                    env: env,
                    keepOpen: keepRunning);

                Thread.Sleep(warmupDelay);

                // Initialize the settings.

                settings = settings ?? new CadenceSettings()
                {
                    CreateDomain  = true,
                    DefaultDomain = defaultDomain,
                    LogLevel      = logLevel
                };

                settings.Servers.Clear();
                settings.Servers.Add($"http://{GetHostInterface(hostInterface, forConnection: true)}:{NetworkPorts.Cadence}");

                this.settings  = settings;
                this.reconnect = reconnect;

                if (!noClient)
                {
                    // Establish the Cadence connection.

                    Client = CadenceClient.ConnectAsync(settings).Result;

                    HttpClient = new HttpClient()
                    {
                        BaseAddress = Client.ListenUri
                    };
                }
            }
        }

        /// <summary>
        /// Returns the settings used to connect to the Cadence cluster.
        /// </summary>
        public CadenceSettings Settings => settings;

        /// <summary>
        /// Returns the <see cref="CadenceClient"/> to be used to interact with Cadence.
        /// </summary>
        public CadenceClient Client
        {
            get
            {
                if (client == null)
                {
                    throw new Exception("Cadence client could not be connected to the Cadence cluster.");
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
        /// associated <b>cadence-proxy</b> process.
        /// </para>
        /// <note>
        /// This will return <c>null</c> if the <b>cadence-proxy</b> process was disabled by
        /// the settings.
        /// </note>
        /// </summary>
        public HttpClient ProxyClient { get; private set; }

        /// <summary>
        /// Closes the existing Cadence connection and restarts the Cadence
        /// server and then establishes a new connection.
        /// </summary>
        public new void Restart()
        {
            // Disconnect.

            Client.Dispose();
            Client = null;

            HttpClient.Dispose();
            HttpClient = null;

            // Restart the Cadence container.

            base.Restart();

            // Reconnect.

            Client = CadenceClient.ConnectAsync(settings).Result;

            HttpClient = new HttpClient()
            {
                BaseAddress = Client.ListenUri
            };
        }

        /// <summary>
        /// This method completely resets the fixture by removing the Cadence 
        /// container from Docker.  Use <see cref="ContainerFixture.Restart"/> 
        /// if you just want to restart a fresh Cadence instance.
        /// </summary>
        public override void Reset()
        {
            if (!noReset)
            {
                CadenceClient.Reset();
            }

            if (Client != null)
            {
                Client.Dispose();
                Client = null;
            }

            // $hack(jefflill): 
            //
            // We're also going to dispose any clients saved in the
            // State dictionary because some Cadence unit tests 
            // persist client instances there.

            foreach (var value in State.Values
                .Where(v => v != null && v.GetType() == typeof(CadenceClient)))
            {
                var client = (CadenceClient)value;

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
        /// establishes a fresh Cadence connection.
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

            Client = CadenceClient.ConnectAsync(settings).Result;

            HttpClient = new HttpClient()
            {
                BaseAddress = Client.ListenUri
            };
        }
    }
}
