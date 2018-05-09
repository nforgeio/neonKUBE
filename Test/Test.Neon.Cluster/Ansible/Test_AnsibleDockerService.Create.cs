//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleDockerService.Create.cs
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
        private ClusterFixture cluster;

        public Test_AnsibleDockerService(ClusterFixture cluster)
        {
            this.cluster = cluster;

            // The test methods in this class depend on some fixed
            // assets like networks, secrets, configs and such and
            // then start, modify, and remove services.
            //
            // We're going to initialize these assets once and then
            // only the cluster services between test methods for
            // (hopefully) a bit better test execution performance.

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
        public void Create_UserGroup()
        {
            // Verify that we can create a service customizing the container user and group.

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
        user: {TestHelper.TestUID}
        group:
          - {TestHelper.TestGID}
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);

            Assert.Equal(TestHelper.TestUID, details.Spec.TaskTemplate.ContainerSpec.User);
            Assert.Equal(new string[] { TestHelper.TestGID }, details.Spec.TaskTemplate.ContainerSpec.Groups);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Health()
        {
            // Verify that we can create a service that customizes
            // the health check related properties.

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
        health_cmd: echo OK
        health_interval: 1000000000ns
        health_retries: 3
        health_start_period: 1100000000ns
        health_timeout: 1200000000ns
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);

            Assert.Equal(new string[] { "CMD-SHELL", "echo OK" }, details.Spec.TaskTemplate.ContainerSpec.HealthCheck.Test);
            Assert.Equal(1000000000L, details.Spec.TaskTemplate.ContainerSpec.HealthCheck.Interval);
            Assert.Equal(3L, details.Spec.TaskTemplate.ContainerSpec.HealthCheck.Retries);
            Assert.Equal(1100000000L, details.Spec.TaskTemplate.ContainerSpec.HealthCheck.StartPeriod);
            Assert.Equal(1200000000L, details.Spec.TaskTemplate.ContainerSpec.HealthCheck.Timeout);

            // Redeploy the service disabling health checks.

            cluster.RemoveService(name);

            name = "test";
            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {name}
        state: present
        image: neoncluster/test
        no_health_check: yes
";
            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            details = cluster.InspectService(name);

            Assert.Equal(new string[] { "NONE" }, details.Spec.TaskTemplate.ContainerSpec.HealthCheck.Test);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Host()
        {
            // Verify that we can create a service that customizes
            // the container DNS [/etc/hosts] file.

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
        host: 
          - ""foo.com:1.1.1.1""
          - ""bar.com:2.2.2.2""
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);

            Assert.Equal(2, details.Spec.TaskTemplate.ContainerSpec.Hosts.Count());
            Assert.Contains("1.1.1.1 foo.com", details.Spec.TaskTemplate.ContainerSpec.Hosts);
            Assert.Contains("2.2.2.2 bar.com", details.Spec.TaskTemplate.ContainerSpec.Hosts);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_NoResolveImage()
        {
            // Verify that [no_resolve_image: true] doesn't barf.
            // This doesn't actually verify that the the setting
            // works but I tested that manually.

            cluster.ClearImages();

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
        no_resolve_image: yes
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Limits()
        {
            // Verify that we can create a service that customizes
            // the container resource limits.

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
        limit_cpu: 1.5
        limit_memory: 64m
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);

            Assert.Equal(1500000000L, details.Spec.TaskTemplate.Resources.Limits.NanoCPUs);
            Assert.Equal(67108864L, details.Spec.TaskTemplate.Resources.Limits.MemoryBytes);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Reservations()
        {
            // Verify that we can create a service that customizes
            // the container resource reservations.

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
        reserve_cpu: 1.5
        reserve_memory: 64m
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);

            Assert.Equal(1500000000L, details.Spec.TaskTemplate.Resources.Reservations.NanoCPUs);
            Assert.Equal(67108864L, details.Spec.TaskTemplate.Resources.Reservations.MemoryBytes);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Mount()
        {
            // Verify that we can create a service that mounts
            // various types of volumes.

            //-----------------------------------------------------------------
            // VOLUME mount:

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
        mount:
          - type: volume
            source: test-volume
            target: /mnt/volume
            readonly: no
            volume_label:
              - VOLUME=TEST
            volume_nocopy: yes
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);
            var mounts = details.Spec.TaskTemplate.ContainerSpec.Mounts;

            Assert.Single(mounts);

            var mount = mounts.First();

            Assert.Equal(ServiceMountType.Volume, mount.Type);
            Assert.False(mount.ReadOnly);
            Assert.Equal(ServiceMountConsistency.Default, mount.Consistency);
            Assert.Null(mount.BindOptions);
            Assert.Equal("test-volume", mount.Source);
            Assert.Equal("/mnt/volume", mount.Target);
            Assert.Single(mount.VolumeOptions.Labels);
            Assert.Equal("TEST", mount.VolumeOptions.Labels["VOLUME"]);
            Assert.True(mount.VolumeOptions.NoCopy);

            //-----------------------------------------------------------------
            // BIND mount:

            cluster.RemoveService("test");

            name = "test";
            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {name}
        state: present
        image: neoncluster/test
        mount:
          - type: bind
            source: /tmp
            target: /mnt/volume
            readonly: yes
            consistency: cached
            bind_propagation: slave
";
            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            details = cluster.InspectService(name);
            mounts = details.Spec.TaskTemplate.ContainerSpec.Mounts;

            Assert.Single(mounts);

            mount = mounts.First();
            Assert.Equal(ServiceMountType.Bind, mount.Type);
            Assert.True(mount.ReadOnly);

            // $todo(jeff.lill):
            //
            // Not sure why this test isn't working.  I can see the option being
            // specified correctly by CreateService() but [service inspect]
            // doesn't include the property in the [BindOptions].
            //
            // This isn't super important so I'm deferring further investigation.

            //Assert.Equal(ServiceMountConsistency.Cached, mount.Consistency);

            Assert.Equal(ServiceMountBindPropagation.Slave, mount.BindOptions.Propagation);
            Assert.Equal("/tmp", mount.Source);
            Assert.Equal("/mnt/volume", mount.Target);

            //-----------------------------------------------------------------
            // TMPFS mount:

            cluster.RemoveService("test");

            name = "test";
            playbook =
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
        mount:
          - type: tmpfs
            target: /mnt/tmpfs
            tmpfs_size: 64m
            tmpfs_mode: 770
";
            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            details = cluster.InspectService(name);
            mounts = details.Spec.TaskTemplate.ContainerSpec.Mounts;

            Assert.Single(mounts);

            mount = mounts.First();
            Assert.Equal(ServiceMountType.Tmpfs, mount.Type);
            Assert.False(mount.ReadOnly);
            Assert.Equal("/mnt/tmpfs", mount.Target);
            Assert.Equal(67108864L, mount.TmpfsOptions.SizeBytes);
            Assert.Equal(Convert.ToInt32("770", 8), mount.TmpfsOptions.Mode);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Publish()
        {
            // Verify that we can create a service with published network ports.

            //-----------------------------------------------------------------
            // Try with some defaults.

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
        publish:
          - published: 8080
            target: 80
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);

            Assert.Single(details.Spec.EndpointSpec.Ports);

            var port = details.Spec.EndpointSpec.Ports.First();

            Assert.Equal(8080, port.PublishedPort);
            Assert.Equal(80, port.TargetPort);
            Assert.Equal(ServicePortMode.Ingress, port.PublishMode);
            Assert.Equal(ServicePortProtocol.Tcp, port.Protocol);

            //-----------------------------------------------------------------
            // ...again, with explicit values.

            cluster.RemoveService("test");

            name = "test";
            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {name}
        state: present
        image: neoncluster/test
        publish:
          - published: 8080
            target: 80
            mode: ingress
            protocol: tcp
";
            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            details = cluster.InspectService(name);

            Assert.Single(details.Spec.EndpointSpec.Ports);

            port = details.Spec.EndpointSpec.Ports.First();

            Assert.Equal(8080, port.PublishedPort);
            Assert.Equal(80, port.TargetPort);
            Assert.Equal(ServicePortMode.Ingress, port.PublishMode);
            Assert.Equal(ServicePortProtocol.Tcp, port.Protocol);

            //-----------------------------------------------------------------
            // ...again, with non-default values.

            cluster.RemoveService("test");

            name = "test";
            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {name}
        state: present
        image: neoncluster/test
        publish:
          - published: 8080
            target: 80
            mode: host
            protocol: udp
";
            results = AnsiblePlayer.NeonPlay(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            details = cluster.InspectService(name);

            Assert.Single(details.Spec.EndpointSpec.Ports);

            port = details.Spec.EndpointSpec.Ports.First();

            Assert.Equal(8080, port.PublishedPort);
            Assert.Equal(80, port.TargetPort);
            Assert.Equal(ServicePortMode.Host, port.PublishMode);
            Assert.Equal(ServicePortProtocol.Udp, port.Protocol);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_ReadOnly()
        {
            // Verify that we can create a service with a read-only file system.

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
        read_only: 1
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);

            Assert.True(details.Spec.TaskTemplate.ContainerSpec.ReadOnly);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_RestartPolicy()
        {
            // Verify that we can create a service with a various
            // restart policy related settings.

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
        restart_condition: on-failure
        restart_delay: 2000ms
        restart_max_attempts: 5
        restart_window: 3000ms
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);
            var policy = details.Spec.TaskTemplate.RestartPolicy;

            Assert.Equal(ServiceRestartCondition.OnFailure, policy.Condition);
            Assert.Equal(2000000000, policy.Delay);
            Assert.Equal(5, policy.MaxAttempts);
            Assert.Equal(3000000000, policy.Window);
        }

        [Fact(Skip = "docker bug?")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_RollbackConfig()
        {
            // $todo(jeff.lill):
            //
            // This test is failing due to an apparent Docker bug:
            //
            //      https://github.com/moby/moby/issues/37027

            // Verify that we can create a service with a various
            // rollback configuration related settings.

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
        rollback_delay: 2
        rollback_failure_action: continue
        rollback_max_failure_ratio: 0.5
        rollback_monitor: 3000ms
        rollback_order: start-first
        rollback_parallism: 2
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);
            var config = details.Spec.RollbackConfig;

            Assert.Equal(2000000000, config.Delay);
            Assert.Equal(ServiceRollbackFailureAction.Continue, config.FailureAction);
            Assert.Equal(0.5, config.MaxFailureRatio);
            Assert.Equal(3000000000, config.Monitor);
            Assert.Equal(ServiceRollbackOrder.StartFirst, config.Order);
            Assert.Equal(2, config.Parallelism);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Network()
        {
            // Verify that services can reference networks.

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
        network:
          - network-1
          - network-2
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);
            var network = details.Spec.TaskTemplate.Networks;

            Assert.NotNull(network);
            Assert.Equal(2, network.Count);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Secret()
        {
            // Verify that we can references secrets.

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

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_StopGracePeriod()
        {
            // Verify that we can create a service customizing the stop grace period.

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
        stop_grace_period: 5s
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);

            Assert.Equal(5000000000L, details.Spec.TaskTemplate.ContainerSpec.StopGracePeriod);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_StopSignal()
        {
            // Verify that we can create a service customizing the stop signal.

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
        stop_signal: SIGTERM
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);

            Assert.Equal("SIGTERM", details.Spec.TaskTemplate.ContainerSpec.StopSignal);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_UpdateConfig()
        {
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
        update_delay: 2
        update_failure_action: continue
        update_max_failure_ratio: 0.5
        update_monitor: 3000ms
        update_order: start-first
        update_parallelism: 2
";
            var results = AnsiblePlayer.NeonPlay(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == name));

            var details = cluster.InspectService(name);
            var config = details.Spec.UpdateConfig;

            Assert.Equal(2000000000, config.Delay);
            Assert.Equal(ServiceUpdateFailureAction.Continue, config.FailureAction);
            Assert.Equal(0.5, config.MaxFailureRatio);
            Assert.Equal(3000000000, config.Monitor);
            Assert.Equal(ServiceUpdateOrder.StartFirst, config.Order);
            Assert.Equal(2, config.Parallelism);
        }
    }
}
