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

using Cassandra;
using Newtonsoft.Json.Linq;
using Npgsql;
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
    /// See <see cref="Start(string, string, string, bool, int, int)"/>
    /// for more information about how this works.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public sealed class YugaByteFixture : DockerComposeFixture
    {
        private string      cassandraKeyspace;
        private string      postgresDatabase;
        private int         ycqlPort;
        private int         ysqlPort;

        /// <summary>
        /// Specifies the default Cassandra keyspace.
        /// </summary>
        private const string DefaultCassandraKeyspace = "test-cassandra";

        /// <summary>
        /// Specifies the default Postgres database.
        /// </summary>
        private const string DefaultPostgresDatabase = "test-postgres";

        /// <summary>
        /// Specifies the Cassandra YCQL port to be exposed by the fixture.
        /// </summary>
        private const int DefaultYcqlPort = 9099;    // $todo(jefflill): Change this back to 9042 after: https://github.com/nforgeio/neonKUBE/issues/1029

        /// <summary>
        /// Specifies the Postgres YSQL port to be exposed by the fixture.
        /// </summary>
        private const int DefaultYsqlPort = 5433;

        /// <summary>
        /// The default Docker compose file text used to spin up YugaByte and it's related services
        /// by the <see cref="YugaByteFixture"/>.
        /// </summary>
        private const string BaseComposeFile =
@"version: '3.5'

# This compose file is parameterized to support custom ports as well
# as to prefix the custom container names with the compose application
# name so thew underlying [DockerComposeFixture] will be able to
# remove the containers.
#
#       YCQLPORT    - will be replaced by the Cassandra port
#       YSQLPORT    - will be replaced by the Postgres port

services:
  yb-master:
    image: yugabytedb/yugabyte:latest
    container_name: yb-master-n1
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
    command: [ '/home/yugabyte/bin/yb-tserver',
               '--fs_data_dirs=/mnt/tserver',
               '--start_pgsql_proxy',
               '--rpc_bind_addresses=yb-tserver-n1:9100',
               '--tserver_master_addrs=yb-master-n1:7100']
    ports:
      - 'YCQLPORT:9042'
      - 'YSQLPORT:5433'
      - '9000:9000'
    environment:
      SERVICE_YSQLPORT_NAME: ysql
      SERVICE_YCQLPORT_NAME: ycql
      SERVICE_6379_NAME: yedis
      SERVICE_9000_NAME: yb-tserver
    depends_on:
      - yb-master 
";

        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public YugaByteFixture()
        {
        }

        /// <summary>
        /// Returns the Cassandra session.
        /// </summary>
        public ISession CassandraSession { get; private set; }

        /// <summary>
        /// Returns the Postgres connection.
        /// </summary>
        public NpgsqlConnection PostgresConnection { get; private set; }

        /// <summary>
        /// <para>
        /// Starts a YugaByte compose application if it's not already running.  You'll generally want
        /// to call this in your test class constructor instead of <see cref="ITestFixture.Start(Action)"/>.
        /// </para>
        /// <note>
        /// You'll need to call <see cref="StartAsComposed(string, string, string, bool, int, int)"/>
        /// instead when this fixture is being added to a <see cref="ComposedFixture"/>.
        /// </note>
        /// </summary>
        /// <param name="name">Optionally specifies the Docker compose application name (defaults to <b>yugabyte-dev</b>).</param>
        /// <param name="cassandraKeyspace">
        /// Optionally specifies the Cassandra keyspace.  This defaults to <b>test-cassandra</b>.  Note that
        /// the <paramref name="cassandraKeyspace"/> and <paramref name="postgresDatabase"/> must be different.
        /// </param>
        /// <param name="postgresDatabase">
        /// Optionally specifies the Postgres database.  This defaults to <b>test-postgres</b>.  Note that
        /// the <paramref name="cassandraKeyspace"/> and <paramref name="postgresDatabase"/> must be different.
        /// </param>
        /// <param name="keepRunning">
        /// Optionally indicates that the compose application should remain running after the fixture is disposed.
        /// </param>
        /// <param name="ycqlPort">
        /// Specifies the port to be exposed by the Cassandra YCQL service.  This currently defaults to <see cref="DefaultYcqlPort"/>
        /// which is temporarily set to <b>9099</b> to avoid conflicts with the Cassandra DBs deployed by the Cadence and Temporal
        /// test fixtures but we hope eventually to change those to use a different port so we can revert this to the standard
        /// <b>9042</b> port.
        /// </param>
        /// <param name="ysqlPort">
        /// Specifies the port to be exposed by the Postgres YCQL service.  This defaults to <see cref="DefaultYsqlPort"/>
        /// which is set to the default Postgres port <b>5433</b>.
        /// </param>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> if the fixture wasn't previously started and
        /// this method call started it or <see cref="TestFixtureStatus.AlreadyRunning"/> if the 
        /// fixture was already running.
        /// </returns>
        public TestFixtureStatus Start(
            string      name              = "yugabyte-dev",
            string      cassandraKeyspace = DefaultCassandraKeyspace,
            string      postgresDatabase  = DefaultPostgresDatabase,
            bool        keepRunning       = false,
            int         ycqlPort          = DefaultYcqlPort,
            int         ysqlPort          = DefaultYsqlPort)
        {
            return base.Start(
                () =>
                {
                    StartAsComposed(
                        name:              name, 
                        cassandraKeyspace: cassandraKeyspace,
                        postgresDatabase:  postgresDatabase,
                        keepRunning:       keepRunning,
                        ycqlPort:          ycqlPort,
                        ysqlPort:          ysqlPort);
                });
        }

        /// <summary>
        /// Used to start the fixture within a <see cref="ComposedFixture"/>.
        /// </summary>
        /// <param name="name">Optionally specifies the YugaByte compose application name (defaults to <b>yugabyte-dev</b>).</param>
        /// <param name="cassandraKeyspace">
        /// Optionally specifies the Cassandra keyspace.  This defaults to <b>test-cassandra</b>.  Note that
        /// the <paramref name="cassandraKeyspace"/> and <paramref name="postgresDatabase"/> must be different.
        /// </param>
        /// <param name="postgresDatabase">
        /// Optionally specifies the Postgres database.  This defaults to <b>test-postgres</b>.  Note that
        /// the <paramref name="cassandraKeyspace"/> and <paramref name="postgresDatabase"/> must be different.
        /// </param>
        /// <param name="keepRunning">
        /// Optionally indicates that the compose application should remain running after the fixture is disposed.
        /// </param>
        /// <param name="ycqlPort">
        /// Specifies the port to be exposed by the Cassandra YCQL service.  This currently defaults to <see cref="DefaultYcqlPort"/>
        /// which is temporarily set to <b>9099</b> to avoid conflicts with the Cassandra DBs deployed by the Cadence and Temporal
        /// test fixtures but we hope eventually to change those to use a different port so we can revert this to the standard
        /// <b>9042</b> port.
        /// </param>
        /// <param name="ysqlPort">
        /// Specifies the port to be exposed by the Postgres YCQL service.  This defaults to <see cref="DefaultYsqlPort"/>
        /// which is set to the default Postgres port <b>5433</b>.
        /// </param>
        public void StartAsComposed(
            string      name              = "yugabyte-dev",
            string      cassandraKeyspace = DefaultCassandraKeyspace,
            string      postgresDatabase  = DefaultPostgresDatabase,
            bool        keepRunning       = false,
            int         ycqlPort          = DefaultYcqlPort,
            int         ysqlPort          = DefaultYsqlPort)
        {
            base.CheckWithinAction();

            this.cassandraKeyspace = cassandraKeyspace;
            this.postgresDatabase  = postgresDatabase;
            this.ycqlPort          = ycqlPort;
            this.ysqlPort          = ysqlPort;

            if (!IsRunning)
            {
                // Start the YugaByte compose application.

                var composeFile = BaseComposeFile;

                composeFile = composeFile.Replace("YCQLPORT", ycqlPort.ToString());
                composeFile = composeFile.Replace("YSQLPORT", ysqlPort.ToString());

                base.StartAsComposed(name, composeFile, keepRunning, new string[] { "yb-master-n1", "yb-tserver-n1" });
                Initialize();
            }
        }

        /// <inheritdoc/>
        public override void Restart()
        {
            base.Restart();
            Initialize();
        }

        /// <summary>
        /// Initializes the database and establishes the connections.
        /// </summary>
        private void Initialize()
        {
            // Establish the database connections.  Note that rather than using a fragile
            // warmup delay, we'll just retry establishing the connections for up to 15 seconds.
            //
            // Note that we're going to delete the Cassandra keyspace and Postgres database
            // before recreating them so they'll start out empty for each unit test.

            var retry = new LinearRetryPolicy(e => true, int.MaxValue, retryInterval: TimeSpan.FromSeconds(1), timeout: new TimeSpan(15));

            // Establish the Cassandra session, recreating the keyspace.

            var cluster = Cluster.Builder()
                .AddContactPoint("127.0.0.1")
                .WithPort(ycqlPort)
                .Build();

            retry.InvokeAsync(
                async () =>
                {
                    CassandraSession = cluster.Connect();
                    await Task.CompletedTask;

                }).Wait();

            CassandraSession.Execute($"DROP KEYSPACE IF EXISTS \"{cassandraKeyspace}\"");
            CassandraSession.Execute($"CREATE KEYSPACE \"{cassandraKeyspace}\"");

            CassandraSession = cluster.Connect(cassandraKeyspace);

            // Establish the Postgres connection, recreating the database.

            PostgresConnection = new NpgsqlConnection($"host=127.0.0.1;port={ysqlPort};user id=yugabyte;password=");

            retry.InvokeAsync(
                async () =>
                {
                    PostgresConnection.Open();
                    await Task.CompletedTask;

                }).Wait();

            NpgsqlCommand command;

            command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{postgresDatabase}\"", PostgresConnection);
            command.ExecuteNonQuery();

            command = new NpgsqlCommand($"CREATE DATABASE \"{postgresDatabase}\"", PostgresConnection);
            command.ExecuteNonQuery();

            PostgresConnection = new NpgsqlConnection($"host=127.0.0.1;database={postgresDatabase};port={ysqlPort};user id=yugabyte;password=");
            PostgresConnection.Open();
        }
    }
}
