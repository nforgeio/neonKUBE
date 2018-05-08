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
using Neon.Docker;
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
                cluster.CreateNetwork("network-1");
                cluster.CreateNetwork("network-2");
                cluster.CreateSecret("secret-1", "test-1");
                cluster.CreateSecret("secret-2", "test-2");
                cluster.CreateConfig("config-1", "test-1");
                cluster.CreateConfig("config-2", "test-2");
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
        image: neoncluster/test        
";
            var results = AnsiblePlayer.NeonPlay(playbook);
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
            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Empty(cluster.ListServices().Where(s => s.Name == name));

            // Verify that removing the service again doesn't change anything
            // because it's already been removed.

            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Empty(cluster.ListServices().Where(s => s.Name == name));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Replicas()
        {
            // Verify that we can deploy more than one replica.

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
        image: neoncluster/test
        replicas: 2
";
            var results = AnsiblePlayer.NeonPlay(playbook);
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

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Args()
        {
            // Verify that we can specify service arguments.

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
        image: neoncluster/test
        args:
          - one
          - two
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);

            Assert.Equal(new string[] { "one", "two" }, details.Spec.TaskTemplate.ContainerSpec.Args);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Config()
        {
            // Verify that we can add Docker configs.

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
        image: neoncluster/test
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
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);
            var config = details.Spec.TaskTemplate.ContainerSpec.Configs.FirstOrDefault();

            Assert.NotNull(config);
            Assert.Equal("config-1", config.ConfigName);
            Assert.Equal("config", config.File.Name);
            Assert.Equal(TestHelper.TestUID, config.File.UID);
            Assert.Equal(TestHelper.TestGID, config.File.GID);
            Assert.Equal(Convert.ToInt32("440", 8), config.File.Mode);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Constraint()
        {
            // Verify that we can add placement constraints.

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
        image: neoncluster/test
        constraint:
          - node.role==manager
          - node.role!=worker
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);
            var constraints = details.Spec.TaskTemplate.Placement.Constraints;

            Assert.NotNull(constraints);
            Assert.Equal(2, constraints.Count);
            Assert.Contains("node.role==manager", constraints);
            Assert.Contains("node.role!=worker", constraints);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_ContainerLabel()
        {
            // Verify that we can add container labels.

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
        image: neoncluster/test
        container_label:
          - foo=bar
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);
            var labels = details.Spec.TaskTemplate.ContainerSpec.Labels;

            Assert.Single(labels);
            Assert.Equal("bar", labels["foo"]);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Dns()
        {
            // Verify that we can manage container DNS settings.

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
        image: neoncluster/test
        dns:
          - 8.8.8.8
          - 8.8.4.4
        dns_option:
          - timeout:2
        dns_search:
          - foo.com
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);
            var dnsConfig = details.Spec.TaskTemplate.ContainerSpec.DNSConfig;

            Assert.Equal(2, dnsConfig.Nameservers.Count);
            Assert.Contains("8.8.8.8", dnsConfig.Nameservers);
            Assert.Contains("8.8.4.4", dnsConfig.Nameservers);

            Assert.Equal(new string[] { "timeout:2" }, dnsConfig.Options);
            Assert.Equal(new string[] { "foo.com" }, dnsConfig.Search);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_EndpointMode()
        {
            // Verify that we can configure the service endpoint mode.

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
        image: neoncluster/test
        endpoint_mode: dnsrr
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);

            Assert.Equal(ServiceEndpointMode.DnsRR, details.Spec.EndpointSpec.Mode);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Entrypoint()
        {
            // Verify that we can override the service image entrypoint.

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
        image: neoncluster/test
        entrypoint:
          - sleep
          - 7777777
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);

            Assert.Equal(new string[] { "sleep", "7777777" }, details.Spec.TaskTemplate.ContainerSpec.Command);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Env()
        {
            // Verify that we can add environment variables.

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
        image: neoncluster/test
        env:
          - FOO=BAR
          - SUDO_USER
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);
            var env     = details.Spec.TaskTemplate.ContainerSpec.Env;

            Assert.Equal(2, env.Count);
            Assert.Contains("FOO=BAR", env);
            Assert.Contains("SUDO_USER=sysadmin", env);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Secret()
        {
            // Verify that we can add Docker secrets.

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
        image: neoncluster/test
        secret:
          - source: secret-1
            target: secret
            uid: {TestHelper.TestUID}
            gid: {TestHelper.TestGID}
            mode: 444
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);
            var secret = details.Spec.TaskTemplate.ContainerSpec.Secrets.FirstOrDefault();

            Assert.NotNull(secret);
            Assert.Equal("secret-1", secret.SecretName);
            Assert.Equal("secret", secret.File.Name);
            Assert.Equal(TestHelper.TestUID, secret.File.UID);
            Assert.Equal(TestHelper.TestGID, secret.File.GID);
            Assert.Equal(Convert.ToInt32("444", 8), secret.File.Mode);
        }
    }
}
