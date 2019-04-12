//-----------------------------------------------------------------------------
// FILE:	    NatsFixture.cs
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
        /// Constructs the fixture.
        /// </summary>
        public NatsFixture()
        {
        }

        /// <summary>
        /// Returns a connected NATS client.
        /// </summary>
        public IConnection Client { get; private set; }

        /// <summary>
        /// Starts a NATS container if it's not already running.  You'll generally want
        /// to call this in your test class constructor instead of <see cref="ITestFixture.Start(Action)"/>.
        /// </summary>
        /// <param name="image">
        /// Optionally specifies the NATS container image.  This defaults to 
        /// <b>nkubeio/nats-test:latest</b> or <b>nkubedev/nats-test:latest</b>
        /// depending on whether the assembly was built from a git release branch
        /// or not.
        /// </param>
        /// <param name="name">Optionally specifies the NATS container name (defaults to <c>nats-test</c>).</param>
        /// <param name="args">Optional NATS server command line arguments.</param>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> if the fixture wasn't previously started and
        /// this method call started it or <see cref="TestFixtureStatus.AlreadyRunning"/> if the 
        /// fixture was already running.
        /// </returns>
        public TestFixtureStatus Start(
            string   image = null,
            string   name  = "nats-test",
            string[] args  = null)
        {
            return base.Start(
                () =>
                {
                    StartInAction(image, name, args);
                });
        }

        /// <summary>
        /// Actually starts NATS within the initialization <see cref="Action"/>.  You'll
        /// generally want to use <see cref="Start(string, string, string[])"/>
        /// but this method is used internally or for special situations.
        /// </summary>
        /// <param name="image">
        /// Optionally specifies the NATS container image.  This defaults to 
        /// <b>nkubeio/nats-test:latest</b> or <b>nkubedev/nats-test:latest</b>
        /// depending on whether the assembly was built from a git release branch
        /// or not.
        /// </param>
        /// <param name="name">Optionally specifies the Couchbase container name (defaults to <c>cb-test</c>).</param>
        /// <param name="args">Optional NATS server command line arguments.</param>
        public void StartInAction(
            string   image = null,
            string   name  = "nats-test",
            string[] args  = null)
        {
            image = image ?? $"{KubeConst.NeonBranchRegistry}/nats:latest";

            base.CheckWithinAction();

            var dockerArgs =
                new string[]
                {
                    "--detach",
                    "-p", "4222:4222",
                    "-p", "8222-8222",
                    "-p", "6222-6222"
                };

            if (!IsRunning)
            {
                RunContainer(name, image, dockerArgs, args);
            }

            var factory = new ConnectionFactory();
            var retry   = new LinearRetryPolicy(exception => true, 20, TimeSpan.FromSeconds(0.5));

            retry.InvokeAsync(
                async () =>
                {
                    Client = factory.CreateConnection();
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

            if (Client != null)
            {
                Client.Dispose();
                Client = null;
            }

            var factory = new ConnectionFactory();
            var retry   = new LinearRetryPolicy(exception => true, 20, TimeSpan.FromSeconds(0.5));

            retry.InvokeAsync(
                async () =>
                {
                    Client = factory.CreateConnection();
                    await Task.CompletedTask;

                }).Wait();

            return Client;
        }

        /// <summary>
        /// This method completely resets the fixture by removing the NATS 
        /// container from Docker.
        /// </summary>
        public override void Reset()
        {
            if (Client != null)
            {
                Client.Dispose();
                Client = null;
            }

            base.Reset();
        }
    }
}
