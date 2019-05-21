//-----------------------------------------------------------------------------
// FILE:	    CadenceFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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

using Neon.Cadence;
using Neon.Common;
using Neon.Data;
using Neon.Retry;
using Neon.Net;

using Newtonsoft.Json.Linq;
using Xunit;

namespace Neon.Xunit.Cadence
{
    /// <summary>
    /// Used to run the Docker <b>nkubeio.cadence-test</b> container on 
    /// the current machine as a test fixture while tests are being performed 
    /// and then deletes the container when the fixture is disposed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This fixture assumes that Cadence is not currently running on the
    /// local workstation or as a container named <b>cadence-test</b>.
    /// You may see port conflict errors if either of these conditions
    /// are not true.
    /// </para>
    /// <para>
    /// A somewhat safer but slower alternative, is to use the <see cref="DockerFixture"/>
    /// instead and add <see cref="CadenceFixture"/> as a subfixture.  The 
    /// advantage is that <see cref="DockerFixture"/> will ensure that all
    /// (potentially conflicting) containers are removed before the Cadence
    /// fixture is started.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public sealed class CadenceFixture : ContainerFixture
    {
        private readonly TimeSpan   warmupDelay = TimeSpan.FromSeconds(2);      // Time to allow Cadence to start.
        private readonly TimeSpan   retryDelay  = TimeSpan.FromSeconds(0.5);    // Time to wait after a failure.
        private CadenceSettings     settings;

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
        /// You'll need to call <see cref="StartAsComposed(CadenceSettings, string, string, string[], bool)"/>
        /// instead when this fixture is being added to a <see cref="ComposedFixture"/>.
        /// </note>
        /// </summary>
        /// <param name="settings">Optional Cadence settings.</param>
        /// <param name="image">Optionally specifies the Cadence container image (defaults to <b>nkubeio/couchbase-test:latest</b>).</param>
        /// <param name="name">Optionally specifies the Cadence container name (defaults to <c>cadence-test</c>).</param>
        /// <param name="env">Optional environment variables to be passed to the Cadence container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <param name="emulateProxy">
        /// <b>INTERNAL USE ONLY:</b> Optionally starts a partially functional integrated 
        /// <b>cadence-proxy</b> for low-level testing.  Most users should never enable this
        /// because it's probably not going to do what you expect.
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
        /// <b>test</b> bucket by default (unless another is specified).
        /// </note>
        /// </remarks>
        public TestFixtureStatus Start(
            CadenceSettings     settings     = null,
            string              image        = "nkubeio/cadence-test:latest",
            string              name         = "cadence-test",
            string[]            env          = null,
            bool                emulateProxy = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(image));

            return base.Start(
                () =>
                {
                    StartAsComposed(settings, image, name, env, emulateProxy);
                });
        }

        /// <summary>
        /// Used to start the fixture within a <see cref="ComposedFixture"/>.
        /// </summary>
        /// <param name="settings">Optional Cadence settings.</param>
        /// <param name="image">Optionally specifies the Cadence container image (defaults to <b>nkubeio/cadence-test:latest</b>).</param>
        /// <param name="name">Optionally specifies the Cadence container name (defaults to <c>cb-test</c>).</param>
        /// <param name="env">Optional environment variables to be passed to the Cadence container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <param name="emulateProxy">
        /// <b>INTERNAL USE ONLY:</b> Optionally starts a partially functional integrated 
        /// <b>cadence-proxy</b> for low-level testing.  Most users should never enable this
        /// because it's probably not going to do what you expect.
        /// </param>
        public void StartAsComposed(
            CadenceSettings     settings     = null,
            string              image        = "nkubeio/cadence-test:latest",
            string              name         = "cadence-test",
            string[]            env          = null,
            bool                emulateProxy = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(image));

            base.CheckWithinAction();

            if (!IsRunning)
            {
                base.StartAsComposed(name, image,
                    new string[]
                    {
                        "--detach",
                        "-p", "7933-7939:7933-7939"
                    },
                    env: env);

                Thread.Sleep(warmupDelay);

                // Initialize the settings.

                settings = settings ?? new CadenceSettings();

                settings.Servers.Clear();
                settings.Servers.Add($"http://localhost:{NetworkPorts.Cadence}");

                settings.DebugEmulateProxy = emulateProxy || settings.DebugEmulateProxy;

                this.settings = settings;

                // Create the Cadence connection.

                Connection = CadenceClient.ConnectAsync(settings).Result;

                ConnectionClient = new HttpClient()
                {
                     BaseAddress = Connection.ListenUri
                };
            }
        }

        /// <summary>
        /// Returns the <see cref="CadenceClient"/> to be used to interact with Cadence.
        /// </summary>
        public CadenceClient Connection { get; private set; }

        /// <summary>
        /// Returns a <see cref="HttpClient"/> suitable for submitting requests to the
        /// <see cref="ConnectionClient"/> instance web server.
        /// </summary>
        public HttpClient ConnectionClient { get; private set; }

        /// <summary>
        /// <para>
        /// Returns a <see cref="HttpClient"/> suitable for submitting requests to the
        /// associated <b>cadence-proxy</b> process.
        /// </para>
        /// <note>
        /// This will return <c>null</c> if the <b>cadence-proxy</b> process was disabled by
        /// the settings.
        /// </note>
        /// </summary>
        public HttpClient ProxyClient { get; private set; }

        /// <summary>
        /// Closes the existing Cadence connection and then restarts the Cadence
        /// server and establishes a new connection.
        /// </summary>
        public new void Restart()
        {
            // Disconnect.

            Connection.Dispose();
            Connection = null;

            ConnectionClient.Dispose();
            ConnectionClient = null;

            // Restart the Cadence container.

            base.Restart();

            // Reconnect.

            Connection = CadenceClient.ConnectAsync(settings).Result;

            ConnectionClient = new HttpClient()
            {
                BaseAddress = Connection.ListenUri
            };
        }

        /// <summary>
        /// This method completely resets the fixture by removing the Cadence 
        /// container from Docker.  Use <see cref="ContainerFixture.Restart"/> 
        /// if you just want to restart a fresh Cadence instance.
        /// </summary>
        public override void Reset()
        {
            if (Connection != null)
            {
                Connection.Dispose();
                Connection = null;
            }

            if (ConnectionClient != null)
            {
                ConnectionClient.Dispose();
                ConnectionClient = null;
            }

            if (ProxyClient != null)
            {
                ProxyClient.Dispose();
                ProxyClient = null;
            }

            base.Reset();
        }
    }
}
