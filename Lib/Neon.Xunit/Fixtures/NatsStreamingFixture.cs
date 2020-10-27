//-----------------------------------------------------------------------------
// FILE:	    NatsStreamingFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.

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
using STAN.Client;

using Newtonsoft.Json.Linq;
using Xunit;

using Neon.Common;
using Neon.Data;
using Neon.Retry;
using Neon.Net;

namespace Neon.Xunit
{
    /// <summary>
    /// Used to run a Docker <b>nats-streaming</b> container on the current 
    /// machine as a test fixture while tests are being performed and 
    /// then deletes the container when the fixture is disposed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This fixture assumes that NATS-SERVER is not currently running on the
    /// local workstation or as a container named <b>nats-test</b>.
    /// You may see port conflict errors if either of these conditions 
    /// are not true.
    /// </para>
    /// <para>
    /// A somewhat safer but slower alternative, is to use the <see cref="DockerFixture"/>
    /// instead and add <see cref="NatsStreamingFixture"/> as a subfixture.  The 
    /// advantage is that <see cref="DockerFixture"/> will ensure that all
    /// (potentially conflicting) containers are removed before the NatsFixture
    /// fixture is started.
    /// </para>
    /// <para>
    /// Use <see cref="Restart"/> to clear the NATS-SATREAMIN server state by
    /// restarting its Docker container.  This also returns the new client 
    /// connection.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public sealed class NatsStreamingFixture : ContainerFixture
    {
        private string containerName;

        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public NatsStreamingFixture()
        {
        }

        /// <summary>
        /// Returns the NATS-STREAMING connection.
        /// </summary>
        public IStanConnection Connection { get; private set; }

        /// <summary>
        /// <para>
        /// Starts a NATS-STREAMING container if it's not already running.  You'll generally want
        /// to call this in your test class constructor instead of <see cref="ITestFixture.Start(Action)"/>.
        /// </para>
        /// <note>
        /// You'll need to call <see cref="StartAsComposed(string, string, string[])"/>
        /// instead when this fixture is being added to a <see cref="ComposedFixture"/>.
        /// </note>
        /// </summary>
        /// <param name="image">
        /// Optionally specifies the NATS-STREAMING container image.  This defaults to 
        /// <b>nkubeio/nats-streaming:latest</b> or <b>nkubedev/nats-streaming:latest</b>
        /// depending on whether the assembly was built from a git release branch
        /// or not.
        /// </param>
        /// <param name="name">Optionally specifies the NATS-STREAMING container name (defaults to <c>nats-streaming-test</c>).</param>
        /// <param name="args">Optional NATS-STREAMING server command line arguments.</param>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> if the fixture wasn't previously started and
        /// this method call started it or <see cref="TestFixtureStatus.AlreadyRunning"/> if the 
        /// fixture was already running.
        /// </returns>
        public TestFixtureStatus Start(
            string   image = null,
            string   name  = "nats-streaming-test",
            string[] args  = null)
        {
            return base.Start(
                () =>
                {
                    StartAsComposed(image, name, args);
                });
        }

        /// <summary>
        /// Used to start the fixture within a <see cref="ComposedFixture"/>.
        /// </summary>
        /// <param name="image">
        /// Optionally specifies the NATS container image.  This defaults to 
        /// <b>nkubeio/nats-streaming:latest</b> or <b>nkubedev/nats-streaming:latest</b>
        /// depending on whether the assembly was built from a git release branch
        /// or not.
        /// </param>
        /// <param name="name">Optionally specifies the container name (defaults to <c>nats-streaming-test</c>).</param>
        /// <param name="args">
        /// <para>
        /// Optional NATS-STREAMING server command line arguments.
        /// </para>
        /// <note>
        /// Reasonable defaults are used when <paramref name="args"/> is passed 
        /// as <c>null</c>.  These connect the NATS streaming server to the NATS
        /// core server running within the container and also configure the
        /// default MEMORY persisted store.
        /// </note>
        /// </param>
        public void StartAsComposed(
            string   image = null,
            string   name  = "nats-streaming-test",
            string[] args  = null)
        {
            this.containerName = name;

            image = image ?? $"{NeonHelper.NeonBranchRegistry}/nats-streaming:latest";

            base.CheckWithinAction();

            var dockerArgs =
                new string[]
                {
                    "--detach",
                    "-p", "4222:4222",
                    "-p", "8222:8222",
                };

            var containerArgs = new List<string>();

            if (args != null)
            {
                foreach (var arg in args)
                {
                    containerArgs.Add(arg);
                }
            }

            if (!IsRunning)
            {
                StartAsComposed(name, image, dockerArgs, containerArgs);
            }

            Connect();
        }

        /// <summary>
        /// Establishes the server connection.
        /// </summary>
        private void Connect()
        {
            var factory = new StanConnectionFactory();
            var retry   = new LinearRetryPolicy(exception => true, 20, TimeSpan.FromSeconds(0.5));

            retry.InvokeAsync(
                async () =>
                {
                    Connection = factory.CreateConnection("test-cluster", nameof(NatsStreamingFixture));
                    await Task.CompletedTask;

                }).Wait();
        }

        /// <summary>
        /// Restarts the NATS container to clear any previous state and returns the 
        /// new client connection.
        /// </summary>
        /// <returns>The new connection.</returns>
        public new IStanConnection Restart()
        {
            base.Restart();

            if (Connection != null)
            {
                Connection.Dispose();
                Connection = null;
            }

            Connect();

            return Connection;
        }

        /// <summary>
        /// This method completely resets the fixture by removing and recreating
        /// the NATS-STREAMING container.
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
