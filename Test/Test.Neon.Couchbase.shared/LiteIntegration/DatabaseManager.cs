//-----------------------------------------------------------------------------
// FILE:	    DatabaseManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Couchbase;
using Couchbase.Core;
using Couchbase.Management;

using Couchbase.Lite;
using Couchbase.Lite.Auth;

using Neon.Common;
using Neon.Couchbase.SyncGateway;

using Xunit;

namespace TestLiteIntegration
{
    /// <summary>
    /// Initializes a local Couchbase database, bucket, and Sync Gateway and
    /// local Couchbase Lite database for testing purposes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class assumes that Couchbase is running locally on the standard
    /// ports with preconfigured admin credentials.  The constructor assumes that
    /// Couchbase Sync Gateway is not already running.  You'll need to stop the
    /// service manually for these tests.
    /// </para>
    /// </remarks>
    public sealed class DatabaseManager : IDisposable
    {
        //---------------------------------------------------------------------
        // Local types

        public sealed class LocalDatabase : IDisposable
        {
            private Uri         databaseUri;
            private Manager     manager;
            private string      folder;

            public LocalDatabase(string databaseUri, string userId, string password)
            {
                this.databaseUri = new Uri(databaseUri);

                folder  = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                manager = new Manager(new DirectoryInfo(folder), new ManagerOptions())
                {
                    // $todo(jeff.lill):
                    //
                    // I can't get ForestDB working here for .NET 4.x unit tests.  SQLite seems
                    // to work fine and I am able to enable ForestDB in the apps.

                    //StorageType = StorageEngineTypes.ForestDB
                };

                Database = manager.GetEntityDatabase(DatabaseName);
                UserId   = userId;
                Password = password;
            }

            public void Dispose()
            {
                if (Database != null)
                {
                    Database.Dispose();
                    Database = null;
                }

                if (manager != null)
                {
                    manager.Close();
                    manager = null;

                    Directory.Delete(folder, recursive: true);
                }
            }

            public EntityDatabase Database { get; private set; }

            public string UserId { get; private set; }

            public string Password { get; private set; }

            /// <summary>
            /// Replicates all pending documents for a given replication and then returns.
            /// </summary>
            /// <param name="replication">The replication.</param>
            /// <returns>The number of replicated documents.</returns>
            private int ReplicatePending(Replication replication)
            {
                var isFinished = false;

                replication.Authenticator = AuthenticatorFactory.CreateBasicAuthenticator(UserId, Password);

                replication.Changed +=
                    (s, a) =>
                    {
                        if (a.Status == ReplicationStatus.Stopped)
                        {
                            isFinished = true;
                        }
                    };

                replication.Start();
                NeonHelper.WaitFor(() => isFinished, TimeSpan.FromSeconds(10));

                return replication.CompletedChangesCount;
            }

            /// <summary>
            /// Synchronizes the local and remote databases.
            /// </summary>
            /// <param name="push">
            /// Pass <c>null</c> to replicate in both directions, <c>true</c> to 
            /// push changes to the remote, or <c>false</c> to pull changes from 
            /// the remote.</param>
            /// <returns>The number of replicated documents.</returns>
            public int Replicate(bool? push = null)
            {
                var count = 0;

                if (push == null || push.Value)
                {
                    count += ReplicatePending(Database.CreatePushReplication(databaseUri));
                }

                if (push == null || !push.Value)
                {
                    count += ReplicatePending(Database.CreatePullReplication(databaseUri));
                }

                return count;
            }
        }

        //---------------------------------------------------------------------
        // Static members

        private const string CouchbaseUID    = "Administrator";
        private const string CouchbasePWD    = "test000";
        private const string SyncGatewayPath = @"C:\Program Files (x86)\Couchbase\sync_gateway.exe";
        private const string BucketName      = "test";
        private const string DatabaseName    = "db";

        public static async Task<DatabaseManager> InitializeAsync()
        {
            var state = new DatabaseManager();

            await state.InitAsync();

            return state;
        }

        //---------------------------------------------------------------------
        // Instance members

        private Cluster                 cluster;
        private IClusterManager         clusterManager;
        private Gateway                 gateway;
        private GatewayManager          gatewayManager;
        private Process                 syncGatewayProcess;
        private List<LocalDatabase>     localDatabases = new List<LocalDatabase>();
        private HashSet<string>         existingRoles  = new HashSet<string>();

        private DatabaseManager()
        {
        }

