//-----------------------------------------------------------------------------
// FILE:	    NatsFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.

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

using NATS.Client;

using Newtonsoft.Json.Linq;
using Xunit;

using Neon.Common;
using Neon.Data;
using Neon.Kube;
using Neon.Retry;
using Neon.Net;

namespace Neon.Xunit
{
    /// <summary>
    /// Used to run a Docker <b>nats</b> container on the current 
    /// machine as a test fixture while tests are being performed and 
    /// then deletes the container when the fixture is disposed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This fixture assumes that NATS is not currently running on the
    /// local workstation or as a container named <b>nats-test</b>.
    /// You may see port conflict errors if either of these conditions 
    /// are not true.
    /// </para>
    /// <para>
    /// A somewhat safer but slower alternative, is to use the <see cref="DockerFixture"/>
    /// instead and add <see cref="NatsFixture"/> as a subfixture.  The 
    /// advantage is that <see cref="DockerFixture"/> will ensure that all
    /// (potentially conflicting) containers are removed before the NatsFixture
    /// fixture is started.
    /// </para>
    /// <para>
    /// Use <see cref="Restart"/> to clear the NATS server state by restarting
    /// its Docker container.  This also returns the new client connection.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public sealed class NatsFixture : ContainerFixture
    {
        /// <summary>
        /// Returns the URI for a NATS server running locally (probably as a Docker container).
        /// </summary>
        public const string ConnectionUri = "nats://localhost:4222";

        private string hostInterface;

        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public NatsFixture()
        {
        }

        /// <summary>
        /// Returns the NATS connection.
        /// </summary>
        public IConnection Connection { get; private set; }

        /// <summary>
        /// <para>
        /// Starts a NATS container if it's not already running.  You'll generally want
        /// to call this in your test class constructor instead of <see cref="ITestFixture.Start(Action)"/>.
        /// </para>
        /// <note>
        /// You'll need to call <see cref="StartAsComposed(string, string, string[], string)"/>
        /// instead when this fixture is being added to a <see cref="ComposedFixture"/>.
        /// </note>
        /// </summary>
        /// <param name="image">
        /// Optionally specifies the NATS container image.  This defaults to 
        /// <b>nkubeio/nats:latest</b> or <b>nkubedev/nats:latest</b> depending 
        /// on whether the assembly was built from a git release branch or not.
        /// </param>
        /// <param name="name">Optionally specifies the NATS container name (defaults to <c>nats-test</c>).</param>
        /// <param name="args">Optional NATS server command line arguments.</param>
        /// <param name="hostInterface">
        /// Optionally specifies the host interface where the container public ports will be
        /// published.  This defaults to <see cref="ContainerFixture.DefaultHostInterface"/>
        /// but may be customized.  This needs to be an IPv4 address.
        /// </param>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> if the fixture wasn't previously started and
        /// this method call started it or <see cref="TestFixtureStatus.AlreadyRunning"/> if the 
        /// fixture was already running.
        /// </returns>
        public TestFixtureStatus Start(
            string   image         = null,
            string   name          = "nats-test",
            string[] args          = null,
            string   hostInterface = null)
        {
            base.CheckDisposed();

            return base.Start(
                () =>
                {
                    StartAsComposed(image, name, args, hostInterface);
                });
        }

        /// <summary>
        /// Used to start the fixture within a <see cref="ComposedFixture"/>.
        /// </summary>
        /// <param name="image">
        /// Optionally specifies the NATS container image.  This defaults to 
        /// <b>nkubeio/nats:latest</b> or <b>nkubedev/nats:latest</b> depending
        /// on whether the assembly was built from a git release branch or not.
        /// </param>
        /// <param name="name">Optionally specifies the container name (defaults to <c>nats-test</c>).</param>
        /// <param name="args">Optional NATS server command line arguments.</param>
        /// <param name="hostInterface">
        /// Optionally specifies the host interface where the container public ports will be
        /// published.  This defaults to <see cref="ContainerFixture.DefaultHostInterface"/>
        /// but may be customized.  This needs to be an IPv4 address.
        /// </param>
        public void StartAsComposed(
            string   image         = null,
            string   name          = "nats-test",
            string[] args          = null,
            string   hostInterface = null)
        {
            image              = image ?? $"{KubeConst.NeonBranchRegistry}/nats:latest";
            this.hostInterface = hostInterface;

            base.CheckWithinAction();

            var dockerArgs =
                new string[]
                {
                    "--detach",
                    "-p", $"{GetHostInterface(hostInterface)}:4222:4222",
                    "-p", $"{GetHostInterface(hostInterface)}:8222:8222",
                    "-p", $"{GetHostInterface(hostInterface)}:6222:6222"
                };

            if (!IsRunning)
            {
                StartAsComposed(name, image, dockerArgs, args);
            }

            var factory = new ConnectionFactory();
            var retry   = new LinearRetryPolicy(exception => true, 20, TimeSpan.FromSeconds(0.5));

            retry.InvokeAsync(
                async () =>
                {
                    Connection = factory.CreateConnection($"nats://{GetHostInterface(hostInterface, forConnection: true)}:4222");

                    await Task.CompletedTask;

                }).Wait();
        }

        /// <summary>
        /// Restarts the NATS container to clear any previous state and returns the 
        /// new client connection.
        /// </summary>
        public new IConnection Restart()
        {
            base.Restart();

            if (Connection != null)
            {
                Connection.Dispose();
                Connection = null;
            }

            var factory = new ConnectionFactory();
            var retry   = new LinearRetryPolicy(exception => true, 20, TimeSpan.FromSeconds(0.5));

            retry.InvokeAsync(
                async () =>
                {
                    Connection = factory.CreateConnection($"nats://{GetHostInterface(hostInterface, forConnection: true)}:4222");
                    await Task.CompletedTask;

                }).Wait();

            return Connection;
        }

        /// <summary>
        /// This method completely resets the fixture by removing and recreating
        /// the NATS container.
        /// </summary>
        public override void Reset()
        {
            if (Connection != null)
            {
                Connection.Dispose();
                Connection = null;
            }

            base.Reset();
        }
    }
}
