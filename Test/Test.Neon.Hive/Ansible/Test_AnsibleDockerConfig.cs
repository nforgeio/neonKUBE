//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleDockerConfig.cs
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
using Neon.Xunit.Hive;

using Xunit;

namespace TestHive
{
    public class Test_AnsibleDockerConfig : IClassFixture<HiveFixture>
    {
        private HiveFixture hive;

        public Test_AnsibleDockerConfig(HiveFixture fixture)
        {
            this.hive = fixture;

            // We're going to use unique config names for each test
            // so we only need to reset the test fixture once for
            // all tests implemented by this class.

            fixture.LoginAndInitialize();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void CheckArgs()
        {
            var name     = "config-" + Guid.NewGuid().ToString("D");
            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: create config
      neon_docker_config:
        name: {name}
        state: present
        text: password
        UNKNOWN: argument
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("create config");

            Assert.False(taskResult.Success);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void CreateText()
        {
            var name     = "config-" + Guid.NewGuid().ToString("D");
            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: create config
      neon_docker_config:
        name: {name}
        state: present
        text: password
";
            // Create a new config.

            var results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("create config");

            Assert.NotNull(taskResult);
            Assert.Equal("create config", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListConfigs().Where(s => s.Name == name));

            // Run the playbook again but this time nothing should
            // be changed because the config already exists.

            results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("create config");

            Assert.NotNull(taskResult);
            Assert.Equal("create config", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Single(hive.ListConfigs().Where(s => s.Name == name));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void CreateBytes()
        {
            var name     = "config-" + Guid.NewGuid().ToString("D");
            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: create config
      neon_docker_config:
        name: {name}
        state: present
        bytes: cGFzc3dvcmQ=
";
            // Create a new config.

            var results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("create config");

            Assert.NotNull(taskResult);
            Assert.Equal("create config", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListConfigs().Where(s => s.Name == name));

            // Run the playbook again but this time nothing should
            // be changed because the config already exists.

            results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("create config");

            Assert.NotNull(taskResult);
            Assert.Equal("create config", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Single(hive.ListConfigs().Where(s => s.Name == name));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Remove()
        {
            var name = "config-" + Guid.NewGuid().ToString("D");
            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: create config
      neon_docker_config:
        name: {name}
        state: present
        bytes: cGFzc3dvcmQ=
";
            // Create a new config.

            var results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("create config");

            Assert.NotNull(taskResult);
            Assert.Equal("create config", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListConfigs().Where(s => s.Name == name));

            // Now remove it.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: remove config
      neon_docker_config:
        name: {name}
        state: absent
        bytes: cGFzc3dvcmQ=
";
            results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("remove config");

            Assert.NotNull(taskResult);
            Assert.Equal("remove config", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Empty(hive.ListConfigs().Where(s => s.Name == name));

            // Remove it again to verify that nothing changes.

            results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("remove config");

            Assert.NotNull(taskResult);
            Assert.Equal("remove config", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Empty(hive.ListConfigs().Where(s => s.Name == name));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void ErrorNoName()
        {
            var name = "config-" + Guid.NewGuid().ToString("D");
            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: create config
      neon_docker_config:
        name: {name}
        state: present
";
            var results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("create config");

            Assert.NotNull(taskResult);
            Assert.Equal("create config", taskResult.TaskName);
            Assert.False(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Empty(hive.ListConfigs().Where(s => s.Name == name));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void ErrorNoValue()
        {
            var name = "config-" + Guid.NewGuid().ToString("D");
            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: create config
      neon_docker_config:
        state: present
        bytes: cGFzc3dvcmQ=
";
            var results = AnsiblePlayer.PlayNoGather(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("create config");

            Assert.NotNull(taskResult);
            Assert.Equal("create config", taskResult.TaskName);
            Assert.False(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Empty(hive.ListConfigs().Where(s => s.Name == name));
        }
    }
}