        private async Task InitAsync()
        {
            // Initialize a clean Couchbase bucket.

            cluster = new Cluster();
            clusterManager = cluster.CreateManager(CouchbaseUID, CouchbasePWD);

            if (clusterManager.ListBuckets().Value.Count(b => b.Name == BucketName) > 0)
            {
                clusterManager.RemoveBucket(BucketName);
            }

            if (!clusterManager.CreateBucket(BucketName, 100).Success)
            {
                Assert.True(false, $"Could not create the [{BucketName}] Couchbase bucket.");
            }

            Bucket = cluster.OpenBucket(BucketName);

            // Crank up the sync gateway.

            syncGatewayProcess = Process.Start(SyncGatewayPath, $"-bucket \"{BucketName}\" -dbname \"{DatabaseName}\"");

            if (syncGatewayProcess.WaitForExit(2000))
            {
                Assert.True(false, $"Could not start a Couchbase Sync Gateway.  Verify that the service is not already running.");
            }

            // Initialize the sync gateway database.

            gateway = new Gateway(
                new GatewaySettings()
                {
                    Host = "localhost"
                });

            gatewayManager = gateway.CreateManager();

            var databasesDeleted = false;

            foreach (var database in await gatewayManager.DatabaseListAsync())
            {
                if (database == DatabaseName)
                {
                    await gatewayManager.DatabaseRemoveAsync(database);
                    databasesDeleted = true;
                }
            }

            if (databasesDeleted)
            {
                // Looks like the sync gateway needs some time to stablize 
                // after deleting databases.

                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            await gatewayManager.DatabaseCreateAsync(
                new DatabaseConfiguration()
                {
                    Name   = DatabaseName,
                    Bucket = BucketName,
                    Server = "http://127.0.0.1:8091",
                    Sync   = "function (doc, oldDoc) { channel(doc.channels); }"
                });
        }

        public void Dispose()
        {
            // Clean up the local databases.

            foreach (var database in localDatabases)
            {
                try
                {
                    database.Dispose();
                }
                catch
                {
                    // Intentionally ignoring these
                }
            } 

            // Stop the Sync Gateway.

            if (gateway != null)
            {
                gateway.Dispose();
                gateway = null;
            }

            if (syncGatewayProcess != null)
            {
                syncGatewayProcess.Kill();
                syncGatewayProcess.WaitForExit(2000);

                syncGatewayProcess = null;
            }

            // Release the Couchbase database connections.

            if (Bucket != null)
            {
                Bucket.Dispose();
                Bucket = null;
            }

            if (cluster != null)
            {
                cluster.Dispose();
                cluster = null;
            }
        }

        public IBucket Bucket { get; private set; }

        /// <summary>
        /// Creates a Sync Gateway user account if it doesn't already exist and then creates a
        /// new local Couchbase Lite database associated with the account.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="channels">The optional channels to be associated with the user.</param>
        /// <param name="roles">The optional roles to be associated with the user.</param>
        /// <returns>Information about thge local database.</returns>
        /// <remarks>
        /// If both <paramref name="channels"/> and <paramref name="roles"/> are <c>null</c>
        /// then the user will be associated with all channels.
        /// </remarks>
        public async Task<LocalDatabase> CreateLocalDatabaseAsync(string userId, IEnumerable<string> channels = null, IEnumerable<RoleProperties> roles = null)
        {
            var userExists = localDatabases.SingleOrDefault(d => d.UserId == userId) != null;
            var password   = userId + "-password";

            if (!userExists)
            {
                if (roles != null)
                {
                    foreach (var role in roles)
                    {
                        if (!existingRoles.Contains(role.Name))
                        {
                            await gatewayManager.RoleCreateAsync(DatabaseName, role);
                            existingRoles.Add(role.Name);
                        }
                    }
                }

                if (channels == null && roles == null)
                {
                    channels = new string[] { "*" };
                }

                if (channels == null)
                {
                    channels = new string[0];
                }

                var roleNames = new List<string>();

                if (roles != null)
                {
                    foreach (var role in roles)
                    {
                        roleNames.Add(role.Name);
                    }
                }

                await gatewayManager.UserCreateAsync(
                    DatabaseName,
                    new UserProperties()
                    {
                        Name          = userId,
                        Password      = password,
                        IsDisabled    = false,
                        AdminChannels = channels.ToList(),
                        Roles         = roleNames,
                        Email         = $"{userId}@test.com"
                    });
            }

            var localDatabase = new LocalDatabase(gateway.GetDatabaseUri(DatabaseName), userId, password);

            localDatabases.Add(localDatabase);

            return localDatabase;
        }
    }
}
