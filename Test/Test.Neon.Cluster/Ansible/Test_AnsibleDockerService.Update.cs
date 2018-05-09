//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleDockerService.Update.cs
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
using Neon.Xunit.Cluster;

using Xunit;

namespace TestNeonCluster
{
    public partial class Test_AnsibleDockerService : IClassFixture<ClusterFixture>
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Update_ImageReplicas()
        {
            AnsiblePlayer.WorkDir = @"c:\temp\ansible";     // $todo(jeff.lill): DELETE THIS!

            // Verify that we can deploy a basic service and then update
            // the image and number of replicas.

            var name = "test";
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
            var results = AnsiblePlayer.NeonPlay(playbook);
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
            results = AnsiblePlayer.NeonPlay(playbook);
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
