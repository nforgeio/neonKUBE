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
                list.Add(index.indexes);    // Strip off the extra "indexes" level.
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

            return indexes.SingleOrDefault(index => ((string)index.name).Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// Verify that an index matches the specified keys,
        /// </summary>
        /// <param name="index">The index to be tested.</param>
        /// <param name="keys">The array of required key names.</param>
        private void VerifyIndexKeys(dynamic index, string[] keys)
        {
            var indexKeys = new Dictionary<string, bool>();
            var keyArray  = (JArray)index.index_key;

            if (keyArray != null)
            {
                foreach (string key in keyArray)
                {
                    indexKeys.Add(key, false);
                }
            }

            Assert.Equal(keys.Length, indexKeys.Count);

            // Note that the keys returned in the index are 
            // quoted with back-ticks.  We'll use CbHelper.LiteralName()
            // to convert the input key names.

            foreach (var key in keys)
            {
                indexKeys[CbHelper.LiteralName(key)] = true;
            }

            Assert.Equal(0, indexKeys.Values.Count(v => !v));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task PrimaryGsi()
        {
            // Verifies that we can create and remove primary GSI indexes.

            Assert.Empty(await ListIndexesAsync());

            //-----------------------------------------------------------------
            // Create a primary GSI index.

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
        primary: yes
        using: gsi
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            dynamic index = await GetIndexAsync("#primary");

            Assert.NotNull(index);
            Assert.True((bool)index.is_primary);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);

            //-----------------------------------------------------------------
            // Run the play again and verify that there was no change.

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            index = await GetIndexAsync("#primary");

            Assert.NotNull(index);
            Assert.True((bool)index.is_primary);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);

            //-----------------------------------------------------------------
            // Remove the index and verify.

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
        primary: yes
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            Assert.Null(await GetIndexAsync("#primary"));

            //-----------------------------------------------------------------
            // Remove it again and verify that there was no change.

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            Assert.Null(await GetIndexAsync("#primary"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task PrimaryGsiToView()
        {
            // Verifies that we can convert a primary GSI index
            // into a VIEW.

            Assert.Empty(await ListIndexesAsync());

            //-----------------------------------------------------------------
            // Create a primary GSI index.

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
        primary: yes
        using: gsi
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            dynamic index = await GetIndexAsync("#primary");

            Assert.NotNull(index);
            Assert.True((bool)index.is_primary);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);

            //-----------------------------------------------------------------
            // Convert to a VIEW.

            playbook =
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
        primary: yes
        using: view
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            index = await GetIndexAsync("#primary");

            Assert.NotNull(index);
            Assert.True((bool)index.is_primary);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("view", (string)index.@using);

            //-----------------------------------------------------------------
            // Run the play again and verify that there was no change.

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            index = await GetIndexAsync("#primary");

            Assert.NotNull(index);
            Assert.True((bool)index.is_primary);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("view", (string)index.@using);

            //-----------------------------------------------------------------
            // Remove the index and verify.

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
        primary: yes
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            Assert.Null(await GetIndexAsync("#primary"));

            //-----------------------------------------------------------------
            // Remove it again and verify that there was no change.

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            Assert.Null(await GetIndexAsync("#primary"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task PrimaryView()
        {
            // Verifies that we can create and remove primary VIEW indexes.

            Assert.Empty(await ListIndexesAsync());

            //-----------------------------------------------------------------
            // Create a primary VIEW index.

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
        primary: yes
        using: view
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
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

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            index = await GetIndexAsync("#primary");

            Assert.NotNull(index);
            Assert.True((bool)index.is_primary);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("view", (string)index.@using);

            //-----------------------------------------------------------------
            // Remove the index and verify.

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
        primary: yes
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            Assert.Null(await GetIndexAsync("#primary"));

            //-----------------------------------------------------------------
            // Remove it again and verify that there was no change.

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            Assert.Null(await GetIndexAsync("#primary"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task PrimaryViewToGsi()
        {
            // Verifies that we can convert a primary VIEW index 
            // into a GSI.

            Assert.Empty(await ListIndexesAsync());

            //-----------------------------------------------------------------
            // Create a primary VIEW index.

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
        primary: yes
        using: view
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            dynamic index = await GetIndexAsync("#primary");

            Assert.NotNull(index);
            Assert.True((bool)index.is_primary);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("view", (string)index.@using);

            //-----------------------------------------------------------------
            // Convert into a GSI.

            playbook =
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
        primary: yes
        using: gsi
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            index = await GetIndexAsync("#primary");

            Assert.NotNull(index);
            Assert.True((bool)index.is_primary);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);

            //-----------------------------------------------------------------
            // Run the play again and verify that there was no change.

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            index = await GetIndexAsync("#primary");

            Assert.NotNull(index);
            Assert.True((bool)index.is_primary);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);

            //-----------------------------------------------------------------
            // Remove the index and verify.

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
        primary: yes
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            Assert.Null(await GetIndexAsync("#primary"));

            //-----------------------------------------------------------------
            // Remove it again and verify that there was no change.

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            Assert.Null(await GetIndexAsync("#primary"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task SecondaryGsi()
        {
            // Verifies that we can create and remove secondary GSI indexes.

            Assert.Empty(await ListIndexesAsync());

            //-----------------------------------------------------------------
            // Create a secondary GSI index.

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
        name: test-index
        using: gsi
        keys:
          - name
          - age
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            dynamic index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);
            VerifyIndexKeys(index, new string[] { "name", "age" });

            //-----------------------------------------------------------------
            // Run the play again and verify that there was no change.

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);
            VerifyIndexKeys(index, new string[] { "name", "age" });

            //-----------------------------------------------------------------
            // Verify that [force=true] rebuilds the index.

            playbook =
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
        name: test-index
        using: gsi
        keys:
          - name
          - age
        force: yes
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);
            VerifyIndexKeys(index, new string[] { "name", "age" });

            //-----------------------------------------------------------------
            // Add an index key and verify.

            playbook =
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
        name: test-index
        using: gsi
        keys:
          - name
          - age
          - email
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);
            VerifyIndexKeys(index, new string[] { "name", "age", "email" });

            //-----------------------------------------------------------------
            // Remove a key and verify.

            playbook =
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
        name: test-index
        using: gsi
        keys:
          - name
          - age
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);
            VerifyIndexKeys(index, new string[] { "name", "age" });

            //-----------------------------------------------------------------
            // Run the play again and verify that there was no change.

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);
            VerifyIndexKeys(index, new string[] { "name", "age" });

            //-----------------------------------------------------------------
            // Add a WHERE clause and verify.

            playbook =
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
        name: test-index
        using: gsi
        keys:
          - name
          - age
        where: ""(65 <= `age`)""
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);
            Assert.Equal("(65 <= `age`)", (string)index.condition);

            //-----------------------------------------------------------------
            // Run the play again and verify that there was no change.

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);
            Assert.Equal("(65 <= `age`)", (string)index.condition);

            //-----------------------------------------------------------------
            // Remove the WHERE clause and verify.

            playbook =
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
        name: test-index
        using: gsi
        keys:
          - name
          - age
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);
            Assert.True(string.IsNullOrEmpty((string)index.condition));

            //-----------------------------------------------------------------
            // Run the play again and verify that there was no change.

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);
            Assert.True(string.IsNullOrEmpty((string)index.condition));

            //-----------------------------------------------------------------
            // Remove the index and verify.

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
        name: test-index
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            Assert.Null(await GetIndexAsync("test-index"));

            //-----------------------------------------------------------------
            // Remove it again and verify that there was no change.

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            Assert.Null(await GetIndexAsync("test-index"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task SecondaryView()
        {
            // Verifies that we can create and remove secondary VIEW indexes.

            Assert.Empty(await ListIndexesAsync());

            //-----------------------------------------------------------------
            // Create a secondary VIEW index.

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
        name: test-index
        using: view
        keys:
          - name
          - age
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            dynamic index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("view", (string)index.@using);
            VerifyIndexKeys(index, new string[] { "name", "age" });

            //-----------------------------------------------------------------
            // Run the play again and verify that there was no change.

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("view", (string)index.@using);
            VerifyIndexKeys(index, new string[] { "name", "age" });

            //-----------------------------------------------------------------
            // Verify that [force=true] rebuilds the index.

            playbook =
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
        name: test-index
        using: view
        keys:
          - name
          - age
        force: yes
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("view", (string)index.@using);
            VerifyIndexKeys(index, new string[] { "name", "age" });

            //-----------------------------------------------------------------
            // Add an index key and verify.

            playbook =
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
        name: test-index
        using: view
        keys:
          - name
          - age
          - email
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("view", (string)index.@using);
            VerifyIndexKeys(index, new string[] { "name", "age", "email" });

            //-----------------------------------------------------------------
            // Remove a key and verify.

            playbook =
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
        name: test-index
        using: view
        keys:
          - name
          - age
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("view", (string)index.@using);
            VerifyIndexKeys(index, new string[] { "name", "age" });

            //-----------------------------------------------------------------
            // Run the play again and verify that there was no change.

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("view", (string)index.@using);
            VerifyIndexKeys(index, new string[] { "name", "age" });

            //-----------------------------------------------------------------
            // Add a WHERE clause and verify.

            playbook =
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
        name: test-index
        using: view
        keys:
          - name
          - age
        where: ""(65 <= `age`)""
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("view", (string)index.@using);
            Assert.Equal("(65 <= `age`)", (string)index.condition);

            //-----------------------------------------------------------------
            // Run the play again and verify that there was no change.

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("view", (string)index.@using);
            Assert.Equal("(65 <= `age`)", (string)index.condition);

            //-----------------------------------------------------------------
            // Remove the WHERE clause and verify.

            playbook =
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
        name: test-index
        using: view
        keys:
          - name
          - age
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("view", (string)index.@using);
            Assert.True(string.IsNullOrEmpty((string)index.condition));

            //-----------------------------------------------------------------
            // Run the play again and verify that there was no change.

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("view", (string)index.@using);
            Assert.True(string.IsNullOrEmpty((string)index.condition));

            //-----------------------------------------------------------------
            // Remove the index and verify.

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
        name: test-index
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            Assert.Null(await GetIndexAsync("test-index"));

            //-----------------------------------------------------------------
            // Remove it again and verify that there was no change.

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            Assert.Null(await GetIndexAsync("test-index"));
        }
    }
}
