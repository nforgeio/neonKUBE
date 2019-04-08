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
    /// Stubbed.
    /// </summary>
    public class NatsSettings
    {
    }

    /// <summary>
    /// Used to run the Docker <b>nkubeio/nats-test</b> container on 
    /// the current machine as a test fixture while tests are being performed 
    /// and then deletes the container when the fixture is disposed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This fixture assumes that NATS is not currently running on the
    /// local workstation or as a container that is named <b>nats-test</b>.
    /// You may see port conflict errors if either of these assumptions are
    /// not true.
    /// </para>
    /// <para>
    /// A somewhat safer but slower alternative, is to use the <see cref="DockerFixture"/>
    /// instead and add <see cref="NatsFixture"/> as a subfixture.  The 
    /// advantage is that <see cref="DockerFixture"/> will ensure that all
    /// (potentially conflicting) containers are removed before the Couchbase
    /// fixture is started.
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
        /// Starts a Couchbase container if it's not already running.  You'll generally want
        /// to call this in your test class constructor instead of <see cref="ITestFixture.Start(Action)"/>.
        /// </summary>
        /// <param name="settings">Optional NATS settings.</param>
        /// <param name="image">Optionally specifies the NATS container image (defaults to <b>nkubeio/nats-test:latest</b>).</param>
        /// <param name="name">Optionally specifies the NATS container name (defaults to <c>nats-test</c>).</param>
        /// <param name="env">Optional environment variables to be passed to the Couchbase container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <param name="username">Optional NATS username (defaults to <b>Administrator</b>).</param>
        /// <param name="password">Optional NATS password (defaults to <b>password</b>).</param>
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
        /// There are three basic patterns for using this fixture.
        /// </para>
        /// <list type="table">
        /// <item>
        /// <term><b>initialize once</b></term>
        /// <description>
        /// <para>
        /// The basic idea here is to have your test class initialize NATS
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
        /// For scenarios where NATS must be cleared before every test,
        /// you can use the <see cref="Clear()"/> method to reset its
        /// state within each test method, populate the database as necessary,
        /// and then perform your tests.
        /// </description>
        /// </item>
        /// <item>
        /// <term><b>docker integrated</b></term>
        /// <description>
        /// The <see cref="NatsFixture"/> can also be added to the <see cref="DockerFixture"/>
        /// and used within a swarm.  This is useful for testing multiple services and
        /// also has the advantage of ensure that swarm/node state is fully reset
        /// before the database container is started.
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        public TestFixtureStatus Start(
            NatsSettings        settings  = null,
            string              image     = "nkubeio/nats-test:latest",
            string              name      = "nats-test",
            string[]            env       = null,
            string              username  = "Administrator",
            string              password  = "password")
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(image));

            return base.Start(
                () =>
                {
                    StartInAction(settings, image, name, env, username, password);
                });
        }

        /// <summary>
        /// Actually starts NATS within the initialization <see cref="Action"/>.  You'll
        /// generally want to use <see cref="Start(NatsSettings, string, string, string[], string, string)"/>
        /// but this method is used internally or for special situations.
        /// </summary>
        /// <param name="settings">Optional Couchbase settings.</param>
        /// <param name="image">Optionally specifies the Couchbase container image (defaults to <b>nkubeio/couchbase-test:latest</b>).</param>
        /// <param name="name">Optionally specifies the Couchbase container name (defaults to <c>cb-test</c>).</param>
        /// <param name="env">Optional environment variables to be passed to the Couchbase container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <param name="username">Optional Couchbase username (defaults to <b>Administrator</b>).</param>
        /// <param name="password">Optional Couchbase password (defaults to <b>password</b>).</param>
        public void StartInAction(
            NatsSettings        settings  = null,
            string              image     = "nkubeio/nats-test:latest",
            string              name      = "nats-test",
            string[]            env       = null,
            string              username  = "Administrator",
            string              password  = "password")
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(image));

            base.CheckWithinAction();
        }
        
        /// <summary>
        /// Restores NATS to its initial vigin state.
        /// </summary>
        public void Clear()
        {
            CheckDisposed();
        }

        /// <summary>
        /// This method completely resets the fixture by removing the NATS 
        /// container from Docker.  Use <see cref="Clear"/> if you just want to 
        /// clear the database.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
        }
    }
}
