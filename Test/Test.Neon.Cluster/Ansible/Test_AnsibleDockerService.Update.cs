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
        /// <summary>
        /// Deploys <b>neoncluster/test:0</b> with default options.
        /// </summary>
        private void DeployTestService()
        {
            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: present
        image: {serviceImage}
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == serviceName));

            var details = cluster.InspectService(serviceName);

            Assert.Equal(serviceName, details.Spec.Name);
            Assert.Equal(serviceImage, details.Spec.TaskTemplate.ContainerSpec.ImageWithoutSHA);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Update_Image()
        {
            DeployTestService();

            // Verify that we can update a service image.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: present
        image: neoncluster/test:1
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == serviceName));

            var details = cluster.InspectService(serviceName);

            Assert.Equal(serviceName, details.Spec.Name);
            Assert.Equal("neoncluster/test:1", details.Spec.TaskTemplate.ContainerSpec.ImageWithoutSHA);

            //-----------------------------------------------------------------
            // Verify that update reports when no change is detected.

            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Update_Replicas()
        {
            DeployTestService();

            // Verify that we can update the replicas.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: present
        image: {serviceImage}
        replicas: 2
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == serviceName));

            var details = cluster.InspectService(serviceName);

            Assert.Equal(2, details.Spec.Mode.Replicated.Replicas);

            //-----------------------------------------------------------------
            // Verify that update reports when no change is detected.

            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Update_Args()
        {
            DeployTestService();

            //-----------------------------------------------------------------
            // Verify that we can update service arguments.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: present
        image: {serviceImage}
        args:
          - one
          - two
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == serviceName));

            var details = cluster.InspectService(serviceName);

            Assert.Equal(new string[] { "one", "two" }, details.Spec.TaskTemplate.ContainerSpec.Args);

            //-----------------------------------------------------------------
            // Verify that update reports when no change is detected.

            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Update_Config()
        {
            DeployTestService();

            //-----------------------------------------------------------------
            // Verify that we can add a config.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: present
        image: {serviceImage}
        config:
          - source: config-1
            target: config
            uid: {TestHelper.TestUID}
            gid: {TestHelper.TestGID}
            mode: 440
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == serviceName));

            var details = cluster.InspectService(serviceName);
            var config = details.Spec.TaskTemplate.ContainerSpec.Configs.FirstOrDefault();

            Assert.NotNull(config);
            Assert.Equal("config-1", config.ConfigName);
            Assert.Equal("config", config.File.Name);
            Assert.Equal(TestHelper.TestUID, config.File.UID);
            Assert.Equal(TestHelper.TestGID, config.File.GID);
            Assert.Equal(Convert.ToInt32("440", 8), config.File.Mode);

            //-----------------------------------------------------------------
            // Verify that update reports when no change is detected.

            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            details = cluster.InspectService(serviceName);
            config = details.Spec.TaskTemplate.ContainerSpec.Configs.FirstOrDefault();

            Assert.NotNull(config);
            Assert.Equal("config-1", config.ConfigName);
            Assert.Equal("config", config.File.Name);
            Assert.Equal(TestHelper.TestUID, config.File.UID);
            Assert.Equal(TestHelper.TestGID, config.File.GID);
            Assert.Equal(Convert.ToInt32("440", 8), config.File.Mode);

            //-----------------------------------------------------------------
            // Verify that we can remove a config.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: present
        image: {serviceImage}
";
            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            details = cluster.InspectService(serviceName);

            Assert.Empty(details.Spec.TaskTemplate.ContainerSpec.Configs);

            //-----------------------------------------------------------------
            // Verify that update reports when no change is detected.

            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Update_Constraint()
        {
            DeployTestService();

            //-----------------------------------------------------------------
            // Verify that we can add placement constraints.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: present
        image: {serviceImage}
        constraint:
          - node.role==manager
          - node.role!=worker
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == serviceName));

            var details = cluster.InspectService(serviceName);
            var constraints = details.Spec.TaskTemplate.Placement.Constraints;

            Assert.NotNull(constraints);
            Assert.Equal(2, constraints.Count);
            Assert.Contains("node.role==manager", constraints);
            Assert.Contains("node.role!=worker", constraints);

            //-----------------------------------------------------------------
            // Verify that update reports when no change is detected.

            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            //-----------------------------------------------------------------
            // Verify that we can remove placement constraints.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: present
        image: {serviceImage}
        constraint:
          - node.role==manager
";
            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == serviceName));

            details = cluster.InspectService(serviceName);
            constraints = details.Spec.TaskTemplate.Placement.Constraints;

            Assert.NotNull(constraints);
            Assert.Single(constraints);
            Assert.Contains("node.role==manager", constraints);

            //-----------------------------------------------------------------
            // Verify that update reports when no change is detected.

            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Update_Secret()
        {
            DeployTestService();

            //-----------------------------------------------------------------
            // Verify that we can add a secret.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: present
        image: {serviceImage}
        secret:
          - source: secret-1
            target: secret
            uid: {TestHelper.TestUID}
            gid: {TestHelper.TestGID}
            mode: 440
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == serviceName));

            var details = cluster.InspectService(serviceName);
            var secret = details.Spec.TaskTemplate.ContainerSpec.Secrets.FirstOrDefault();

            Assert.NotNull(secret);
            Assert.Equal("secret-1", secret.SecretName);
            Assert.Equal("secret", secret.File.Name);
            Assert.Equal(TestHelper.TestUID, secret.File.UID);
            Assert.Equal(TestHelper.TestGID, secret.File.GID);
            Assert.Equal(Convert.ToInt32("440", 8), secret.File.Mode);

            //-----------------------------------------------------------------
            // Verify that update reports when no change is detected.

            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            details = cluster.InspectService(serviceName);
            secret = details.Spec.TaskTemplate.ContainerSpec.Secrets.FirstOrDefault();

            Assert.NotNull(secret);
            Assert.Equal("secret-1", secret.SecretName);
            Assert.Equal("secret", secret.File.Name);
            Assert.Equal(TestHelper.TestUID, secret.File.UID);
            Assert.Equal(TestHelper.TestGID, secret.File.GID);
            Assert.Equal(Convert.ToInt32("440", 8), secret.File.Mode);

            //-----------------------------------------------------------------
            // Verify that we can remove a secret.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: present
        image: {serviceImage}
";
            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            details = cluster.InspectService(serviceName);

            Assert.Empty(details.Spec.TaskTemplate.ContainerSpec.Secrets);

            //-----------------------------------------------------------------
            // Verify that update reports when no change is detected.

            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
        }
    }
}
