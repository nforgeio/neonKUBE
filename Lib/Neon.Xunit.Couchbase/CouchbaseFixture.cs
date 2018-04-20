//-----------------------------------------------------------------------------
// FILE:	    CouchbaseFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

using Couchbase;

using Neon.Common;
using Neon.Data;

namespace Xunit
{
    /// <summary>
    /// Used to run a Docker container on the current machine as a test 
    /// fixture while tests are being performed and then deletes the
    /// container when the fixture is disposed.
    /// </summary>
    public sealed class CouchbaseFixture : DockerContainerFixture
    {
        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public CouchbaseFixture()
        {
        }

        /// <summary>
        /// Starts a Couchbase container if it's not already running.
        /// </summary>
        /// <param name="settings">Optional Couchbase settings.</param>
        /// <param name="image">Optionally specifies the Couchbase container image (defaults to <b>neoncluster/couchbase-test:latest</b>).</param>
        /// <param name="name">Optionally specifies the Couchbase container name (defaults to <c>cb-test</c>).</param>
        /// <param name="env">Optional environment variables to be passed to the Couchbase container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <param name="username">Optional Couchbase username (defaults to <b>Administrator</b>).</param>
        /// <param name="password">Optional Couchbase password (defaults to <b>password</b>).</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this is not called from  within the <see cref="Action"/> method 
        /// passed <see cref="ITestFixture.Initialize(Action)"/>
        /// </exception>
        /// <remarks>
        /// <note>
        /// Some of the <paramref name="settings"/> properties will be ignored including 
        /// <see cref="CouchbaseSettings.Servers"/>.  This will be replaced by the local
        /// endpoint for the Couchbase container.  Also, the fixture will connect to the 
        /// <b>test</b> bucket by default (unless another is specified).
        /// </note>
        /// </remarks>
        public void Start(
            CouchbaseSettings   settings = null, 
            string              image    = "neoncluster/couchbase-test:latest",
            string              name     = "cb-test",
            string[]            env      = null,
            string              username = "Administrator",
            string              password = "password")
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(image));

            base.CheckWithinAction();

            lock (base.SyncRoot)
            {
                if (IsInitialized)
                {
                    return;
                }

                RunContainer(image, name, new string[] { "--detach", "-p", "8091-8094:8091-8094", "-p", "11210:11210" }, env: env);

                settings = settings ?? new CouchbaseSettings();

                settings.Servers.Clear();
                settings.Servers.Add(new Uri("http://localhost:8091"));

                if (settings.Bucket == null)
                {
                    settings.Bucket = "test";
                }

                Bucket   = settings.OpenBucket(username, password);
                Settings = settings;
            }
        }

        /// <summary>
        /// Returns the Couchbase bucket to be used to interact with Couchbase.
        /// </summary>
        public NeonBucket Bucket { get; private set; }

        /// <summary>
        /// Returns the <see cref="CouchbaseSettings"/> used to connect to the bucket.
        /// </summary>
        public CouchbaseSettings Settings { get; private set; }
    }
}
