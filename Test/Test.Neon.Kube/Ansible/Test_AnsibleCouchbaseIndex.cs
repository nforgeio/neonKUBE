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
using Neon.Retry;
using Neon.Xunit;
using Neon.Xunit.Couchbase;
using Neon.Xunit.Hive;

using Xunit;
using Neon.Data;
using System.Threading;

namespace TestHive
{
    public class Test_AnsibleCouchbaseIndex : IClassFixture<CouchbaseFixture>
    {
        //---------------------------------------------------------------------
        // Internal types

        public class TestInfo
        {
            public string name { get; set; }
            public long age { get; set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        private CouchbaseFixture    couchbase;
        private NeonBucket          bucket;

        public Test_AnsibleCouchbaseIndex(CouchbaseFixture couchbase)
        {
            this.couchbase = couchbase;

            // Note that the database bucket will be created without a 
            // primary index.

            if (!couchbase.Start(noPrimary: true))
            {
                // Clear the database if we didn't just start it.

                couchbase.Clear();
            }

            bucket = couchbase.Bucket;
        }

        /// <summary>
        /// Lists the indexes for the test bucket.
        /// </summary>
        /// <returns>The list of index information.</returns>
        private async Task<List<dynamic>> ListIndexesAsync()
        {
            // $hack(jeff.lill):
            //
            // This index query can fail for a random period of time after resetting
            // Couchbase or after creating an index.  We're going to retry for up to
            // 60 seconds until the query succeeds.

            dynamic indexes = null;

            await NeonBucket.ReadyRetry.InvokeAsync(
                async () =>
                {
                    indexes = await bucket.QuerySafeAsync<dynamic>(new QueryRequest($"select * from system:indexes where keyspace_id='{bucket.Name}'"));
                });

            var list = new List<dynamic>();

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

        /// <summary>
        /// Waits for an index to report as a specific state.
        /// </summary>
        /// <param name="name">The index name.</param>
        /// <param name="state">The desired index state.</param>
        /// <param name="failState">
        /// Optionally specifies the state the index should never reach.
        /// This typically indicates that we missed a state transition
        /// because the operation completed too quickly.
        /// </param>
        private async Task WaitForIndexStateAsync(string name, string state, string failState = null)
        {
            await NeonHelper.WaitForAsync(
                async () =>
                {
                    dynamic index = await GetIndexAsync(name);

                    Assert.NotNull(index);

                    if (failState != null && (string)index.state == failState)
                    {
                        Assert.True(false, $"Unexpected index [state={failState}].  Operation may have completed too quickly.");
                    }

                    return (string)index.state == state;
                },
                timeout: TimeSpan.FromDays(365),
                pollTime: TimeSpan.FromSeconds(1));
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

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task SecondaryGsiToView()
        {
            // Verifies that we can convert secondary GSI index
            // into a VIEW.

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
        keys:
          - name
          - age
        using: gsi
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            dynamic index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.False((bool?)index.is_primary ?? false);
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
        name: test-index
        keys:
          - name
          - age
        using: view
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.False((bool?)index.is_primary ?? false);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("view", (string)index.@using);

            //-----------------------------------------------------------------
            // Run the play again and verify that there was no change.

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.False((bool?)index.is_primary ?? false);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("view", (string)index.@using);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task SecondaryViewToGsi()
        {
            // Verifies that we can convert secondary GSI index
            // into a VIEW.

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
        keys:
          - name
          - age
        using: view
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            dynamic index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.False((bool?)index.is_primary ?? false);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("view", (string)index.@using);

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
        name: test-index
        keys:
          - name
          - age
        using: gsi
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.False((bool?)index.is_primary ?? false);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);

            //-----------------------------------------------------------------
            // Run the play again and verify that there was no change.

            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("index");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            index = await GetIndexAsync("test-index");

            Assert.NotNull(index);
            Assert.False((bool?)index.is_primary ?? false);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task GsiReplicas()
        {
            // Verify that we can specify the number of index replicas.

            // $todo(jeff.lill):
            //
            // We really can't test this without deploying a multi-node
            // Couchbase test cluster.  I don't really want to devote
            // any effort towards that right now.
            //
            // Instead, we'll just set [replicas: 1] to ensure that
            // we can at least parse the argument and then do the
            // same for [nodes].

            Assert.Empty(await ListIndexesAsync());

            //-----------------------------------------------------------------
            // Test: replicas=0

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
        replicas: 0
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
            // Test: nodes = 127.0.0.1:8091

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
        nodes:
          - 127.0.0.1:8091
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
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task GsiDeferBuild()
        {
            // Verify that we can defer building GSI indexes and then
            // build them with [state=build].

            Assert.Empty(await ListIndexesAsync());

            //-----------------------------------------------------------------
            // Load  the bucket with lots of data so that the index build will take
            // some time complete so we can verify that the [build_wait] parameter
            // works,
            //
            // This is a bit fragile and we may need to increase the number of
            // docs saved in the future as computer and disk performance improves.

            const int blockCount   = 100;
            const int docsPerBlock = 1000;

            var block = new List<IDocument<TestInfo>>();

            for (int blockNum = 0; blockNum <= blockCount; blockNum++)
            {
                block.Clear();

                for (int docNum = 0; docNum < docsPerBlock; docNum++)
                {
                    var doc = new Document<TestInfo>();

                    doc.Id           = Guid.NewGuid().ToString("D");
                    doc.Content      = new TestInfo();
                    doc.Content.name = $"name-{(long)blockCount * (long)docNum}";
                    doc.Content.age  = docNum;

                    block.Add(doc);
                }

                await bucket.InsertSafeAsync<TestInfo>(block);
            }

            //-----------------------------------------------------------------
            // Create two deferred indexes and verify that they're actually deferred.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: name-index
      neon_couchbase_index:
        state: present
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        name: name-index
        using: gsi
        keys:
          - name
        build_defer: yes
    - name: age-index
      neon_couchbase_index:
        state: present
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        name: age-index
        using: gsi
        keys:
          - age
        build_defer: yes
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("name-index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            dynamic index = await GetIndexAsync("name-index");

            Assert.NotNull(index);
            Assert.Equal("deferred", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);
            VerifyIndexKeys(index, new string[] { "name" });

            taskResult = results.GetTaskResult("age-index");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            index = await GetIndexAsync("age-index");

            Assert.NotNull(index);
            Assert.Equal("deferred", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);
            VerifyIndexKeys(index, new string[] { "age" });

            //-----------------------------------------------------------------
            // Build the deferred indexes.  Note that [build_defer: no] and 
            // [build_wait: yes] are is the defaults, so the indexes should be 
            // online when the module returns.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: build
      neon_couchbase_index:
        state: build
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("build");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            index = await GetIndexAsync("name-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);
            VerifyIndexKeys(index, new string[] { "name" });

            index = await GetIndexAsync("age-index");

            Assert.NotNull(index);
            Assert.Equal("online", (string)index.state);
            Assert.Equal("gsi", (string)index.@using);
            VerifyIndexKeys(index, new string[] { "age" });

            //-----------------------------------------------------------------
            // Test [build_wait: no]

            // Start out by removing and then recreating the indexes (deferred).

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: remove name-index
      neon_couchbase_index:
        state: absent
        name: name-index        
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}

    - name: remove age-index
      neon_couchbase_index:
        state: absent
        name: age-index        
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}

    - name: name-index
      neon_couchbase_index:
        state: present
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        name: name-index
        using: gsi
        keys:
          - name
        build_defer: yes

    - name: age-index
      neon_couchbase_index:
        state: present
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        name: age-index
        using: gsi
        keys:
          - age
        build_defer: yes

    - name: build indexes
      neon_couchbase_index:
        state: build
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        build_wait: no
";
            results = AnsiblePlayer.PlayNoGather(playbook);

            foreach (var result in results.TaskResults)
            {
                Assert.True(result.Success);
            }

            // Wait for the indexes to transition to [building].

            await WaitForIndexStateAsync("name-index", "building", failState: "online");
            await WaitForIndexStateAsync("age-index", "building", failState: "online");

            // Ensure that the indexes transition to [online].

            await WaitForIndexStateAsync("name-index", "online");
            await WaitForIndexStateAsync("age-index", "online");
        }
    }
}
