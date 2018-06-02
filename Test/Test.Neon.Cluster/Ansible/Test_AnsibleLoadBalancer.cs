//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleLoadBalancer.cs
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

using Neon.Data;
using Neon.Cluster;

using Xunit;

namespace TestNeonCluster
{
    public class Test_AnsibleLoadBalancer : IClassFixture<ClusterFixture>
    {
        private ClusterFixture  fixture;
        private ClusterProxy    cluster;

        public Test_AnsibleLoadBalancer(ClusterFixture fixture)
        {
            if (!fixture.LoginAndInitialize())
            {
                fixture.ClearLoadBalancers();
            }

            this.fixture = fixture;
            this.cluster = fixture.Cluster;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Public()
        {
            //-----------------------------------------------------------------
            // Verify that we can add a simple public load balancer rule.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_load_balancer:
        name: public
        state: present
        rule_name: test
        rule:
          mode: http
          frontends:
            - host: test.com
          backends:
            - server: www.google.com
              port: 80
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            var rule = (LoadBalancerHttpRule)cluster.PublicLoadBalancer.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(LoadBalancerMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(NeonHostPorts.ProxyPublicHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("www.google.com", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);

            //-----------------------------------------------------------------
            // Add the same rule and verify that no change was detected this time.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_load_balancer:
        name: public
        state: present
        rule_name: test
        rule:
          mode: http
          frontends:
            - host: test.com
          backends:
            - server: www.google.com
              port: 80
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            rule = (LoadBalancerHttpRule)cluster.PublicLoadBalancer.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(LoadBalancerMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(NeonHostPorts.ProxyPublicHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("www.google.com", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);

            //-----------------------------------------------------------------
            // Add the same rule and use [force: yes] to force an update.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_load_balancer:
        name: public
        state: present
        rule_name: test
        rule:
          mode: http
          frontends:
            - host: test.com
          backends:
            - server: www.google.com
              port: 80
        force: yes
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (LoadBalancerHttpRule)cluster.PublicLoadBalancer.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(LoadBalancerMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(NeonHostPorts.ProxyPublicHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("www.google.com", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);

            //-----------------------------------------------------------------
            // Modify the rule and verify that a change was detected.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_load_balancer:
        name: public
        state: present
        rule_name: test
        rule:
          mode: http
          frontends:
            - host: test.com
          backends:
            - server: bing.com
              port: 80
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (LoadBalancerHttpRule)cluster.PublicLoadBalancer.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(LoadBalancerMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(NeonHostPorts.ProxyPublicHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("bing.com", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);

            //-----------------------------------------------------------------
            // Delete the rule and verify.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_load_balancer:
        name: public
        state: absent
        rule_name: test
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (LoadBalancerHttpRule)cluster.PublicLoadBalancer.GetRule("test");

            Assert.Null(rule);

            //-----------------------------------------------------------------
            // Delete the rule again and verify that there was no change.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_load_balancer:
        name: public
        state: absent
        rule_name: test
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            rule = (LoadBalancerHttpRule)cluster.PublicLoadBalancer.GetRule("test");

            Assert.Null(rule);

            //-----------------------------------------------------------------
            // Delete the rule again [force: yes] and verify the change.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_load_balancer:
        name: public
        state: absent
        rule_name: test
        force: yes
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (LoadBalancerHttpRule)cluster.PublicLoadBalancer.GetRule("test");

            Assert.Null(rule);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Private()
        {
            //-----------------------------------------------------------------
            // Verify that we can add a simple public load balancer rule.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_load_balancer:
        name: private
        state: present
        rule_name: test
        rule:
          mode: http
          frontends:
            - host: test.com
          backends:
            - server: www.google.com
              port: 80
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            var rule = (LoadBalancerHttpRule)cluster.PrivateLoadBalancer.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(LoadBalancerMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(NeonHostPorts.ProxyPrivateHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("www.google.com", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);

            //-----------------------------------------------------------------
            // Add the same rule and verify that no change was detected this time.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_load_balancer:
        name: private
        state: present
        rule_name: test
        rule:
          mode: http
          frontends:
            - host: test.com
          backends:
            - server: www.google.com
              port: 80
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            rule = (LoadBalancerHttpRule)cluster.PrivateLoadBalancer.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(LoadBalancerMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(NeonHostPorts.ProxyPrivateHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("www.google.com", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);

            //-----------------------------------------------------------------
            // Add the same rule and use [force: yes] to force an update.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_load_balancer:
        name: private
        state: present
        rule_name: test
        rule:
          mode: http
          frontends:
            - host: test.com
          backends:
            - server: www.google.com
              port: 80
        force: yes
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (LoadBalancerHttpRule)cluster.PrivateLoadBalancer.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(LoadBalancerMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(NeonHostPorts.ProxyPrivateHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("www.google.com", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);

            //-----------------------------------------------------------------
            // Modify the rule and verify that a change was detected.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_load_balancer:
        name: private
        state: present
        rule_name: test
        rule:
          mode: http
          frontends:
            - host: test.com
          backends:
            - server: bing.com
              port: 80
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (LoadBalancerHttpRule)cluster.PrivateLoadBalancer.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(LoadBalancerMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(NeonHostPorts.ProxyPrivateHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("bing.com", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);

            //-----------------------------------------------------------------
            // Delete the rule and verify.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_load_balancer:
        name: private
        state: absent
        rule_name: test
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (LoadBalancerHttpRule)cluster.PrivateLoadBalancer.GetRule("test");

            Assert.Null(rule);

            //-----------------------------------------------------------------
            // Delete the rule again and verify that there was no change.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_load_balancer:
        name: private
        state: absent
        rule_name: test
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            rule = (LoadBalancerHttpRule)cluster.PrivateLoadBalancer.GetRule("test");

            Assert.Null(rule);

            //-----------------------------------------------------------------
            // Delete the rule again [force: yes] and verify the change.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_load_balancer:
        name: private
        state: absent
        rule_name: test
        force: yes
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (LoadBalancerHttpRule)cluster.PrivateLoadBalancer.GetRule("test");

            Assert.Null(rule);
        }
    }
}
