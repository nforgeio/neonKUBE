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
using Neon.Xunit.Hive;

using Xunit;

// $todo(jeff.lill):
//
// We could add tests to verify that CHECK-MODE doesn't
// actually make any changes.

namespace TestHive
{
    public partial class Test_AnsibleDockerService : IClassFixture<HiveFixture>
    {
        private const string serviceName  = "test";
        private const string serviceImage = "nhive/test:0";

        private HiveFixture hive;

        public Test_AnsibleDockerService(HiveFixture fixture)
        {
            this.hive = fixture;

            // The test methods in this class depend on some fixed
            // assets like networks, secrets, configs and such and
            // then start, modify, and remove services.
            //
            // We're going to initialize these assets once and then
            // only the hive services between test methods for
            // (hopefully) a bit better test execution performance.

            if (fixture.LoginAndInitialize(login: null))
            {
                fixture.CreateNetwork("network-1");
                fixture.CreateNetwork("network-2");
                fixture.CreateSecret("secret-1", "test-1");
                fixture.CreateSecret("secret-2", "test-2");
                fixture.CreateConfig("config-1", "test-1");
                fixture.CreateConfig("config-2", "test-2");
            }
            else
            {
                fixture.ClearServices();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Replicas()
        {
            // Verify that we can deploy more than one replica.

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
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);

            Assert.Equal(2, details.Spec.Mode.Replicated.Replicas);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Args()
        {
            // Verify that we can specify service arguments.

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
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);

            Assert.Equal(new string[] { "one", "two" }, details.Spec.TaskTemplate.ContainerSpec.Args);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Config()
        {
            // Verify that we can add Docker configs.

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
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);
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
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);
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
        container_label:
          - foo=bar
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);
            var labels = details.Spec.TaskTemplate.ContainerSpec.Labels;

            Assert.Single(labels);
            Assert.Equal("bar", labels["foo"]);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Dns()
        {
            // Verify that we can manage container DNS settings.

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
        dns:
          - 8.8.8.8
          - 8.8.4.4
        dns_option:
          - timeout:2
        dns_search:
          - foo.com
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);
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
        endpoint_mode: dnsrr
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);

            Assert.Equal(ServiceEndpointMode.DnsRR, details.Spec.EndpointSpec.Mode);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Entrypoint()
        {
            // Verify that we can override the service image entrypoint.

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
        entrypoint:
          - sleep
          - 7777777
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);

