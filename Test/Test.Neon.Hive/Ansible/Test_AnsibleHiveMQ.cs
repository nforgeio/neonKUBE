//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleHiveMQ.cs
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
    public class Test_AnsibleHiveMQ : IClassFixture<HiveFixture>
    {
        private HiveFixture hive;

        public Test_AnsibleHiveMQ(HiveFixture fixture)
        {
            this.hive = fixture;

            // We're going to use unique DNS hosts for each test
            // so we only need to reset the test fixture once for
            // all tests implemented by this class.

            fixture.LoginAndInitialize(login: null);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void RabbitMQCtl()
        {
            // We're simply going to verify that we can execute thbis command: rabbitmqctl status

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage hivemq
      neon_hivemq:
        command:
          - rabbitmqctl
          - status
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage hivemq");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void RabbitMQAdmin()
        {
            // We're simply going to verify that we can execute this command: rabbitmqadmin --version

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage hivemq
      neon_hivemq:
        command:
          - rabbitmqadmin
          - --version
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage hivemq");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
        }
    }
}
