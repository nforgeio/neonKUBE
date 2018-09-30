//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleGlobals.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Consul;

using Neon.Common;
using Neon.Hive;
using Neon.Xunit;
using Neon.Xunit.Hive;

using Xunit;

namespace TestHive
{
    public class Test_AnsibleGlobals : IClassFixture<HiveFixture>
    {
        private HiveFixture     hiveFixture;
        private HiveProxy       hive;

        public Test_AnsibleGlobals(HiveFixture fixture)
        {
            // We're going to use unique dashboard name for each test
            // so we only need to reset the test fixture once for
            // all tests implemented by this class.

            fixture.LoginAndInitialize(login: null);

            this.hiveFixture = fixture;
            this.hive        = fixture.Hive;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void SetUser()
        {
            // Verify that we can set a valid user modifiable settings.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: globals
      neon_globals:
        state: set
        name: allow-unit-testing
        value: yes
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("globals");

            Assert.True(taskResult.Success);

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: globals
      neon_globals:
        state: set
        name: disable-auto-unseal
        value: false
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("globals");

            Assert.True(taskResult.Success);

            // Verify that we check setting values.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: globals
      neon_globals:
        state: set
        name: allow-unit-testing
        value: INVALID
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("globals");

            Assert.False(taskResult.Success);

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: globals
      neon_globals:
        state: set
        name: log-retention-days
        value: INVALID
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("globals");

            Assert.False(taskResult.Success);

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: globals
      neon_globals:
        state: set
        name: log-retention-days
        value: -1
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("globals");

            Assert.False(taskResult.Success);


            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: globals
      neon_globals:
        state: set
        name: log-retention-days
        value: 0
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("globals");

            Assert.False(taskResult.Success);

            // Verify that we don't allow changing non-user modifiable settings.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: globals
      neon_globals:
        state: set
        name: NOT-USER-MODIFIABLE
        value: yes
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("globals");

            Assert.False(taskResult.Success);

            // Verify that we check for unknown arguments.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: globals
      neon_globals:
        state: set
        name: allow-unit-testing
        value: yes
        UNKNOWN: ARGUMENT
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("globals");

            Assert.False(taskResult.Success);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Set()
        {
            // Verify that we can set a valid non-user modifiable setting.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: globals
      neon_globals:
        state: set
        name: NOT-USER-MODIFIABLE-TEST
        value: HELLO-WORLD!
        validate: no
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("globals");

            Assert.True(taskResult.Success);

            // Verify the change.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: globals
      neon_globals:
        state: get
        name: NOT-USER-MODIFIABLE-TEST
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("globals");

            Assert.True(taskResult.Success);
            Assert.Contains("HELLO-WORLD!", taskResult.OutputText);
        }
    }
}