            Assert.Equal(new string[] { "sleep", "7777777" }, details.Spec.TaskTemplate.ContainerSpec.Command);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Env()
        {
            // Verify that we can add environment variables.

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
        env:
          - FOO=BAR
          - SUDO_USER
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);
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
        user: {TestHelper.TestUID}
        group:
          - {TestHelper.TestGID}
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);

            Assert.Equal(TestHelper.TestUID, details.Spec.TaskTemplate.ContainerSpec.User);
            Assert.Equal(new string[] { TestHelper.TestGID }, details.Spec.TaskTemplate.ContainerSpec.Groups);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Health()
        {
            // Verify that we can create a service that customizes
            // the health check related properties.

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
        health_cmd: echo OK
        health_interval: 1000000000ns
        health_retries: 3
        health_start_period: 1100000000ns
        health_timeout: 1200000000ns
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);

            Assert.Equal(new string[] { "CMD-SHELL", "echo OK" }, details.Spec.TaskTemplate.ContainerSpec.HealthCheck.Test);
            Assert.Equal(1000000000L, details.Spec.TaskTemplate.ContainerSpec.HealthCheck.Interval);
            Assert.Equal(3L, details.Spec.TaskTemplate.ContainerSpec.HealthCheck.Retries);
            Assert.Equal(1100000000L, details.Spec.TaskTemplate.ContainerSpec.HealthCheck.StartPeriod);
            Assert.Equal(1200000000L, details.Spec.TaskTemplate.ContainerSpec.HealthCheck.Timeout);

            // Redeploy the service disabling health checks.

            hive.RemoveService(serviceName);

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
        no_healthcheck: yes
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            details = hive.InspectService(serviceName);

            Assert.Equal(new string[] { "NONE" }, details.Spec.TaskTemplate.ContainerSpec.HealthCheck.Test);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Host()
        {
            // Verify that we can create a service that customizes
            // the container DNS [/etc/hosts] file.

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
        host: 
          - ""foo.com:1.1.1.1""
          - ""bar.com:2.2.2.2""
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);
            var hosts = details.Spec.TaskTemplate.ContainerSpec.Hosts;

            Assert.Equal(2, hosts.Count);
            Assert.Contains("1.1.1.1 foo.com", hosts);
            Assert.Contains("2.2.2.2 bar.com", hosts);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_NoResolveImage()
        {
            // Verify that [no_resolve_image: true] doesn't barf.
            // This doesn't actually verify that the setting works
            // but I tested that manually.

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
        no_resolve_image: yes
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);    // This always reports as TRUE
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Label()
        {
            // Verify that we can add service labels.

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
        label:
          - foo=bar
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);
            var labels = details.Spec.Labels;

            Assert.Single(labels);
            Assert.Equal("bar", labels["foo"]);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Limits()
        {
            // Verify that we can create a service that customizes
            // the container resource limits.

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
        limit_cpu: 1.5
        limit_memory: 64m
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);
            var limits = details.Spec.TaskTemplate.Resources.Limits;

            Assert.Equal(1500000000L, limits.NanoCPUs);
            Assert.Equal(67108864L, limits.MemoryBytes);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Reservations()
        {
            // Verify that we can create a service that customizes
            // the container resource reservations.

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
        reserve_cpu: 1.5
        reserve_memory: 64m
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);
            var reservations = details.Spec.TaskTemplate.Resources.Reservations;

            Assert.Equal(1500000000L, reservations.NanoCPUs);
            Assert.Equal(67108864L, reservations.MemoryBytes);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Mount()
        {
            // Verify that we can create a service that mounts
            // various types of volumes.

            //-----------------------------------------------------------------
            // VOLUME mount:

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
        mount:
          - type: volume
            source: test-volume
            target: /mnt/volume
            readonly: no
            volume_label:
              - VOLUME=TEST
            volume_nocopy: yes
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);
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

            hive.RemoveService("test");

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
        mount:
          - type: bind
            source: /tmp
            target: /mnt/volume
            readonly: yes
            consistency: cached
            bind_propagation: slave
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            details = hive.InspectService(serviceName);
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

            hive.RemoveService("test");

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
        mount:
          - type: tmpfs
            target: /mnt/volume
            tmpfs_size: 64m
            tmpfs_mode: 770
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            details = hive.InspectService(serviceName);
            mounts = details.Spec.TaskTemplate.ContainerSpec.Mounts;

            Assert.Single(mounts);

            mount = mounts.First();
            Assert.Equal(ServiceMountType.Tmpfs, mount.Type);
            Assert.False(mount.ReadOnly);
            Assert.Equal("/mnt/volume", mount.Target);
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
        publish:
          - published: 8080
            target: 80
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);
            var ports = details.Spec.EndpointSpec.Ports;

            Assert.Single(ports);

            var port = ports.First();

            Assert.Equal(8080, port.PublishedPort);
            Assert.Equal(80, port.TargetPort);
            Assert.Equal(ServicePortMode.Ingress, port.PublishMode);
            Assert.Equal(ServicePortProtocol.Tcp, port.Protocol);

            //-----------------------------------------------------------------
            // ...again, with explicit values.

            hive.RemoveService("test");

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
        publish:
          - published: 8080
            target: 80
            mode: ingress
            protocol: tcp
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            details = hive.InspectService(serviceName);
            ports = details.Spec.EndpointSpec.Ports;

            Assert.Single(ports);

            port = ports.First();

            Assert.Equal(8080, port.PublishedPort);
            Assert.Equal(80, port.TargetPort);
            Assert.Equal(ServicePortMode.Ingress, port.PublishMode);
            Assert.Equal(ServicePortProtocol.Tcp, port.Protocol);

            //-----------------------------------------------------------------
            // ...again, with non-default values.

            hive.RemoveService("test");

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
        publish:
          - published: 8080
            target: 80
            mode: host
            protocol: udp
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            details = hive.InspectService(serviceName);
            ports = details.Spec.EndpointSpec.Ports;

            Assert.Single(ports);

            port = ports.First();

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
        read_only: 1
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);

            Assert.True(details.Spec.TaskTemplate.ContainerSpec.ReadOnly);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_RestartPolicy()
        {
            // Verify that we can create a service with a various
            // restart policy related settings.

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
        restart_condition: on-failure
        restart_delay: 2000ms
        restart_max_attempts: 5
        restart_window: 3000ms
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);
            var policy = details.Spec.TaskTemplate.RestartPolicy;

            Assert.Equal(ServiceRestartCondition.OnFailure, policy.Condition);
            Assert.Equal(2000000000, policy.Delay);
            Assert.Equal(5, policy.MaxAttempts);
            Assert.Equal(3000000000, policy.Window);
        }

        [Fact(Skip = "DOCKER BUG: https://github.com/moby/moby/issues/37027")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_RollbackConfig()
        {
            // $todo(jeff.lill):
            //
            // This test is failing due to a Docker bug:
            //
            //      https://github.com/moby/moby/issues/37027

            // Verify that we can create a service with a various
            // rollback configuration related settings.

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
        rollback_delay: 2
        rollback_failure_action: continue
        rollback_max_failure_ratio: 0.5
        rollback_monitor: 3000ms
        rollback_order: start-first
        rollback_parallism: 2
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);
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
        network:
          - network-1
          - network-2
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);
            var networks = details.Spec.TaskTemplate.Networks;

            Assert.NotNull(networks);
            Assert.Equal(2, networks.Count);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Secret()
        {
            // Verify that we can references secrets.

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
            mode: 444
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);
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
        stop_grace_period: 5s
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);

            Assert.Equal(5000000000L, details.Spec.TaskTemplate.ContainerSpec.StopGracePeriod);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_StopSignal()
        {
            // Verify that we can create a service customizing the stop signal.

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
        stop_signal: SIGTERM
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);

            Assert.Equal("SIGTERM", details.Spec.TaskTemplate.ContainerSpec.StopSignal);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_UpdateConfig()
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
        update_delay: 2
        update_failure_action: continue
        update_max_failure_ratio: 0.5
        update_monitor: 3000ms
        update_order: start-first
        update_parallelism: 2
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(hive.ListServices().Where(s => s.Name == serviceName));

            var details = hive.InspectService(serviceName);
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
