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

using Consul;

using Neon.Cluster;
using Neon.Common;
using Neon.Xunit;
using Neon.Xunit.Cluster;

using Xunit;

namespace TestNeonCluster
{
    public class Test_AnsibleDns : IClassFixture<ClusterFixture>
    {
        //---------------------------------------------------------------------
        // Static members

        private static int hostId = 0;

        //---------------------------------------------------------------------
        // Instance members

        private ClusterFixture  cluster;

        public Test_AnsibleDns(ClusterFixture cluster)
        {
            this.cluster = cluster;

            // We're going to use unique DNS hosts for each test
            // so we only need to reset the test fixture once for
            // all tests implemented by this class.

            cluster.LoginAndInitialize(login: null);
        }

        /// <summary>
        /// Returns a unique hostname for testing.
        /// </summary>
        /// <returns></returns>
        private string GetHostname()
        {
            return $"neon-test-{hostId++}.com";
        }

        /// <summary>
        /// Returns the DNS entry for a hostname.
        /// </summary>
        /// <param name="host">The hostname.</param>
        /// <returns>The <see cref="DnsEntry"/> or <c>null</c>.</returns>
        private DnsEntry GetDnsEntry(string host)
        {
            return cluster.Consul.KV.GetObjectOrDefault<DnsEntry>($"{NeonClusterConst.ConsulDnsEntriesKey}/{host}").Result;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void CheckArgs()
        {
            var host = GetHostname();

            //-----------------------------------------------------------------
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
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage dns");

            Assert.False(taskResult.Success);

            //-----------------------------------------------------------------
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
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("manage dns");

            Assert.False(taskResult.Success);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create()
        {
            var host = GetHostname();

            // Should start out without this entry.

            Assert.Null(GetDnsEntry(host));

            //-----------------------------------------------------------------
            // Create a DNS entry and then verify that it was added.

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
            var results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("manage dns");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            
            var entry = GetDnsEntry(host);

            Assert.NotNull(entry);
            Assert.Equal("1.1.1.1", entry.Endpoints.Single().Target);
            Assert.False(entry.Endpoints.Single().Check);

            //-----------------------------------------------------------------
            // Run the playbook again but this time nothing should
            // be changed because the DNS record already exists.

            results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("manage dns");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            entry = GetDnsEntry(host);

            Assert.NotNull(entry);
            Assert.Equal("1.1.1.1", entry.Endpoints.Single().Target);
            Assert.False(entry.Endpoints.Single().Check);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Update()
        {
            var host = GetHostname();

            // Should start out without this entry.

            Assert.Null(GetDnsEntry(host));

            // Create a DNS entry and then verify that it was added.

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
            var results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("manage dns");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            var entry = GetDnsEntry(host);

            Assert.NotNull(entry);
            Assert.Equal("1.1.1.1", entry.Endpoints.Single().Target);
            Assert.False(entry.Endpoints.Single().Check);

            //-----------------------------------------------------------------
            // Update the dashboard and verify.

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
          - target: 2.2.2.2
            check: yes
";
            results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("manage dns");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            entry = GetDnsEntry(host);

            Assert.NotNull(entry);
            Assert.Equal("2.2.2.2", entry.Endpoints.Single().Target);
            Assert.True(entry.Endpoints.Single().Check);

            //-----------------------------------------------------------------
            // Run the playbook again but this time nothing should
            // have changed.

            results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("manage dns");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            entry = GetDnsEntry(host);

            Assert.NotNull(entry);
            Assert.Equal("2.2.2.2", entry.Endpoints.Single().Target);
            Assert.True(entry.Endpoints.Single().Check);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Remove()
        {
            var host = GetHostname();

            // Should start out without this entry.

            Assert.Null(GetDnsEntry(host));

            //-----------------------------------------------------------------
            // Create a DNS entry and then verify that it was added.

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
          - target: 2.2.2.2
            check: no
";
            var results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("manage dns");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            var entry = GetDnsEntry(host);

            Assert.NotNull(entry);
            Assert.Equal("2.2.2.2", entry.Endpoints.Single().Target);

            //-----------------------------------------------------------------
            // Run the playbook again but this time nothing should
            // be changed because the DNS record already exists.

            results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("manage dns");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            entry = GetDnsEntry(host);

            Assert.NotNull(entry);
            Assert.Equal("2.2.2.2", entry.Endpoints.Single().Target);

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
          - target: 2.2.2.2
            check: no
";
            results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("manage dns");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Null(GetDnsEntry(host));

            //-----------------------------------------------------------------
            // Run the playbook again but this time nothing should
            // be changed because the DNS record already exists.

            results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("manage dns");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Null(GetDnsEntry(host));
        }
    }
}
