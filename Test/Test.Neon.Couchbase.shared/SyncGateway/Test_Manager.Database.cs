//-----------------------------------------------------------------------------
// FILE:	    Test_Manager.Database.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase;

using Neon.Common;
using Neon.Couchbase.SyncGateway;
using Neon.Xunit;

using Xunit;

namespace TestSyncGateway
{
    /// <summary>
    /// Tests the Sync Gateway manager REST API.
    /// </summary>
    public partial class Test_Manager
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task DatabaseList()
        {
            await TestCluster.ClearAsync();

            try
            {
                using (var gateway = TestCluster.CreateGateway())
                {
                    var manager   = gateway.CreateManager();
                    var databases = await manager.DatabaseListAsync();

                    Assert.Empty(databases);

                    await TestCluster.CreateDatabaseAsync("db");

                    databases = await manager.DatabaseListAsync();
                    Assert.Single(databases);
                    Assert.Contains(databases, db => db == "db");
                }
            }
            finally
            {
                await TestCluster.ClearAsync();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task DatabaseStatus()
        {
            await TestCluster.ClearAsync();

            try
            {
                using (var gateway = TestCluster.CreateGateway())
                {
                    var manager   = gateway.CreateManager();
                    var databases = await manager.DatabaseListAsync();

                    await TestCluster.CreateDatabaseAsync("db");

                    var status = await manager.DatabaseStatusAsync("db");

                    Assert.Equal("db", status.Name);
                    Assert.True(status.CommitUpdateSequence >= 0);
                    Assert.True(status.UpdateSequence >= 0);
                    Assert.True(status.UpdateSequence >= status.CommitUpdateSequence);
                    Assert.Equal(DatabaseState.Online, status.State);
                    Assert.True(status.StartTimeUtc > new DateTime(2016, 5, 15));
                }
            }
            finally
            {
                await TestCluster.ClearAsync();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task DatabaseCreateRemove()
        {
            try
            {
                await TestCluster.ClearAsync();

                using (var gateway = TestCluster.CreateGateway())
                {
                    var manager   = gateway.CreateManager();
                    var databases = await manager.DatabaseListAsync();

                    Assert.Empty(databases);

                    var config = new DatabaseConfiguration()
                    {
                         Name   = "foo",
                         Bucket = "foo-bucket",
                         Server = TestCluster.WalrusUri,
                         Sync   = null
                    };

                    await manager.DatabaseCreateAsync(config);

                    databases = await manager.DatabaseListAsync();

                    Assert.Single(databases);
                    Assert.Contains(databases, db => db == "foo");

                    var status = await manager.DatabaseStatusAsync("foo");

                    Assert.Equal("foo", status.Name);
                    Assert.True(status.CommitUpdateSequence >= 0);
                    Assert.True(status.UpdateSequence >= 0);
                    Assert.True(status.UpdateSequence >= status.CommitUpdateSequence);
                    Assert.Equal(DatabaseState.Online, status.State);
                    Assert.True(status.StartTimeUtc > new DateTime(2016, 5, 15));

                    await manager.DatabaseRemoveAsync("foo");

                    databases = await manager.DatabaseListAsync();

                    Assert.Empty(databases);
                }
            }
            finally
            {
                await TestCluster.ClearAsync();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task DatabaseConfig()
        {
            try
            {
                await TestCluster.ClearAsync();

                using (var gateway = TestCluster.CreateGateway())
                {
                    var manager   = gateway.CreateManager();
                    var databases = await manager.DatabaseListAsync();

                    Assert.Empty(databases);

                    var config = new DatabaseConfiguration()
                    {
                         Name   = "foo",
                         Bucket = "foo-bucket",
                         Server = TestCluster.WalrusUri,
                         Sync   = null
                    };

                    await manager.DatabaseCreateAsync(config);

                    databases = await manager.DatabaseListAsync();

                    Assert.Single(databases);
                    Assert.Contains(databases, db => db == "foo");

                    var status = await manager.DatabaseStatusAsync("foo");

                    Assert.Equal("foo", status.Name);
                    Assert.True(status.CommitUpdateSequence >= 0);
                    Assert.True(status.UpdateSequence >= 0);
                    Assert.True(status.UpdateSequence >= status.CommitUpdateSequence);
                    Assert.Equal(DatabaseState.Online, status.State);
                    Assert.True(status.StartTimeUtc > new DateTime(2016, 5, 15));

                    config.Bucket = "bar-bucket";

                    await manager.DatabaseConfigAsync("db", config);

                    // There isn't a way to tell that the bucket changed so
                    // we'll just verify that everything still looks OK.

                    status = await manager.DatabaseStatusAsync("foo");

                    Assert.Equal("foo", status.Name);
                    Assert.True(status.CommitUpdateSequence >= 0);
                    Assert.True(status.UpdateSequence >= 0);
                    Assert.True(status.UpdateSequence >= status.CommitUpdateSequence);
                    Assert.Equal(DatabaseState.Online, status.State);
                    Assert.True(status.StartTimeUtc > new DateTime(2016, 5, 15));
                }
            }
            finally
            {
                await TestCluster.ClearAsync();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task DatabaseCompact()
        {
            await TestCluster.ClearAsync();

            try
            {
                using (var gateway = TestCluster.CreateGateway())
                {
                    var manager = gateway.CreateManager();

                    await TestCluster.CreateDatabaseAsync("db");
                    await manager.DatabaseCompact("db");

                    while (true)
                    {
                        var status = await manager.DatabaseStatusAsync("db");

                        if (!status.IsCompacting)
                        {
                            break;
                        }

                        await Task.Delay(100);
                    }
                }
            }
            finally
            {
                await TestCluster.ClearAsync();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task DatabaseOnOffline()
        {
            await TestCluster.ClearAsync();

            try
            {
                using (var gateway = TestCluster.CreateGateway())
                {
                    var manager = gateway.CreateManager();

                    await TestCluster.CreateDatabaseAsync("db");

                    await manager.DatabaseOffline("db");
                    Assert.Equal(DatabaseState.Offline, (await manager.DatabaseStatusAsync("db")).State);

                    await manager.DatabaseOnline("db");
                    Assert.Equal(DatabaseState.Online, (await manager.DatabaseStatusAsync("db")).State);
                }
            }
            finally
            {
                await TestCluster.ClearAsync();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task DatabasePurge()
        {
            await TestCluster.ClearAsync();

            try
            {
                using (var gateway = TestCluster.CreateGateway())
                {
                    var manager = gateway.CreateManager();

                    await TestCluster.CreateDatabaseAsync("db");

                    await manager.DatabasePurgeAsync("db");
                }
            }
            finally
            {
                await TestCluster.ClearAsync();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task DatabaseResync()
        {
            await TestCluster.ClearAsync();

            try
            {
                using (var gateway = TestCluster.CreateGateway())
                {
                    var manager = gateway.CreateManager();

                    await TestCluster.CreateDatabaseAsync("db");

                    await manager.DatabaseOffline("db");
                    await manager.DatabaseResyncAsync("db");
                }
            }
            finally
            {
                await TestCluster.ClearAsync();
            }
        }
    }
}
