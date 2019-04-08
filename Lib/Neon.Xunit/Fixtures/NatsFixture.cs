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
    /// Used to run the Docker <b>nkubeio.nats-test</b> container on 
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

        public TestFixtureStatus Start()
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(image));

            return base.Start(
                () =>
                {
                    StartInAction(settings, image, name, env, username, password, noPrimary);
                });
        }

        /// <summary>
        /// Actually starts NATS within the initialization <see cref="Action"/>.  You'll
        /// generally want to use <see cref="Start()"/>
        /// but this method is used internally or for special situations.
        /// </summary>
        public void StartInAction()
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
