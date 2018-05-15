//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleCouchbaseIndex.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase;
using Couchbase.Core;
using Couchbase.Linq;
using Couchbase.Linq.Extensions;
using Couchbase.N1QL;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Cluster;
using Neon.Xunit.Couchbase;

using Xunit;
using Neon.Data;

namespace TestSamples
{
    public class Test_AnsibleCouchbaseIndex : IClassFixture<CouchbaseFixture>
    {
        private CouchbaseFixture    couchbase;
        private NeonBucket          bucket;

        public Test_AnsibleCouchbaseIndex(CouchbaseFixture couchbase)
        {
            this.couchbase = couchbase;

            // Note that the database bucket will be created without a 
            // primary index.

            if (!couchbase.Start(/* primaryIndex: null */))
            {
                // Flush the database if we didn't just start it.

                couchbase.Flush();
            }

            // This needs to be assigned outside of the initialization action
            // so that the bucket will be available for every test.

            bucket = couchbase.Bucket;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task PrimaryCreate()
        {
            // First, verify that the bucket does not already have
            // a primary index.  This should not be the case because
            // we indicated that when we started Couchbase fixture
            // in the constructor above.

            var query = new QueryRequest($"select name from system:indexes where keyspace_id='bucket.Name'")
                .ScanConsistency(ScanConsistency.RequestPlus);

            var items = await bucket.QuerySafeAsync<dynamic>(query);

            Assert.Empty(items);

            // Create a primary index.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: index
      neon_couchbase_index:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
";
            var results = AnsiblePlayer.Play(playbook);
            var taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);   // Should be false because we didn't import anything.
        }
    }
}
