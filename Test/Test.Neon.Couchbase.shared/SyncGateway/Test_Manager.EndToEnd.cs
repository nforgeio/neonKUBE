//-----------------------------------------------------------------------------
// FILE:	    Test_Manager.EndToEnd.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase;
using Couchbase.Lite;
using Couchbase.Lite.Auth;

using Neon.Common;
using Neon.Couchbase.SyncGateway;
using Neon.Xunit;

using Xunit;

namespace TestSyncGateway
{
    public class Test_ManagerEndToEnd
    {
        private TimeSpan MaxWait = TimeSpan.FromSeconds(15);

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task SingleUser()
        {
            // Perform an end-to-end integration test where we provision
            // the Sync Gateway with a user and verify that document R/W
            // and replication works.

            string      tempFolder = null;
            Database    db0        = null;
            Database    db1        = null;
            Manager     manager    = null;

            try
            {
                tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

                Directory.CreateDirectory(tempFolder);

                await TestCluster.ClearAsync();

                using (var gateway = TestCluster.CreateGateway())
                {
                    var gatewayManager = gateway.CreateManager();

                    await TestCluster.CreateDatabaseAsync("db");

                    await gatewayManager.RoleCreateAsync("db", 
                        new RoleProperties()
                        {
                            Name          = "role0",
                            AdminChannels = new List<string>() { $"role-channel" }
                        });

                    await gatewayManager.UserCreateAsync("db",
                        new UserProperties()
                        {
                            Name          = "user0",
                            Password      = "password0",
                            Email         = "user0@gmail.com",
                            IsDisabled    = false,
                            AdminChannels = new List<string>() { "user-channel" },
                            Roles         = new List<string>() { "role0" }
                        });

                    // Create two local databases for the same user.

                    manager = new Manager(new DirectoryInfo(tempFolder), new ManagerOptions());

                    var auth = AuthenticatorFactory.CreateBasicAuthenticator("user0", "password0");

                    db0 = manager.GetDatabase("db0");
                    db1 = manager.GetDatabase("db1");

                    var db0ExternalChange = false;
                    var db1ExternalChange = false;

                    db0.Changed +=
                        (s, a) =>
                        {
                            Debug.Write($"*** db0: Count={a.Changes.Count()} External={a.IsExternal}");

                            if (a.Changes.Count() > 0 && a.IsExternal)
                            {
                                db0ExternalChange = true;
                            }
                        };

                    db1.Changed +=
                        (s, a) =>
                        {
                            Debug.Write($"*** db1: Count={a.Changes.Count()} External={a.IsExternal}");

                            if (a.Changes.Count() > 0 && a.IsExternal)
                            {
                                db1ExternalChange = true;
                            }
                        };

                    // Here's the meat of the test:
                    //
                    //      1. Create push and pull replicators for each database.
                    //      2. Write a document to one database
                    //      3. Verify that the source database's push replicator pushed the document
                    //      4. ...and the target database's pull replicator pulled the document

                    var db0Pull = db0.CreatePullReplication(new Uri(gateway.GetDatabaseUri("db")));

                    db0Pull.Authenticator = auth;
                    db0Pull.Continuous    = true;
                    db0Pull.Start();

                    var db0Push = db0.CreatePushReplication(new Uri(gateway.GetDatabaseUri("db")));

                    db0Push.Authenticator = auth;
                    db0Push.Continuous    = true;
                    db0Push.Start();

                    var db1Pull = db1.CreatePullReplication(new Uri(gateway.GetDatabaseUri("db")));

                    db1Pull.Authenticator = auth;
                    db1Pull.Continuous    = true;
                    db1Pull.Start();

                    var db1Push = db1.CreatePushReplication(new Uri(gateway.GetDatabaseUri("db")));

                    db1Push.Authenticator = auth;
                    db1Push.Continuous    = true;
                    db1Push.Start();

                    // Write a document to db0 and wait for the replication to db1.

                    var source     = db0.GetDocument("doc-0");
                    var properties = new Dictionary<string, object>()
                    {
                        { "+c", "doc0" },
                        { "+ch", new string[] { "user-channel" } }
                    };

                    source.PutProperties(properties);
                    var target = db0.GetDocument("doc-0");

                    Assert.Equal("doc0", target.Properties["+c"]);

                    NeonHelper.WaitFor(() => !db0Push.IsDocumentPending(target), MaxWait);
                    NeonHelper.WaitFor(() => db1ExternalChange, MaxWait);

                    target = db1.GetDocument("doc-0");

                    Assert.Equal("doc0", target.Properties["+c"]);

                    // Write a document to db1 and wait for the replication to db0.

                    db0ExternalChange = db1ExternalChange = false;

                    source     = db1.GetDocument("doc-1");
                    properties = new Dictionary<string, object>()
                    {
                        { "+c", "doc1" },
                        { "+ch", new string[] { "user-channel" } }
                    };

                    source.PutProperties(properties);
                    target = db1.GetDocument("doc-1");

                    Assert.Equal("doc1", target.Properties["+c"]);

                    NeonHelper.WaitFor(() => !db1Push.IsDocumentPending(target), MaxWait);
                    NeonHelper.WaitFor(() => db0ExternalChange, MaxWait);

                    target = db1.GetDocument("doc-1");

                    Assert.Equal("doc1", target.Properties["+c"]);
                }
            }
            finally
            {
                if (db0 != null)
                {
                    db0.Dispose();
                }

                if (db1 != null)
                {
                    db1.Dispose();
                }

                if (manager != null)
                {
                    manager.Close();
                }

                await TestCluster.ClearAsync();
                Directory.Delete(tempFolder, recursive: true);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCouchbase)]
        public async Task MultiUser()
        {
            // Perform an end-to-end integration test where we provision
            // the Sync Gateway with two users that explicitly share a
            // channel and also indirectly share a channel via a role.
            //
            // Then, we're going to verify that documents can be read and
            // written end-to-end.

            string      tempFolder = null;
            Database    db0        = null;
            Database    db1        = null;
            Manager     manager    = null;

            try
            {
                tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

                Directory.CreateDirectory(tempFolder);

                await TestCluster.ClearAsync();

                using (var gateway = TestCluster.CreateGateway())
                {
                    var gatewayManager = gateway.CreateManager();

                    await TestCluster.CreateDatabaseAsync("db");

                    await gatewayManager.RoleCreateAsync("db", 
                        new RoleProperties()
                        {
                            Name          = "role0",
                            AdminChannels = new List<string>() { $"role-channel" }
                        });

                    for (int i = 0; i < 2; i++)
                    {
                        await gatewayManager.UserCreateAsync("db",
                            new UserProperties()
                            {
                                Name          = $"user{i}",
                                Password      = $"password{i}",
                                Email         = $"user{i}@gmail.com",
                                IsDisabled    = false,
                                AdminChannels = new List<string>() { "user-channel" },
                                Roles         = new List<string>() { "role0" }
                            });
                    }

                    // Create separate Couchbase Lite databases for each user.

                    manager = new Manager(new DirectoryInfo(tempFolder), new ManagerOptions());

                    var auth0 = AuthenticatorFactory.CreateBasicAuthenticator("user0", "password0");
                    var auth1 = AuthenticatorFactory.CreateBasicAuthenticator("user1", "password1");

                    db0 = manager.GetDatabase("user0");
                    db1 = manager.GetDatabase("user1");

                    var db0ExternalChange = false;
                    var db1ExternalChange = false;

                    db0.Changed +=
                        (s, a) =>
                        {
                            Debug.Write($"*** db0: Count={a.Changes.Count()} External={a.IsExternal}");

                            if (a.Changes.Count() > 0 && a.IsExternal)
                            {
                                db0ExternalChange = true;
                            }
                        };

                    db1.Changed +=
                        (s, a) =>
                        {
                            Debug.Write($"*** db1: Count={a.Changes.Count()} External={a.IsExternal}");

                            if (a.Changes.Count() > 0 && a.IsExternal)
                            {
                                db1ExternalChange = true;
                            }
                        };

                    // Here's the meat of the test:
                    //
                    //      1. Create push and pull replicators for each database.
                    //      2. Write a document to one user's database
                    //      3. Verify that the source database's push replicator pushed the document
                    //      4. ...and the orher user's database's pull replicator pulled the document

                    var db0Pull = db0.CreatePullReplication(new Uri(gateway.GetDatabaseUri("db")));

                    db0Pull.Authenticator = auth0;
                    db0Pull.Continuous    = true;
                    db0Pull.Start();

                    var db0Push = db0.CreatePushReplication(new Uri(gateway.GetDatabaseUri("db")));

                    db0Push.Authenticator = auth0;
                    db0Push.Continuous    = true;
                    db0Push.Start();

                    var db1Pull = db1.CreatePullReplication(new Uri(gateway.GetDatabaseUri("db")));

                    db1Pull.Authenticator = auth1;
                    db1Pull.Continuous    = true;
                    db1Pull.Start();

                    var db1Push = db1.CreatePushReplication(new Uri(gateway.GetDatabaseUri("db")));

                    db1Push.Authenticator = auth1;
                    db1Push.Continuous    = true;
                    db1Push.Start();

                    // Write a document to db0 and wait for the replication to db1.

                    var source     = db0.GetDocument("doc-0");
                    var properties = new Dictionary<string, object>()
                    {
                        { "+c", "doc0" },
                        { "+ch", new string[] { "user-channel" } }
                    };

                    source.PutProperties(properties);
                    var target = db0.GetDocument("doc-0");

                    Assert.Equal("doc0", target.Properties["+c"]);

                    NeonHelper.WaitFor(() => !db0Push.IsDocumentPending(target), MaxWait);
                    NeonHelper.WaitFor(() => db1ExternalChange, MaxWait);

                    target = db1.GetDocument("doc-0");

                    Assert.Equal("doc0", target.Properties["+c"]);

                    // Write a document to db1 and wait for the replication to db0.

                    db0ExternalChange = db1ExternalChange = false;

                    source     = db1.GetDocument("doc-1");
                    properties = new Dictionary<string, object>()
                    {
                        { "+c", "doc1" },
                        { "+ch", new string[] { "user-channel" } }
                    };

                    source.PutProperties(properties);
                    target = db1.GetDocument("doc-1");

                    Assert.Equal("doc1", target.Properties["+c"]);

                    NeonHelper.WaitFor(() => !db1Push.IsDocumentPending(target), MaxWait);
                    NeonHelper.WaitFor(() => db0ExternalChange, MaxWait);

                    target = db1.GetDocument("doc-1");

                    Assert.Equal("doc1", target.Properties["+c"]);
                }
            }
            finally
            {
                if (db0 != null)
                {
                    db0.Dispose();
                }

                if (db1 != null)
                {
                    db1.Dispose();
                }

                if (manager != null)
                {
                    manager.Close();
                }

                await TestCluster.ClearAsync();
                Directory.Delete(tempFolder, recursive: true);
            }
        }
    }
}
