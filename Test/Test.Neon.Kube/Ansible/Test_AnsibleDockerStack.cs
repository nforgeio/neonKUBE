//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleDockerStack.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Docker;
using Neon.Xunit;
using Neon.Xunit.Hive;

using Xunit;

// $todo(jeff.lill):
//
// We could add tests to verify that CHECK-MODE doesn't
// actually make any changes.

namespace TestHive
{
    public class Test_AnsibleDockerStack : IClassFixture<HiveFixture>
    {
        private HiveFixture hive;

        public Test_AnsibleDockerStack(HiveFixture fixture)
        {
            this.hive = fixture;

            if (!fixture.LoginAndInitialize(login: null))
            {
                fixture.ClearStacks();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void DeployAndRemove()
        {
            // Deploy a stack via the [neon_docker_stack] module.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: stack deploy
      neon_docker_stack:
        name: test-stack
        state: deploy
        stack:
          version: ""3""
          services:
            test:
              image: nhive/test:0
              deploy:
                replicas: 1
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("stack deploy");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListStacks().Where(s => s.Name == "test-stack"));

            // Now remove the stack.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: stack remove
      neon_docker_stack:
        name: test-stack
        state: remove
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("stack remove");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Empty(hive.ListStacks().Where(s => s.Name == "test-stack"));
        }
    }
}
