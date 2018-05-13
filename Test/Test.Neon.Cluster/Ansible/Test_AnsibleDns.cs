//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleDns.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;
using Neon.Xunit.Cluster;

using Xunit;

namespace TestNeonCluster
{
    public class Test_AnsibleDns : IClassFixture<ClusterFixture>
    {
        private ClusterFixture  cluster;
        private int             hostId = 0;

        /// <summary>
        /// Returns a unique host name for testing.
        /// </summary>
        /// <returns></returns>
        private string GetUniqueHost()
        {
            return $"neon-test-{hostId++}.com";
        }

        /// <summary>
        /// Determines whether the specified hostname has one or more
        /// DNS entries defined for it.
        /// </summary>
        /// <param name="host">The hostname.</param>
        /// <returns><c>true</c> if one or more entries exist.</returns>
        private bool DnsEntryExists(string host)
        {
            var response = cluster.NeonExecute("dns ls");

            Assert.True(response.ExitCode == 0);

            return response.OutputText.Contains($"{host}");
        }

        public Test_AnsibleDns(ClusterFixture cluster)
        {
            this.cluster = cluster;

            // We're going to use unique DNS hosts for each test
            // so we only need to reset the test fixture once for
            // all tests implemented by this class.

            cluster.LoginAndInitialize(login: null);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void CheckArgs()
        {
            var host = GetUniqueHost();

            // Verify that we can detect unknown top-level arguments.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage dns
      neon_dns:
        state: present
        hostname: {host}
        UNKNOWN: argument
        endpoints:
          target: 10.0.0.1
          check: yes
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage dns");

            Assert.False(taskResult.Success);

            // Verify that we can detect unknown endpoint arguments too.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage dns
      neon_dns:
        state: present
        hostname: {host}
        endpoints:
          target: 10.0.0.1
          check: yes
          UNKNOWN: argument
";
            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage dns");

            Assert.False(taskResult.Success);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create()
        {
            var host = GetUniqueHost();

            // Should start out without this entry.

            Assert.False(DnsEntryExists(host));

            // Create a DNS entry and then verify that it was added
            // and also that it resolves properly on manager, worker 
            // and pet nodes.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage dns
      neon_dns:
        state: present
        hostname: {host}
        endpoints:
          - target: 1.1.1.1
            check: no
";

            var results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("manage dns");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.True(DnsEntryExists(host));

            // Run the playbook again but this time nothing should
            // be changed because the DNS record already exists.

            results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("manage dns");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.True(DnsEntryExists(host));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Remove()
        {
            var host = GetUniqueHost();

            // Should start out without this entry.

            Assert.False(DnsEntryExists(host));

            // Create a DNS entry and then verify that it was added
            // and also that it resolves properly on manager, worker 
            // and pet nodes.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage dns
      neon_dns:
        state: present
        hostname: {host}
        endpoints:
          - target: 1.1.1.1
            check: no
";

            var results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("manage dns");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.True(DnsEntryExists(host));

            //-----------------------------------------------------------------
            // Run the playbook again but this time nothing should
            // be changed because the DNS record already exists.

            results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("manage dns");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.True(DnsEntryExists(host));

            //-----------------------------------------------------------------
            // Now delete and verify.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage dns
      neon_dns:
        state: absent
        hostname: {host}
        endpoints:
          - target: 1.1.1.1
            check: no
";

            results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("manage dns");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.False(DnsEntryExists(host));

            //-----------------------------------------------------------------
            // Run the playbook again but this time nothing should
            // be changed because the DNS record already exists.

            results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("manage dns");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.False(DnsEntryExists(host));
        }
    }
}
