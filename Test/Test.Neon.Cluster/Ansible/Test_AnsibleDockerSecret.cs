//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleDockerSecret.cs
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
    public class Test_AnsibleDockerSecret : IClassFixture<ClusterFixture>
    {
        private ClusterFixture cluster;

        public Test_AnsibleDockerSecret(ClusterFixture cluster)
        {
            this.cluster = cluster;

            // We're going to use unique secret names for each test
            // so we only need to reset the test fixture once for
            // all tests implemented by this class.

            cluster.LoginAndInitialize(login: null);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void CheckArgs()
        {
            var name     = "secret-" + Guid.NewGuid().ToString("D");
            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: create secret
      neon_docker_secret:
        name: {name}
        state: present
        text: password
        UNKNOWN: argument
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("create secret");

            Assert.False(taskResult.Success);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void CreateText()
        {
            var name     = "secret-" + Guid.NewGuid().ToString("D");
            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: create secret
      neon_docker_secret:
        name: {name}
        state: present
        text: password
";
            // Create a new secret.

            var results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("create secret");

            Assert.NotNull(taskResult);
            Assert.Equal("create secret", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListSecrets().Where(s => s.Name == name));

            // Run the playbook again but this time nothing should
            // be changed because the secret already exists.

            results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("create secret");

            Assert.NotNull(taskResult);
            Assert.Equal("create secret", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Single(cluster.ListSecrets().Where(s => s.Name == name));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void CreateBytes()
        {
            var name     = "secret-" + Guid.NewGuid().ToString("D");
            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: create secret
      neon_docker_secret:
        name: {name}
        state: present
        bytes: cGFzc3dvcmQ=
";
            // Create a new secret.

            var results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("create secret");

            Assert.NotNull(taskResult);
            Assert.Equal("create secret", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListSecrets().Where(s => s.Name == name));

            // Run the playbook again but this time nothing should
            // be changed because the secret already exists.

            results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("create secret");

            Assert.NotNull(taskResult);
            Assert.Equal("create secret", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Single(cluster.ListSecrets().Where(s => s.Name == name));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Remove()
        {
            var name = "secret-" + Guid.NewGuid().ToString("D");
            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: create secret
      neon_docker_secret:
        name: {name}
        state: present
        bytes: cGFzc3dvcmQ=
";
            // Create a new secret.

            var results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("create secret");

            Assert.NotNull(taskResult);
            Assert.Equal("create secret", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListSecrets().Where(s => s.Name == name));

            // Now remove it.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: remove secret
      neon_docker_secret:
        name: {name}
        state: absent
        bytes: cGFzc3dvcmQ=
";
            results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("remove secret");

            Assert.NotNull(taskResult);
            Assert.Equal("remove secret", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Empty(cluster.ListSecrets().Where(s => s.Name == name));

            // Remove it again to verify that nothing changes.

            results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("remove secret");

            Assert.NotNull(taskResult);
            Assert.Equal("remove secret", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Empty(cluster.ListSecrets().Where(s => s.Name == name));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void ErrorNoName()
        {
            var name = "secret-" + Guid.NewGuid().ToString("D");
            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: create secret
      neon_docker_secret:
        name: {name}
        state: present
";
            var results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("create secret");

            Assert.NotNull(taskResult);
            Assert.Equal("create secret", taskResult.TaskName);
            Assert.False(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Empty(cluster.ListSecrets().Where(s => s.Name == name));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void ErrorNoValue()
        {
            var name = "secret-" + Guid.NewGuid().ToString("D");
            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: create secret
      neon_docker_secret:
        state: present
        bytes: cGFzc3dvcmQ=
";
            var results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("create secret");

            Assert.NotNull(taskResult);
            Assert.Equal("create secret", taskResult.TaskName);
            Assert.False(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Empty(cluster.ListSecrets().Where(s => s.Name == name));
        }
    }
}
