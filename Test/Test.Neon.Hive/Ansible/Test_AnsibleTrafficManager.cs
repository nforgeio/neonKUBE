//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleTrafficManager.cs
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
using Neon.Data;
using Neon.Hive;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Couchbase;
using Neon.Xunit.Hive;

using Xunit;

namespace TestHive
{
    public class Test_AnsibleTrafficManager : IClassFixture<HiveFixture>
    {
        private HiveFixture     hiveFixture;
        private HiveProxy       hive;

        public Test_AnsibleTrafficManager(HiveFixture fixture)
        {
            if (!fixture.LoginAndInitialize())
            {
                fixture.ClearTrafficDirectors();
            }

            this.hiveFixture = fixture;
            this.hive        = fixture.Hive;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Public()
        {
            //-----------------------------------------------------------------
            // Verify that we can add a simple public traffic manager rule.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
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

            var rule = (TrafficManagerHttpRule)hive.PublicTraffic.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(TrafficManagerMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(HiveHostPorts.ProxyPublicHttp, rule.Frontends.First().ProxyPort);
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
      neon_traffic_manager:
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

            rule = (TrafficManagerHttpRule)hive.PublicTraffic.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(TrafficManagerMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(HiveHostPorts.ProxyPublicHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("www.google.com", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);

            //-----------------------------------------------------------------
            // Add the same rule and use [state: update] to force an update.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
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
    - name: update
      neon_traffic_manager:
        name: public
        state: update
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            rule = (TrafficManagerHttpRule)hive.PublicTraffic.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(TrafficManagerMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(HiveHostPorts.ProxyPublicHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("www.google.com", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);

            taskResult = results.GetTaskResult("update");
            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            
            //-----------------------------------------------------------------
            // Modify the rule and verify that a change was detected.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
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

            rule = (TrafficManagerHttpRule)hive.PublicTraffic.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(TrafficManagerMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(HiveHostPorts.ProxyPublicHttp, rule.Frontends.First().ProxyPort);
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
      neon_traffic_manager:
        name: public
        state: absent
        rule_name: test
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (TrafficManagerHttpRule)hive.PublicTraffic.GetRule("test");

            Assert.Null(rule);

            //-----------------------------------------------------------------
            // Delete the rule again and verify that there was no change.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: public
        state: absent
        rule_name: test
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            rule = (TrafficManagerHttpRule)hive.PublicTraffic.GetRule("test");

            Assert.Null(rule);

            //-----------------------------------------------------------------
            // Delete the rule again and then use [state: update] and verify the change.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: public
        state: absent
        rule_name: test
    - name: update
      neon_traffic_manager:
        name: public
        state: update
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            taskResult = results.GetTaskResult("update");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (TrafficManagerHttpRule)hive.PublicTraffic.GetRule("test");

            Assert.Null(rule);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Private()
        {
            //-----------------------------------------------------------------
            // Verify that we can add a simple public traffic manager rule.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
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

            var rule = (TrafficManagerHttpRule)hive.PrivateTraffic.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(TrafficManagerMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(HiveHostPorts.ProxyPrivateHttp, rule.Frontends.First().ProxyPort);
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
      neon_traffic_manager:
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

            rule = (TrafficManagerHttpRule)hive.PrivateTraffic.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(TrafficManagerMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(HiveHostPorts.ProxyPrivateHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("www.google.com", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);

            //-----------------------------------------------------------------
            // Add the same rule and then use [state: update] to force an update.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
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
    - name: update
      neon_traffic_manager:
        name: private
        state: update
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            taskResult = results.GetTaskResult("update");
            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (TrafficManagerHttpRule)hive.PrivateTraffic.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(TrafficManagerMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(HiveHostPorts.ProxyPrivateHttp, rule.Frontends.First().ProxyPort);
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
      neon_traffic_manager:
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

            rule = (TrafficManagerHttpRule)hive.PrivateTraffic.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(TrafficManagerMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(HiveHostPorts.ProxyPrivateHttp, rule.Frontends.First().ProxyPort);
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
      neon_traffic_manager:
        name: private
        state: absent
        rule_name: test
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (TrafficManagerHttpRule)hive.PrivateTraffic.GetRule("test");

            Assert.Null(rule);

            //-----------------------------------------------------------------
            // Delete the rule again and verify that there was no change.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: private
        state: absent
        rule_name: test
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            rule = (TrafficManagerHttpRule)hive.PrivateTraffic.GetRule("test");

            Assert.Null(rule);

            //-----------------------------------------------------------------
            // Delete the rule again and then use [state: update] for force an update.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: private
        state: absent
        rule_name: test
    - name: update
      neon_traffic_manager:
        name: private
        state: update
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            taskResult = results.GetTaskResult("update");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (TrafficManagerHttpRule)hive.PrivateTraffic.GetRule("test");

            Assert.Null(rule);
        }
    }
}
