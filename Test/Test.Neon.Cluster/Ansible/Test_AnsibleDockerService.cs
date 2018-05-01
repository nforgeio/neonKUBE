//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleDockerService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

using Xunit;
using Xunit.Neon;

namespace TestNeonCluster
{
    public class Test_AnsibleDockerService : IClassFixture<ClusterFixture>
    {
        private ClusterFixture cluster;

        public Test_AnsibleDockerService(ClusterFixture cluster)
        {
            this.cluster = cluster;

            // The test methods in this class depend on some fixed
            // assets like networks, secrets, configs and such and
            // then start, modify, and remove services.
            //
            // We're going to initialize these assets once and then
            // reset only the cluster services between test methods
            // for (hopefully) a bit better test execution performance.

            if (cluster.LoginAndInitialize(login: null))
            {
                cluster.CreateNetwork("test-1");
                cluster.CreateNetwork("test-2");
                cluster.CreateSecret("test-1", "test-1");
                cluster.CreateSecret("test-2", "test-2");
                cluster.CreateConfig("test-1", "test-1");
                cluster.CreateConfig("test-2", "test-2");
            }
            else
            {
                cluster.ClearServices();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void SimpleDeployAndRemove()
        {
            // Verify that we can deploy a basic service.

            var name     = "test";
            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {name}
        state: present
        image: neoncluster/test        
";
            var results    = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            // Verify that [state=absent] will remove the service.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {name}
        state: absent
        image: neoncluster/test
";
            results    = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Empty(cluster.ListServices().Where(s => s.Name == name));

            // Verify that removing the service again doesn't change anything
            // because it's already been removed.

            results    = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Empty(cluster.ListServices().Where(s => s.Name == name));
        }
    }
}
