//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleCouchbaseImport.cs
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
    public class Test_AnsibleCouchbaseImport : IClassFixture<CouchbaseFixture>
    {
        private CouchbaseFixture    couchbase;
        private NeonBucket          bucket;

        public Test_AnsibleCouchbaseImport(CouchbaseFixture couchbase)
        {
            this.couchbase = couchbase;

            if (!couchbase.Start())
            {
                // Clear the database if we didn't just start it.

                couchbase.Clear();
            }

            bucket = couchbase.Bucket;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void LinesEmpty()
        {
            // Verify that we can import a file no JSON data.

            using (var folder = new TempFolder())
            {
                var jsonFile = Path.Combine(folder.Path, "lines.json");

                // An empty file with a blank line thrown in for good measure.
                // The module is supposed to ignore blank lines for [json-lines]
                // mode and should return [changed=false] when it didn't
                // import anything.

                File.WriteAllText(jsonFile,
@"
");
                var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: import
      neon_couchbase_import:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        source: {Path.GetFileName(jsonFile)}
        format: json-lines
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook);
                var taskResult = results.GetTaskResult("import");

                Assert.True(taskResult.Success);
                Assert.False(taskResult.Changed);   // Should be false because we didn't import anything.
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task LinesUuid()
        {
            // Verify that we can import a file with one JSON object per line,
            // generating a UUID key for each.

            using (var folder = new TempFolder())
            {
                var jsonFile = Path.Combine(folder.Path, "lines.json");

                File.WriteAllText(jsonFile,
@"{ ""name"": ""jack"", ""age"": 10 }
{ ""name"": ""jill"", ""age"": 11 }
{ ""name"": ""spot"", ""age"": 2 }
");
                var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: import
      neon_couchbase_import:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        source: {Path.GetFileName(jsonFile)}
        format: json-lines
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook);
                var taskResult = results.GetTaskResult("import");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                // Verify

                var query = new QueryRequest($"select meta({bucket.Name}).id, {bucket.Name}.* from {bucket.Name}")
                    .ScanConsistency(ScanConsistency.RequestPlus);

                var items = await bucket.QuerySafeAsync<dynamic>(query);

                // Verify that we have all of documents.

                Assert.Equal(3, items.Count);
                Assert.Single(items.Where(doc => doc.name == "jack"));
                Assert.Single(items.Where(doc => doc.name == "jill"));
                Assert.Single(items.Where(doc => doc.name == "spot"));

                // Verify that all of the IDs look like UUIDs.  We're
                // going to assume that all entity UUIDs have the same
                // length for this test.

                var keyLength = EntityHelper.CreateUuid().Length;

                Assert.Equal(3, items.Count(doc => ((string)doc.id).Length == keyLength));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task LinesUuidTemplate()
        {
            // Verify that we can import a file with one JSON object per line,
            // generating a templated UUID key for each.

            using (var folder = new TempFolder())
            {
                var jsonFile = Path.Combine(folder.Path, "lines.json");

                File.WriteAllText(jsonFile,
@"{ ""name"": ""jack"", ""age"": 10 }
{ ""name"": ""jill"", ""age"": 11 }
{ ""name"": ""spot"", ""age"": 2 }
");
                var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: import
      neon_couchbase_import:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        source: {Path.GetFileName(jsonFile)}
        format: json-lines
        key: ""ID-#UUID#""
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook);
                var taskResult = results.GetTaskResult("import");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                // Verify

                var query = new QueryRequest($"select meta({bucket.Name}).id, {bucket.Name}.* from {bucket.Name}")
                    .ScanConsistency(ScanConsistency.RequestPlus);

                var items = await bucket.QuerySafeAsync<dynamic>(query);

                // Verify that we have all of documents.

                Assert.Equal(3, items.Count);
                Assert.Single(items.Where(doc => doc.name == "jack"));
                Assert.Single(items.Where(doc => doc.name == "jill"));
                Assert.Single(items.Where(doc => doc.name == "spot"));

                // Verify that all of the IDs look like UUIDs.  We're
                // going to assume that all entity UUIDs have the same
                // length for this test.

                var keyLength = "ID-".Length + EntityHelper.CreateUuid().Length;

                Assert.Equal(3, items.Count(doc => ((string)doc.id).Length == keyLength && ((string)doc.id).StartsWith("ID-")));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task LinesInteger1()
        {
            // Verify that we can import a file with one JSON object per line,
            // generating an integer key starting at 1 for each.

            using (var folder = new TempFolder())
            {
                var jsonFile = Path.Combine(folder.Path, "lines.json");

                File.WriteAllText(jsonFile,
@"{ ""name"": ""jack"", ""age"": 10 }
{ ""name"": ""jill"", ""age"": 11 }
{ ""name"": ""spot"", ""age"": 2 }
");
                var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: import
      neon_couchbase_import:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        source: {Path.GetFileName(jsonFile)}
        format: json-lines
        key: ""#MONO_INCR#""
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook);
                var taskResult = results.GetTaskResult("import");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                // Verify

                var query = new QueryRequest($"select meta({bucket.Name}).id, {bucket.Name}.* from {bucket.Name}")
                    .ScanConsistency(ScanConsistency.RequestPlus);

                var items = await bucket.QuerySafeAsync<dynamic>(query);

                // Verify that we have all of documents.

                Assert.Equal(3, items.Count);
                Assert.Single(items.Where(doc => doc.name == "jack"));
                Assert.Single(items.Where(doc => doc.name == "jill"));
                Assert.Single(items.Where(doc => doc.name == "spot"));

                // Verify that that the documents were assigned the
                // expected IDs.

                Assert.Equal("1", (string)items.Single(doc => doc.name == "jack").id);
                Assert.Equal("2", (string)items.Single(doc => doc.name == "jill").id);
                Assert.Equal("3", (string)items.Single(doc => doc.name == "spot").id);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task LinesInteger1000()
        {
            // Verify that we can import a file with one JSON object per line,
            // generating an integer key starting at 1000 for each.

            using (var folder = new TempFolder())
            {
                var jsonFile = Path.Combine(folder.Path, "lines.json");

                File.WriteAllText(jsonFile,
@"{ ""name"": ""jack"", ""age"": 10 }
{ ""name"": ""jill"", ""age"": 11 }
{ ""name"": ""spot"", ""age"": 2 }
");
                var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: import
      neon_couchbase_import:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        source: {Path.GetFileName(jsonFile)}
        format: json-lines
        key: ""#MONO_INCR#""
        first_key: 1000
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook);
                var taskResult = results.GetTaskResult("import");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                // Verify

                var query = new QueryRequest($"select meta({bucket.Name}).id, {bucket.Name}.* from {bucket.Name}")
                    .ScanConsistency(ScanConsistency.RequestPlus);

                var items = await bucket.QuerySafeAsync<dynamic>(query);

                // Verify that we have all of documents.

                Assert.Equal(3, items.Count);
                Assert.Single(items.Where(doc => doc.name == "jack"));
                Assert.Single(items.Where(doc => doc.name == "jill"));
                Assert.Single(items.Where(doc => doc.name == "spot"));

                // Verify that that the documents were assigned the
                // expected IDs.

                Assert.Equal("1000", (string)items.Single(doc => doc.name == "jack").id);
                Assert.Equal("1001", (string)items.Single(doc => doc.name == "jill").id);
                Assert.Equal("1002", (string)items.Single(doc => doc.name == "spot").id);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task LinesEmbeddedKey()
        {
            // Verify that we can import a file with one JSON object per line,
            // with the key specified by a special [@@key] property.

            using (var folder = new TempFolder())
            {
                var jsonFile = Path.Combine(folder.Path, "lines.json");

                File.WriteAllText(jsonFile,
@"{ ""@@key"": ""id-jack"", ""name"": ""jack"", ""age"": 10 }
{ ""@@key"": ""id-jill"", ""name"": ""jill"", ""age"": 11 }
{ ""@@key"": ""id-spot"", ""name"": ""spot"", ""age"": 2 }
");
                var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: import
      neon_couchbase_import:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        source: {Path.GetFileName(jsonFile)}
        format: json-lines
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook);
                var taskResult = results.GetTaskResult("import");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                // Verify

                var query = new QueryRequest($"select meta({bucket.Name}).id, {bucket.Name}.* from {bucket.Name}")
                    .ScanConsistency(ScanConsistency.RequestPlus);

                var items = await bucket.QuerySafeAsync<dynamic>(query);

                // Verify that we have all of documents.

                Assert.Equal(3, items.Count);
                Assert.Single(items.Where(doc => doc.name == "jack"));
                Assert.Single(items.Where(doc => doc.name == "jill"));
                Assert.Single(items.Where(doc => doc.name == "spot"));

                // Verify that that the documents were assigned the
                // expected IDs.

                Assert.Equal("id-jack", (string)items.Single(doc => doc.name == "jack").id);
                Assert.Equal("id-jill", (string)items.Single(doc => doc.name == "jill").id);
                Assert.Equal("id-spot", (string)items.Single(doc => doc.name == "spot").id);

                // Verify that the special [@@key] fields were removed.

                Assert.Null(((JObject)items.Single(doc => doc.name == "jack")).Property("@@key"));
                Assert.Null(((JObject)items.Single(doc => doc.name == "jill")).Property("@@key"));
                Assert.Null(((JObject)items.Single(doc => doc.name == "spot")).Property("@@key"));
            }
        }

        //============================================================================================

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void ArrayEmpty()
        {
            // Verify that we can import a file no JSON data.

            using (var folder = new TempFolder())
            {
                var jsonFile = Path.Combine(folder.Path, "lines.json");

                // An empty array.

                File.WriteAllText(jsonFile,
@"
[
]
");
                var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: import
      neon_couchbase_import:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        source: {Path.GetFileName(jsonFile)}
        format: json-array
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook);
                var taskResult = results.GetTaskResult("import");

                Assert.True(taskResult.Success);
                Assert.False(taskResult.Changed);   // Should be false because we didn't import anything.
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task ArrayUuid()
        {
            // Verify that we can import a file with an array of JSON
            // objects, generating a UUID key for each.

            using (var folder = new TempFolder())
            {
                var jsonFile = Path.Combine(folder.Path, "lines.json");

                File.WriteAllText(jsonFile,
@"[
    { ""name"": ""jack"", ""age"": 10 },
    { ""name"": ""jill"", ""age"": 11 },
    { ""name"": ""spot"", ""age"": 2 }
]
");
                var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: import
      neon_couchbase_import:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        source: {Path.GetFileName(jsonFile)}
        format: json-array
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook);
                var taskResult = results.GetTaskResult("import");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                // Verify

                var query = new QueryRequest($"select meta({bucket.Name}).id, {bucket.Name}.* from {bucket.Name}")
                    .ScanConsistency(ScanConsistency.RequestPlus);

                var items = await bucket.QuerySafeAsync<dynamic>(query);

                // Verify that we have all of documents.

                Assert.Equal(3, items.Count);
                Assert.Single(items.Where(doc => doc.name == "jack"));
                Assert.Single(items.Where(doc => doc.name == "jill"));
                Assert.Single(items.Where(doc => doc.name == "spot"));

                // Verify that all of the IDs look like UUIDs.  We're
                // going to assume that all entity UUIDs have the same
                // length for this test.

                var keyLength = EntityHelper.CreateUuid().Length;

                Assert.Equal(3, items.Count(doc => ((string)doc.id).Length == keyLength));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task ArrayUuidTemplate()
        {
            // Verify that we can import a file with an array of JSON
            // objects, generating a templated UUID key for each.

            using (var folder = new TempFolder())
            {
                var jsonFile = Path.Combine(folder.Path, "lines.json");

                File.WriteAllText(jsonFile,
@"[
    { ""name"": ""jack"", ""age"": 10 },
    { ""name"": ""jill"", ""age"": 11 },
    { ""name"": ""spot"", ""age"": 2 }
]
");
                var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: import
      neon_couchbase_import:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        source: {Path.GetFileName(jsonFile)}
        format: json-array
        key: ""ID-#UUID#""
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook);
                var taskResult = results.GetTaskResult("import");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                // Verify

                var query = new QueryRequest($"select meta({bucket.Name}).id, {bucket.Name}.* from {bucket.Name}")
                    .ScanConsistency(ScanConsistency.RequestPlus);

                var items = await bucket.QuerySafeAsync<dynamic>(query);

                // Verify that we have all of documents.

                Assert.Equal(3, items.Count);
                Assert.Single(items.Where(doc => doc.name == "jack"));
                Assert.Single(items.Where(doc => doc.name == "jill"));
                Assert.Single(items.Where(doc => doc.name == "spot"));

                // Verify that all of the IDs look like UUIDs.  We're
                // going to assume that all entity UUIDs have the same
                // length for this test.

                var keyLength = "ID-".Length + EntityHelper.CreateUuid().Length;

                Assert.Equal(3, items.Count(doc => ((string)doc.id).Length == keyLength && ((string)doc.id).StartsWith("ID-")));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task ArrayUuidTemplateEscaped()
        {
            // Verify that we can import a file with an array of JSON
            // objects, generating a templated UUID key for each.
            //
            // This test verifies that we can escape the special 
            // "#" and "%" characters.

            using (var folder = new TempFolder())
            {
                var jsonFile = Path.Combine(folder.Path, "lines.json");

                File.WriteAllText(jsonFile,
@"[
    { ""name"": ""jack"", ""age"": 10 },
    { ""name"": ""jill"", ""age"": 11 },
    { ""name"": ""spot"", ""age"": 2 }
]
");
                var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: import
      neon_couchbase_import:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        source: {Path.GetFileName(jsonFile)}
        format: json-array
        key: ""##%%-#UUID#""
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook);
                var taskResult = results.GetTaskResult("import");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                // Verify

                var query = new QueryRequest($"select meta({bucket.Name}).id, {bucket.Name}.* from {bucket.Name}")
                    .ScanConsistency(ScanConsistency.RequestPlus);

                var items = await bucket.QuerySafeAsync<dynamic>(query);

                // Verify that we have all of documents.

                Assert.Equal(3, items.Count);
                Assert.Single(items.Where(doc => doc.name == "jack"));
                Assert.Single(items.Where(doc => doc.name == "jill"));
                Assert.Single(items.Where(doc => doc.name == "spot"));

                // Verify that all of the IDs look like UUIDs.  We're
                // going to assume that all entity UUIDs have the same
                // length for this test.

                var keyLength = "#%-".Length + EntityHelper.CreateUuid().Length;

                Assert.Equal(3, items.Count(doc => ((string)doc.id).Length == keyLength && ((string)doc.id).StartsWith("#%-")));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task ArrayInteger1()
        {
            // Verify that we can import a file with an array of
            // JSON objects, generating an integer key starting at 1 for each.

            using (var folder = new TempFolder())
            {
                var jsonFile = Path.Combine(folder.Path, "lines.json");

                File.WriteAllText(jsonFile,
@"[
    { ""name"": ""jack"", ""age"": 10 },
    { ""name"": ""jill"", ""age"": 11 },
    { ""name"": ""spot"", ""age"": 2 }
]
");
                var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: import
      neon_couchbase_import:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        source: {Path.GetFileName(jsonFile)}
        format: json-array
        key: ""#MONO_INCR#""
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook);
                var taskResult = results.GetTaskResult("import");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                // Verify

                var query = new QueryRequest($"select meta({bucket.Name}).id, {bucket.Name}.* from {bucket.Name}")
                    .ScanConsistency(ScanConsistency.RequestPlus);

                var items = await bucket.QuerySafeAsync<dynamic>(query);

                // Verify that we have all of documents.

                Assert.Equal(3, items.Count);
                Assert.Single(items.Where(doc => doc.name == "jack"));
                Assert.Single(items.Where(doc => doc.name == "jill"));
                Assert.Single(items.Where(doc => doc.name == "spot"));

                // Verify that that the documents were assigned the
                // expected IDs.

                Assert.Equal("1", (string)items.Single(doc => doc.name == "jack").id);
                Assert.Equal("2", (string)items.Single(doc => doc.name == "jill").id);
                Assert.Equal("3", (string)items.Single(doc => doc.name == "spot").id);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task ArrayInteger1000()
        {
            // Verify that we can import a file with an array of
            // JSON obhjects, generating an integer key starting 
            // at 1000 for each.

            using (var folder = new TempFolder())
            {
                var jsonFile = Path.Combine(folder.Path, "lines.json");

                File.WriteAllText(jsonFile,
@"[
    { ""name"": ""jack"", ""age"": 10 },
    { ""name"": ""jill"", ""age"": 11 },
    { ""name"": ""spot"", ""age"": 2 }
]
");
                var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: import
      neon_couchbase_import:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        source: {Path.GetFileName(jsonFile)}
        format: json-array
        key: ""#MONO_INCR#""
        first_key: 1000
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook);
                var taskResult = results.GetTaskResult("import");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                // Verify

                var query = new QueryRequest($"select meta({bucket.Name}).id, {bucket.Name}.* from {bucket.Name}")
                    .ScanConsistency(ScanConsistency.RequestPlus);

                var items = await bucket.QuerySafeAsync<dynamic>(query);

                // Verify that we have all of documents.

                Assert.Equal(3, items.Count);
                Assert.Single(items.Where(doc => doc.name == "jack"));
                Assert.Single(items.Where(doc => doc.name == "jill"));
                Assert.Single(items.Where(doc => doc.name == "spot"));

                // Verify that that the documents were assigned the
                // expected IDs.

                Assert.Equal("1000", (string)items.Single(doc => doc.name == "jack").id);
                Assert.Equal("1001", (string)items.Single(doc => doc.name == "jill").id);
                Assert.Equal("1002", (string)items.Single(doc => doc.name == "spot").id);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task ArrayEmbeddedKey()
        {
            // Verify that we can import a file with an array of
            // JSON objects with the key specified by a special
            // [@@key] property.

            using (var folder = new TempFolder())
            {
                var jsonFile = Path.Combine(folder.Path, "lines.json");

                File.WriteAllText(jsonFile,
@"[
    { ""@@key"": ""id-jack"", ""name"": ""jack"", ""age"": 10 },
    { ""@@key"": ""id-jill"", ""name"": ""jill"", ""age"": 11 },
    { ""@@key"": ""id-spot"", ""name"": ""spot"", ""age"": 2 }
]
");
                var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: import
      neon_couchbase_import:
        servers:
          - {couchbase.Settings.Servers.First().Host}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        source: {Path.GetFileName(jsonFile)}
        format: json-array
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook);
                var taskResult = results.GetTaskResult("import");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                // Verify

                var query = new QueryRequest($"select meta({bucket.Name}).id, {bucket.Name}.* from {bucket.Name}")
                    .ScanConsistency(ScanConsistency.RequestPlus);

                var items = await bucket.QuerySafeAsync<dynamic>(query);

                // Verify that we have all of documents.

                Assert.Equal(3, items.Count);
                Assert.Single(items.Where(doc => doc.name == "jack"));
                Assert.Single(items.Where(doc => doc.name == "jill"));
                Assert.Single(items.Where(doc => doc.name == "spot"));

                // Verify that that the documents were assigned the
                // expected IDs.

                Assert.Equal("id-jack", (string)items.Single(doc => doc.name == "jack").id);
                Assert.Equal("id-jill", (string)items.Single(doc => doc.name == "jill").id);
                Assert.Equal("id-spot", (string)items.Single(doc => doc.name == "spot").id);

                // Verify that the special [@@key] fields were removed.

                Assert.Null(((JObject)items.Single(doc => doc.name == "jack")).Property("@@key"));
                Assert.Null(((JObject)items.Single(doc => doc.name == "jill")).Property("@@key"));
                Assert.Null(((JObject)items.Single(doc => doc.name == "spot")).Property("@@key"));
            }
        }
    }
}
