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
using Neon.Xunit;
using Neon.Xunit.Cluster;

using Xunit;

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
        public void Create_Remove()
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

            // Verify that [state=absent] removes the service.

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

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Replicas()
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
        replicas: 2
";
            var results    = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);

            Assert.Equal(2, details.Spec.Mode.Replicated.Replicas);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Update_ImageReplicas()
        {
            AnsiblePlayer.WorkDir = @"c:\temp\ansible";     // $todo(jeff.lill): DELETE THIS!

            // Verify that we can deploy a basic service and then update
            // the image and number of replicas.

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
        image: neoncluster/test:0
";
            var results    = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);

            Assert.Equal(name, details.Spec.Name);
            Assert.Equal("neoncluster/test:0", details.Spec.TaskTemplate.ContainerSpec.ImageWithoutSHA);
            Assert.Equal(1, details.Spec.Mode.Replicated.Replicas);

            // Verify that we can update the service.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {name}
        state: present
        image: neoncluster/test:1
        replicas: 2
";
            results    = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            details = cluster.InspectService(name);

            Assert.Equal(name, details.Spec.Name);
            Assert.Equal("neoncluster/test:1", details.Spec.TaskTemplate.ContainerSpec.ImageWithoutSHA);
            Assert.Equal(2, details.Spec.Mode.Replicated.Replicas);
        }
    }
}
