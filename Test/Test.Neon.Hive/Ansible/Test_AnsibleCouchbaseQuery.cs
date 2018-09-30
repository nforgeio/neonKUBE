//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleCouchbaseQuery.cs
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
using Neon.Xunit.Couchbase;
using Neon.Xunit.Hive;

using Xunit;
using Neon.Data;

namespace TestHive
{
    public class Test_AnsibleCouchbaseQuery : IClassFixture<CouchbaseFixture>
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

        private const int docCount = 100;

        private CouchbaseFixture    couchbase;
        private NeonBucket          bucket;

        public Test_AnsibleCouchbaseQuery(CouchbaseFixture couchbase)
        {
            this.couchbase = couchbase;

            // These tests are all read-only so we're going to initialize the
            // database once with some test data.

            if (couchbase.Start())
            {
                var block = new List<IDocument<TestInfo>>();

                for (int docNum = 0; docNum < docCount; docNum++)
                {
                    var doc = new Document<TestInfo>();

                    doc.Id = Guid.NewGuid().ToString("D");
                    doc.Content = new TestInfo();
                    doc.Content.name = $"name-{docNum.ToString("000#")}";
                    doc.Content.age = docNum;

                    block.Add(doc);
                }

                couchbase.Bucket.InsertSafeAsync<TestInfo>(block).Wait();
            }

            bucket = couchbase.Bucket;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void QueryLines()
        {
            //-----------------------------------------------------------------
            // Verify that [json-lines] is the default output format.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: query
      neon_couchbase_query:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        query: ""select {bucket.Name}.* from {bucket.Name} order by name asc""
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("query");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            Assert.False(taskResult.OutputText.Trim().StartsWith("["));

            using (var reader = new StringReader(taskResult.OutputText))
            {
                var count = 0;

                foreach (var line in reader.Lines())
                {
                    var item = NeonHelper.JsonDeserialize<TestInfo>(line);

                    Assert.Equal($"name-{count.ToString("000#")}", item.name);
                    count++;
                }

                Assert.Equal(docCount, count);
            }

            //-----------------------------------------------------------------
            // Verify that we can limit the number of documents returned.

            var limit = docCount / 2;

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: query
      neon_couchbase_query:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        query: ""select {bucket.Name}.* from {bucket.Name} order by name asc""
        limit: {limit}
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("query");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            Assert.False(taskResult.OutputText.Trim().StartsWith("["));

            using (var reader = new StringReader(taskResult.OutputText))
            {
                var count = 0;

                foreach (var line in reader.Lines())
                {
                    var item = NeonHelper.JsonDeserialize<TestInfo>(line);

                    Assert.Equal($"name-{count.ToString("000#")}", item.name);
                    count++;
                }

                Assert.Equal(limit, count);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void QueryLinesToFile()
        {
            //-----------------------------------------------------------------
            // Verify that [json-lines] is the default output format and that
            // we can write the output a file.

            using (var folder = new TempFolder())
            {
                var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: query
      neon_couchbase_query:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        query: ""select {bucket.Name}.* from {bucket.Name} order by name asc""
        output: output.json
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook);
                var taskResult = results.GetTaskResult("query");

                Assert.True(taskResult.Success);
                Assert.False(taskResult.Changed);

                Assert.False(taskResult.OutputText.Trim().StartsWith("["));

                using (var reader = new StreamReader(Path.Combine(folder.Path, "output.json")))
                {
                    var count = 0;

                    foreach (var line in reader.Lines())
                    {
                        var item = NeonHelper.JsonDeserialize<TestInfo>(line);

                        Assert.Equal($"name-{count.ToString("000#")}", item.name);
                        count++;
                    }

                    Assert.Equal(docCount, count);
                }
            }

            //-----------------------------------------------------------------
            // Verify that we can limit the number of documents returned.

            using (var folder = new TempFolder())
            {
                var limit = docCount / 2;

                var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: query
      neon_couchbase_query:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        query: ""select {bucket.Name}.* from {bucket.Name} order by name asc""
        limit: {limit}
        format: json-lines
        output: output.json
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook);
                var taskResult = results.GetTaskResult("query");

                Assert.True(taskResult.Success);
                Assert.False(taskResult.Changed);

                Assert.False(taskResult.OutputText.Trim().StartsWith("["));

                using (var reader = new StreamReader(Path.Combine(folder.Path, "output.json")))
                {
                    var count = 0;

                    foreach (var line in reader.Lines())
                    {
                        var item = NeonHelper.JsonDeserialize<TestInfo>(line);

                        Assert.Equal($"name-{count.ToString("000#")}", item.name);
                        count++;
                    }

                    Assert.Equal(limit, count);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void QueryArray()
        {
            //-----------------------------------------------------------------
            // Verify that [json-array] works.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: query
      neon_couchbase_query:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        query: ""select {bucket.Name}.* from {bucket.Name} order by name asc""
        format: json-array
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("query");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            Assert.StartsWith("[", taskResult.OutputText.Trim());

            var array = NeonHelper.JsonDeserialize<TestInfo[]>(taskResult.OutputText);

            Assert.Equal(docCount, array.Length);

            for (int i = 0; i < array.Length; i++)
            {
                var item = array[i];

                Assert.Equal($"name-{i.ToString("000#")}", item.name);
            }

            //-----------------------------------------------------------------
            // Verify that we can limit the number of documents returned.

            var limit = docCount / 2;

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: query
      neon_couchbase_query:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        query: ""select {bucket.Name}.* from {bucket.Name} order by name asc""
        limit: {limit}
        format: json-array
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("query");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            Assert.StartsWith("[", taskResult.OutputText.Trim());

            array = NeonHelper.JsonDeserialize<TestInfo[]>(taskResult.OutputText);

            Assert.Equal(limit, array.Length);

            for (int i = 0; i < array.Length; i++)
            {
                var item = array[i];

                Assert.Equal($"name-{i.ToString("000#")}", item.name);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void QueryArrayToFile()
        {
            //-----------------------------------------------------------------
            // Verify that [json-array] works and we can write to a file.

            using (var folder = new TempFolder())
            {
                var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: query
      neon_couchbase_query:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        query: ""select {bucket.Name}.* from {bucket.Name} order by name asc""
        format: json-array
        output: output.json
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook);
                var taskResult = results.GetTaskResult("query");

                Assert.True(taskResult.Success);
                Assert.False(taskResult.Changed);

                var outputText = File.ReadAllText(Path.Combine(folder.Path, "output.json"));

                Assert.StartsWith("[", outputText.Trim());

                var array = NeonHelper.JsonDeserialize<TestInfo[]>(outputText);

                Assert.Equal(docCount, array.Length);

                for (int i = 0; i < array.Length; i++)
                {
                    var item = array[i];

                    Assert.Equal($"name-{i.ToString("000#")}", item.name);
                }
            }

            //-----------------------------------------------------------------
            // Verify that we can limit the number of documents returned.

            using (var folder = new TempFolder())
            {
                var limit = docCount / 2;

                var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: query
      neon_couchbase_query:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        query: ""select {bucket.Name}.* from {bucket.Name} order by name asc""
        limit: {limit}
        format: json-array
        output: output.json
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook);
                var taskResult = results.GetTaskResult("query");

                Assert.True(taskResult.Success);
                Assert.False(taskResult.Changed);

                var outputText = File.ReadAllText(Path.Combine(folder.Path, "output.json"));

                Assert.StartsWith("[", outputText.Trim());

                var array = NeonHelper.JsonDeserialize<TestInfo[]>(outputText);

                Assert.Equal(limit, array.Length);

                for (int i = 0; i < array.Length; i++)
                {
                    var item = array[i];

                    Assert.Equal($"name-{i.ToString("000#")}", item.name);
                }
            }
        }
    }
}
