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
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Cluster;
using Neon.Xunit.Couchbase;

using Xunit;

namespace TestSamples
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
                // Flush the database if we didn't just start it.

                couchbase.Flush();
            }

            // This needs to be assigned outside of the initialization action
            // so that the bucket will be available for every test.

            bucket = couchbase.Bucket;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void LinesEmpty()
        {
            // Verify that we can import a file with one JSON object per line,
            // generating a UUID key for each.

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
                var results = AnsiblePlayer.PlayInFolder(folder.Path, playbook);
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
        servers: {couchbase.Settings.Servers.First()}
        bucket: {bucket.Name}
        username: {couchbase.Username}
        password: {couchbase.Password}
        source: {Path.GetFileName(jsonFile)}
        format: json-lines
";
                var results = AnsiblePlayer.PlayInFolder(folder.Path, playbook);
                var taskResult = results.GetTaskResult("manage service");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                // Verify

            }
        }
    }
}
