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

            if (!couchbase.Start(noPrimary: true))
            {
                // Flush the database if we didn't just start it.

                couchbase.Flush();
            }

            // This needs to be assigned outside of the initialization action
            // so that the bucket will be available for every test.

            bucket = couchbase.Bucket;
        }

        /// <summary>
        /// Lists the indexes for the test bucket.
        /// </summary>
        /// <returns>The list of index information.</returns>
        private async Task<List<dynamic>> ListIndexesAsync()
        {
            var indexes = await bucket.QuerySafeAsync<dynamic>(new QueryRequest($"select * from system:indexes where keyspace_id='{bucket.Name}'"));
            var list    = new List<dynamic>();

            foreach (var index in indexes)
            {
                list.Add(index.indexes);    // Get rid of the extra "indexes" level.
            }

            return list;
        }

        /// <summary>
        /// Returns information about a named Couchbase index for the test bucket.
        /// </summary>
        /// <param name="name">The index name.</param>
        /// <returns>
        /// The index information as a <c>dynamic</c> or <c>null</c> 
        /// if the index doesn't exist.
        /// </returns>
        private async Task<dynamic> GetIndexAsync(string name)
        {
            var indexes = await ListIndexesAsync();

            return indexes.SingleOrDefault(i => (string)i.name == name);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task PrimaryGSI()
        {
            // First, verify that the bucket does not already have
            // a primary index.  This should not be the case because
            // we indicated that when we started Couchbase fixture
            // in the constructor above.

            Assert.Empty(await ListIndexesAsync());

            // Create a primary index.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: index
      neon_couchbase_index:
        state: present
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        name: test_primary
        primary: yes
        using: gsi
";
            var results = AnsiblePlayer.Play(playbook);
            var taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            dynamic index = await GetIndexAsync("test_primary");

            Assert.NotNull(index);
            Assert.True((bool)index.is_primary);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);

            //-----------------------------------------------------------------
            // Run the play again and verify that there was no change.

            results = AnsiblePlayer.Play(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            index = await GetIndexAsync("test_primary");

            Assert.NotNull(index);
            Assert.True((bool)index.is_primary);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);

            //-----------------------------------------------------------------
            // Remove it and verify.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: index
      neon_couchbase_index:
        state: absent
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        name: test_primary
";
            results = AnsiblePlayer.Play(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            Assert.Null(await GetIndexAsync("test_primary"));

            //-----------------------------------------------------------------
            // Remove it again and verify that there was no change.

            results = AnsiblePlayer.Play(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            Assert.Null(await GetIndexAsync("test_primary"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task PrimaryVIEW()
        {
            // First, verify that the bucket does not already have
            // a primary index.  This should not be the case because
            // we indicated that when we started Couchbase fixture
            // in the constructor above.

            Assert.Empty(await ListIndexesAsync());

            // Create a primary index.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: index
      neon_couchbase_index:
        state: present
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        name: test_primary
        primary: yes
        using: view
";
            var results = AnsiblePlayer.Play(playbook);
            var taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            dynamic index = await GetIndexAsync("#primary");

            Assert.NotNull(index);
            Assert.True((bool)index.is_primary);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("view", (string)index.@using);

            //-----------------------------------------------------------------
            // Run the play again and verify that there was no change.

            results = AnsiblePlayer.Play(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            index = await GetIndexAsync("#primary");

            Assert.NotNull(index);
            Assert.True((bool)index.is_primary);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("view", (string)index.@using);

            //-----------------------------------------------------------------
            // Remove it and verify.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: index
      neon_couchbase_index:
        state: absent
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
";
            results = AnsiblePlayer.Play(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            Assert.Null(await GetIndexAsync("#primary"));

            //-----------------------------------------------------------------
            // Remove it again and verify that there was no change.

            results = AnsiblePlayer.Play(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            Assert.Null(await GetIndexAsync("#primary"));
        }
    }
}
