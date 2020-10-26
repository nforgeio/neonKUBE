//-----------------------------------------------------------------------------
// FILE:	    YugaByteFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.

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

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Retry;
using Neon.Net;

using Newtonsoft.Json.Linq;
using Xunit;

namespace Neon.Xunit.YugaByte
{
    /// <summary>
    /// Used to run YugaByte database server and its related and services as
    /// a Docker compose application on the current machine as a test fixture while tests 
    /// are being performed  and then deletes the ap[plication when the fixture is
    /// disposed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This fixture assumes that YugaByte is not currently running on the
    /// local workstation or is running as a application named <b>yugabyte-dev</b>.
    /// You may see port conflict errors if either of these conditions
    /// are not true.
    /// </para>
    /// <para>
    /// See <see cref="Start(string, string, bool, int)"/>
    /// for more information about how this works.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public sealed class YugaByteFixture : DockerComposeFixture
    {
        /// <summary>
        /// <para>
        /// Specifies the default Cassandra YSQL port to be exposed by the fixture.  This
        /// is different from the Cassandra's default 9042 port to avoid conflcting with
        /// the Cassandra DB deployed the Cadence and Temporal test fixtures.  This is 
        /// a temporay hack:
        /// </para>
        /// <para>
        /// https://github.com/nforgeio/neonKUBE/issues/1029
        /// </para>
        /// </summary>
        public const int DefaultYcqlPort = 9099;

        /// <summary>
        /// The default Docker compose file text used to spin up YugaByte and it's related services
        /// by the <see cref="YugaByteFixture"/>.
        /// </summary>
        public const string DefaultComposeFile =
@"version: '2'

volumes:
  yb-master-data-1:
  yb-tserver-data-1:

services:
  yb-master:
    image: yugabytedb/yugabyte:latest
    container_name: yb-master-n1
    volumes:
    - yb-master-data-1:/mnt/master
    command: [ '/home/yugabyte/bin/yb-master',
               '--fs_data_dirs=/mnt/master',
               '--master_addresses=yb-master-n1:7100',
               '--rpc_bind_addresses=yb-master-n1:7100',
               '--replication_factor=1']
    ports:
    - '7000:7000'
    environment:
      SERVICE_7000_NAME: yb-master

  yb-tserver:
    image: yugabytedb/yugabyte:latest
    container_name: yb-tserver-n1
    volumes:
    - yb-tserver-data-1:/mnt/tserver
    command: [ '/home/yugabyte/bin/yb-tserver',
               '--fs_data_dirs=/mnt/tserver',
               '--start_pgsql_proxy',
               '--rpc_bind_addresses=yb-tserver-n1:9100',
               '--tserver_master_addrs=yb-master-n1:7100']
    ports:
    - '9099:9042'
    - '5433:5433'
    - '9000:9000'
    environment:
      SERVICE_5433_NAME: ysql
      SERVICE_9099_NAME: ycql
      SERVICE_6379_NAME: yedis
      SERVICE_9000_NAME: yb-tserver
    depends_on:
    - yb-master 
";

        private readonly TimeSpan   warmupDelay = TimeSpan.FromSeconds(2);      // Time to allow the YugaByte compose application to start.

        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public YugaByteFixture()
        {
        }

        /// <summary>
        /// Returns the connection string to be used by Cassandra clients to connect to the database.
        /// </summary>
        public string CassandraConnectionString { get; private set; }

        /// <summary>
        /// Returns the connection string to be used by Postgres clients to connecto to the database.
        /// </summary>
        public string PostgresConnectionString { get; private set; }

        /// <summary>
        /// Initializes the <see cref="CassandraConnectionString"/> and <see cref="PostgresConnectionString"/> properties.
        /// </summary>
        /// <param name="ycqlPort">The Cassandra YSQL port.</param>
        private void SetConnectionString(int ycqlPort)
        {
            CassandraConnectionString = $"HOST=localhost;PORT={ycqlPort};";
            PostgresConnectionString  = $"";
        }

        /// <summary>
        /// <para>
        /// Starts a YugaByte compose application if it's not already running.  You'll generally want
        /// to call this in your test class constructor instead of <see cref="ITestFixture.Start(Action)"/>.
        /// </para>
        /// <note>
        /// You'll need to call <see cref="StartAsComposed(string, string, bool, int)"/>
        /// instead when this fixture is being added to a <see cref="ComposedFixture"/>.
        /// </note>
        /// </summary>
        /// <param name="composeFile">
        /// <para>
        /// Optionally specifies the YugaByte Docker compose file text.  This defaults to
        /// <see cref="DefaultComposeFile"/> which configures YugaByte server to start with
        /// a new Cassandra database instance listening on port <b>9042</b> as well as the
        /// Temporal web UI running on port <b>8088</b>.  Temporal server is listening on
        /// its standard gRPC port <b>7233</b>.
        /// </para>
        /// <para>
        /// You may specify your own Docker compose text file to customize this by configuring
        /// a different backend database, etc.
        /// </para>
        /// </param>
        /// <param name="name">Optionally specifies the Docker compose application name (defaults to <c>yugabyte-dev</c>).</param>
        /// <param name="keepRunning">
        /// Optionally indicates that the compose application should remain running after the fixture is disposed.
        /// This is handy for using the Temporal web UI for port mortems after tests have completed.
        /// </param>
        /// <param name="ycqlPort">
        /// Specifies the port to be exposed by the Cassandra YSQL service.  This currently defaults to <see cref="DefaultYcqlPort"/>
        /// to avoid conflicts with the Cassandra DBs deployed by the Cadence and Temporal test fixtures but we hope eventually
        /// to change those to use a different port so we can revert this to the standard <b>9042</b> port.
        /// </param>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> if the fixture wasn't previously started and
        /// this method call started it or <see cref="TestFixtureStatus.AlreadyRunning"/> if the 
        /// fixture was already running.
        /// </returns>
        public TestFixtureStatus Start(
            string      composeFile = DefaultComposeFile,
            string      name        = "yugabyte-dev",
            bool        keepRunning = false,
            int         ycqlPort    = DefaultYcqlPort)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(composeFile), nameof(composeFile));

            return base.Start(
                () =>
                {
                    StartAsComposed(
                        composeFile:    composeFile, 
                        name:           name, 
                        keepRunning:    keepRunning,
                        ycqlPort:       ycqlPort);
                });
        }

        /// <summary>
        /// Used to start the fixture within a <see cref="ComposedFixture"/>.
        /// </summary>
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
        /// <param name="name">Optionally specifies the YugaByte compose application name (defaults to <c>yugabyte-dev</c>).</param>
        /// <param name="keepRunning">
        /// Optionally indicates that the compose application should remain running after the fixture is disposed.
        /// This is handy for using the Temporal web UI for port mortems after tests have completed.
        /// </param>
        /// <param name="ycqlPort">
        /// Specifies the port to be exposed by the Cassandra YSQL service.  This currently defaults to <see cref="DefaultYcqlPort"/>
        /// to avoid conflicts with the Cassandra DBs deployed by the Cadence and Temporal test fixtures but we hope eventually
        /// to change those to use a different port so we can revert this to the standard <b>9042</b> port.
        /// </param>
        public void StartAsComposed(
            string      composeFile  = DefaultComposeFile,
            string      name         = "yugabyte-dev",
            bool        keepRunning  = false,
            int         ycqlPort     = DefaultYcqlPort)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(composeFile), nameof(composeFile));

            base.CheckWithinAction();

            if (!IsRunning)
            {
                // Start the YugaByte compose application.

                base.StartAsComposed(name, composeFile.Replace("9099", ycqlPort.ToString()), keepRunning);
                Thread.Sleep(warmupDelay);
            }
        }
    }
}
