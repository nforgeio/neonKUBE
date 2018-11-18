//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleDockerRegistry.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Consul;

using Couchbase;
using Couchbase.Core;
using Couchbase.Linq;
using Couchbase.Linq.Extensions;
using Couchbase.N1QL;
using Newtonsoft.Json.Linq;

using Neon.Data;
using Neon.Common;
using Neon.Hive;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Couchbase;
using Neon.Xunit.Hive;

using Xunit;

namespace TestHive
{
    public class Test_AnsibleDockerRegistry : IClassFixture<HiveFixture>
    {
        private HiveFixture     hiveFixture;
        private HiveProxy       hive;

        public Test_AnsibleDockerRegistry(HiveFixture fixture)
        {
            fixture.LoginAndInitialize();

            this.hiveFixture = fixture;
            this.hive        = fixture.Hive;

            // Ensure that tests start without a local registry
            // and related assets.

            var manager = this.hive.GetReachableManager();

            if (this.hive.Docker.InspectService("neon-registry") != null)
            {
                manager.DockerCommand(RunOptions.None, "docker service rm neon-registry");
            }

            hiveFixture.ClearVolumes();

            this.hive.Certificate.Remove("neon-registry");
            this.hive.PublicTraffic.RemoveRule("neon-registry");
            this.hive.Dns.Remove("xunit-registry.neonforge.net");
            this.hive.Dns.Remove("xunit-registry2.neonforge.net");
            this.hive.Registry.Logout("xunit-registry.neonforge.net");
            this.hive.Registry.Logout("xunit-registry2.neonforge.net");
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void DeployAndRemove()
        {
            // We're going to create a temporary Ansible working folder and copy 
            // the test secrets file there so we can reference it from the playbooks.

            using (var folder = new TempFolder())
            {
                File.Copy(TestHelper.AnsibleSecretsPath, Path.Combine(folder.Path, "secrets.yaml"));

                //-------------------------------------------------------------
                // Verify that we can deploy a local Docker registry.

                var playbook =
@"
- name: test
  hosts: localhost
  vars_files:
    - secrets.yaml
  tasks:
    - name: registry
      neon_docker_registry:
        state: present
        hostname: xunit-registry.neonforge.net
        certificate: ""{{ _neonforge_net_pem }}""
        username: test
        password: password
        secret: secret
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                var taskResult = results.GetTaskResult("registry");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                Assert.Single(hiveFixture.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry.neonforge.net"));
                Assert.Single(hiveFixture.ListTrafficManagers("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                var registryCredentials = hive.Registry.GetCredentials("xunit-registry.neonforge.net");

                Assert.NotNull(registryCredentials);
                Assert.Equal("xunit-registry.neonforge.net", registryCredentials.Registry);
                Assert.Equal("test", registryCredentials.Username);
                Assert.Equal("password", registryCredentials.Password);

                Assert.Equal("xunit-registry.neonforge.net", hive.Registry.GetLocalHostname());
                Assert.Equal("secret", hive.Registry.GetLocalSecret());

                foreach (var manager in hive.Managers)
                {
                    Assert.Single(hiveFixture.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                var dnsEntry = hive.Dns.Get("xunit-registry.neonforge.net");

                Assert.NotNull(dnsEntry);
                Assert.True(dnsEntry.IsSystem);

                //-------------------------------------------------------------
                // Run the playbook again and verify that nothing changed.

                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("registry");

                Assert.True(taskResult.Success);
                Assert.False(taskResult.Changed);

                Assert.Single(hiveFixture.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry.neonforge.net"));
                Assert.Single(hiveFixture.ListTrafficManagers("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                registryCredentials = hive.Registry.GetCredentials("xunit-registry.neonforge.net");

                Assert.NotNull(registryCredentials);
                Assert.Equal("xunit-registry.neonforge.net", registryCredentials.Registry);
                Assert.Equal("test", registryCredentials.Username);
                Assert.Equal("password", registryCredentials.Password);

                Assert.Equal("xunit-registry.neonforge.net", hive.Registry.GetLocalHostname());
                Assert.Equal("secret", hive.Registry.GetLocalSecret());

                foreach (var manager in hive.Managers)
                {
                    Assert.Single(hiveFixture.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                dnsEntry = hive.Dns.Get("xunit-registry.neonforge.net");

                Assert.NotNull(dnsEntry);
                Assert.True(dnsEntry.IsSystem);

                //-------------------------------------------------------------
                // Remove the registry and verify that service and related items
                // are no longer present.

                playbook =
@"
- name: test
  hosts: localhost
  vars_files:
    - secrets.yaml
  tasks:
    - name: registry
      neon_docker_registry:
        state: absent
        hostname: xunit-registry.neonforge.net
";
                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("registry");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                Assert.Empty(hiveFixture.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Empty(hiveFixture.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry.neonforge.net"));
                Assert.Empty(hiveFixture.ListTrafficManagers("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Empty(hiveFixture.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                registryCredentials = hive.Registry.GetCredentials("xunit-registry.neonforge.net");

                Assert.Null(registryCredentials);

                Assert.Null(hive.Registry.GetLocalHostname());
                Assert.Null(hive.Registry.GetLocalSecret());

                foreach (var manager in hive.Managers)
                {
                    Assert.Empty(hiveFixture.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                dnsEntry = hive.Dns.Get("xunit-registry.neonforge.net");

                Assert.Null(dnsEntry);

                //-------------------------------------------------------------
                // Run the playbook again and verify that nothing changed.

                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("registry");

                Assert.True(taskResult.Success);
                Assert.False(taskResult.Changed);

                Assert.Empty(hiveFixture.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Empty(hiveFixture.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry.neonforge.net"));
                Assert.Empty(hiveFixture.ListTrafficManagers("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Empty(hiveFixture.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                registryCredentials = hive.Registry.GetCredentials("xunit-registry.neonforge.net");

                Assert.Null(registryCredentials);

                Assert.Null(hive.Registry.GetLocalHostname());
                Assert.Null(hive.Registry.GetLocalSecret());

                foreach (var manager in hive.Managers)
                {
                    Assert.Empty(hiveFixture.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                dnsEntry = hive.Dns.Get("xunit-registry.neonforge.net");

                Assert.Null(dnsEntry);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Update()
        {
            // We're going to create a temporary Ansible working folder and copy 
            // the test secrets file there so we can reference it from the playbooks.

            using (var folder = new TempFolder())
            {
                File.Copy(TestHelper.AnsibleSecretsPath, Path.Combine(folder.Path, "secrets.yaml"));

                //-------------------------------------------------------------
                // Deploy a local Docker registry so we can verify that we can 
                // update it.

                var playbook =
@"
- name: test
  hosts: localhost
  vars_files:
    - secrets.yaml
  tasks:
    - name: registry
      neon_docker_registry:
        state: present
        hostname: xunit-registry.neonforge.net
        certificate: ""{{ _neonforge_net_pem }}""
        username: test
        password: password
        secret: secret
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                var taskResult = results.GetTaskResult("registry");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                Assert.Single(hiveFixture.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry.neonforge.net"));
                Assert.Single(hiveFixture.ListTrafficManagers("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                var registryCredentials = hive.Registry.GetCredentials("xunit-registry.neonforge.net");

                Assert.NotNull(registryCredentials);
                Assert.Equal("xunit-registry.neonforge.net", registryCredentials.Registry);
                Assert.Equal("test", registryCredentials.Username);
                Assert.Equal("password", registryCredentials.Password);

                Assert.Equal("xunit-registry.neonforge.net", hive.Registry.GetLocalHostname());
                Assert.Equal("secret", hive.Registry.GetLocalSecret());

                foreach (var manager in hive.Managers)
                {
                    Assert.Single(hiveFixture.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                var dnsEntry = hive.Dns.Get("xunit-registry.neonforge.net");

                Assert.NotNull(dnsEntry);
                Assert.True(dnsEntry.IsSystem);

                //-------------------------------------------------------------
                // Update the hostname and verify.

                playbook =
@"
- name: test
  hosts: localhost
  vars_files:
    - secrets.yaml
  tasks:
    - name: registry
      neon_docker_registry:
        state: present
        hostname: xunit-registry2.neonforge.net
        certificate: ""{{ _neonforge_net_pem }}""
        username: test
        password: password
        secret: secret
";

                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("registry");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                Assert.Single(hiveFixture.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry2.neonforge.net"));
                Assert.Single(hiveFixture.ListTrafficManagers("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                registryCredentials = hive.Registry.GetCredentials("xunit-registry2.neonforge.net");

                Assert.NotNull(registryCredentials);
                Assert.Equal("xunit-registry2.neonforge.net", registryCredentials.Registry);
                Assert.Equal("test", registryCredentials.Username);
                Assert.Equal("password", registryCredentials.Password);

                Assert.Equal("xunit-registry2.neonforge.net", hive.Registry.GetLocalHostname());
                Assert.Equal("secret", hive.Registry.GetLocalSecret());

                foreach (var manager in hive.Managers)
                {
                    Assert.Single(hiveFixture.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                dnsEntry = hive.Dns.Get("xunit-registry2.neonforge.net");

                Assert.NotNull(dnsEntry);
                Assert.True(dnsEntry.IsSystem);

                //-------------------------------------------------------------
                // Update the username and verify.

                playbook =
@"
- name: test
  hosts: localhost
  vars_files:
    - secrets.yaml
  tasks:
    - name: registry
      neon_docker_registry:
        state: present
        hostname: xunit-registry2.neonforge.net
        certificate: ""{{ _neonforge_net_pem }}""
        username: test2
        password: password
        secret: secret
";

                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("registry");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                Assert.Single(hiveFixture.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry2.neonforge.net"));
                Assert.Single(hiveFixture.ListTrafficManagers("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                registryCredentials = hive.Registry.GetCredentials("xunit-registry2.neonforge.net");

                Assert.NotNull(registryCredentials);
                Assert.Equal("xunit-registry2.neonforge.net", registryCredentials.Registry);
                Assert.Equal("test2", registryCredentials.Username);
                Assert.Equal("password", registryCredentials.Password);

                Assert.Equal("xunit-registry2.neonforge.net", hive.Registry.GetLocalHostname());
                Assert.Equal("secret", hive.Registry.GetLocalSecret());

                foreach (var manager in hive.Managers)
                {
                    Assert.Single(hiveFixture.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                dnsEntry = hive.Dns.Get("xunit-registry2.neonforge.net");

                Assert.NotNull(dnsEntry);
                Assert.True(dnsEntry.IsSystem);

                //-------------------------------------------------------------
                // Update the password and verify.

                playbook =
@"
- name: test
  hosts: localhost
  vars_files:
    - secrets.yaml
  tasks:
    - name: registry
      neon_docker_registry:
        state: present
        hostname: xunit-registry2.neonforge.net
        certificate: ""{{ _neonforge_net_pem }}""
        username: test2
        password: password2
        secret: secret
";

                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("registry");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                Assert.Single(hiveFixture.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry2.neonforge.net"));
                Assert.Single(hiveFixture.ListTrafficManagers("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                registryCredentials = hive.Registry.GetCredentials("xunit-registry2.neonforge.net");

                Assert.NotNull(registryCredentials);
                Assert.Equal("xunit-registry2.neonforge.net", registryCredentials.Registry);
                Assert.Equal("test2", registryCredentials.Username);
                Assert.Equal("password2", registryCredentials.Password);

                Assert.Equal("xunit-registry2.neonforge.net", hive.Registry.GetLocalHostname());
                Assert.Equal("secret", hive.Registry.GetLocalSecret());

                foreach (var manager in hive.Managers)
                {
                    Assert.Single(hiveFixture.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                dnsEntry = hive.Dns.Get("xunit-registry2.neonforge.net");

                Assert.NotNull(dnsEntry);
                Assert.True(dnsEntry.IsSystem);

                //-------------------------------------------------------------
                // Update the secret and verify.

                playbook =
@"
- name: test
  hosts: localhost
  vars_files:
    - secrets.yaml
  tasks:
    - name: registry
      neon_docker_registry:
        state: present
        hostname: xunit-registry2.neonforge.net
        certificate: ""{{ _neonforge_net_pem }}""
        username: test2
        password: password2
        secret: secret2
";

                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("registry");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                Assert.Single(hiveFixture.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry2.neonforge.net"));
                Assert.Single(hiveFixture.ListTrafficManagers("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                registryCredentials = hive.Registry.GetCredentials("xunit-registry2.neonforge.net");

                Assert.NotNull(registryCredentials);
                Assert.Equal("xunit-registry2.neonforge.net", registryCredentials.Registry);
                Assert.Equal("test2", registryCredentials.Username);
                Assert.Equal("password2", registryCredentials.Password);

                Assert.Equal("xunit-registry2.neonforge.net", hive.Registry.GetLocalHostname());
                Assert.Equal("secret2", hive.Registry.GetLocalSecret());

                foreach (var manager in hive.Managers)
                {
                    Assert.Single(hiveFixture.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                dnsEntry = hive.Dns.Get("xunit-registry2.neonforge.net");

                Assert.NotNull(dnsEntry);
                Assert.True(dnsEntry.IsSystem);

                //-------------------------------------------------------------
                // Run the playbook again and verify that nothing changed this time.

                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("registry");

                Assert.True(taskResult.Success);
                Assert.False(taskResult.Changed);

                Assert.Single(hiveFixture.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry2.neonforge.net"));
                Assert.Single(hiveFixture.ListTrafficManagers("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                registryCredentials = hive.Registry.GetCredentials("xunit-registry2.neonforge.net");

                Assert.NotNull(registryCredentials);
                Assert.Equal("xunit-registry2.neonforge.net", registryCredentials.Registry);
                Assert.Equal("test2", registryCredentials.Username);
                Assert.Equal("password2", registryCredentials.Password);

                Assert.Equal("xunit-registry2.neonforge.net", hive.Registry.GetLocalHostname());
                Assert.Equal("secret2", hive.Registry.GetLocalSecret());

                foreach (var manager in hive.Managers)
                {
                    Assert.Single(hiveFixture.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                dnsEntry = hive.Dns.Get("xunit-registry2.neonforge.net");

                Assert.NotNull(dnsEntry);
                Assert.True(dnsEntry.IsSystem);
            }
        }

        [Fact]
        public void EndToEnd()
        {
            // We're going to create a temporary Ansible working folder and copy 
            // the test secrets file there so we can reference it from the playbooks.

            using (var folder = new TempFolder())
            {
                File.Copy(TestHelper.AnsibleSecretsPath, Path.Combine(folder.Path, "secrets.yaml"));

                //-------------------------------------------------------------
                // Deploy a local Docker registry so we can verify that it's
                // actually working below.

                var playbook =
@"
- name: test
  hosts: localhost
  vars_files:
    - secrets.yaml
  tasks:
    - name: registry
      neon_docker_registry:
        state: present
        hostname: xunit-registry.neonforge.net
        certificate: ""{{ _neonforge_net_pem }}""
        username: test
        password: password
        secret: secret
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                var taskResult = results.GetTaskResult("registry");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                Assert.Single(hiveFixture.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry.neonforge.net"));
                Assert.Single(hiveFixture.ListTrafficManagers("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                var registryCredentials = hive.Registry.GetCredentials("xunit-registry.neonforge.net");

                Assert.NotNull(registryCredentials);
                Assert.Equal("xunit-registry.neonforge.net", registryCredentials.Registry);
                Assert.Equal("test", registryCredentials.Username);
                Assert.Equal("password", registryCredentials.Password);

                Assert.Equal("xunit-registry.neonforge.net", hive.Registry.GetLocalHostname());
                Assert.Equal("secret", hive.Registry.GetLocalSecret());

                foreach (var node in hive.Managers)
                {
                    Assert.Single(hiveFixture.ListVolumes(node.Name).Where(name => name == "neon-registry"));
                }

                var dnsEntry = hive.Dns.Get("xunit-registry.neonforge.net");

                Assert.NotNull(dnsEntry);
                Assert.True(dnsEntry.IsSystem);

                //-------------------------------------------------------------
                // Verify that the new registry is actually working by pushing and
                // pulling an image from it.

                var manager = hive.GetReachableManager();

                // Ensure that any test images are removed from previous test runs.

                manager.DockerCommand(RunOptions.None, "docker", "image", "rm", "nhive/test:latest");
                manager.DockerCommand(RunOptions.None, "docker", "image", "rm", "xunit-registry.neonforge.net/test-image:latest");

                // Pull a test image from the Docker public registry.

                var response = manager.DockerCommand(RunOptions.None, "docker", "pull", "nhive/test:latest");

                if (response.ExitCode != 0)
                {
                    throw new Exception(response.OutputText);    
                }

                // Tag the image for the new registry.

                response = manager.DockerCommand(RunOptions.None, "docker", "tag", "nhive/test:latest", "xunit-registry.neonforge.net/test-image:latest");

                if (response.ExitCode != 0)
                {
                    throw new Exception(response.OutputText);
                }

                // Push the image to the new registry.

                response = manager.DockerCommand(RunOptions.None, "docker", "push", "xunit-registry.neonforge.net/test-image:latest");

                if (response.ExitCode != 0)
                {
                    throw new Exception(response.OutputText);
                }

                // Remove the two local images and verify that they are no longer present.

                manager.DockerCommand(RunOptions.None, "docker", "image", "rm", "nhive/test:latest");
                manager.DockerCommand(RunOptions.None, "docker", "image", "rm", "xunit-registry.neonforge.net/test-image:latest");

                response = manager.DockerCommand(RunOptions.None, "docker", "image", "ls");

                Assert.Equal(0, response.ExitCode);
                Assert.DoesNotContain("nhive/test", response.AllText);
                Assert.DoesNotContain("xunit-registry.neonforge.net/test-image", response.AllText);

                // Pull the image from new registry and verify.

                response = manager.DockerCommand(RunOptions.None, "docker", "pull", "xunit-registry.neonforge.net/test-image:latest");

                if (response.ExitCode != 0)
                {
                    throw new Exception(response.OutputText);
                }

                response = manager.DockerCommand(RunOptions.None, "docker", "image", "ls");
                Assert.Contains("xunit-registry.neonforge.net/test-image", response.AllText);

                // Remove the local image we just pulled and verify.

                manager.DockerCommand(RunOptions.None, "docker", "image", "rm", "xunit-registry.neonforge.net/test-image:latest");

                response = manager.DockerCommand(RunOptions.None, "docker", "image", "ls");

                Assert.Equal(0, response.ExitCode);
                Assert.DoesNotContain("xunit-registry.neonforge.net/test-image", response.AllText);

                //-------------------------------------------------------------
                // Pull the image via Ansible and verify.

                playbook =
$@"
- name: test
  hosts: {manager.Name}
  tasks:
    - name: pull
      command: docker pull xunit-registry.neonforge.net/test-image
";
                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook);
                taskResult = results.GetTaskResult("pull");

                Assert.True(taskResult.Success);

                response = manager.DockerCommand(RunOptions.None, "docker", "image", "ls");
                Assert.Contains("xunit-registry.neonforge.net/test-image", response.AllText);

                // Remove the local image we just pulled and verify.

                manager.DockerCommand(RunOptions.None, "docker", "image", "rm", "xunit-registry.neonforge.net/test-image:latest");

                response = manager.DockerCommand(RunOptions.None, "docker", "image", "ls");

                Assert.Equal(0, response.ExitCode);
                Assert.DoesNotContain("xunit-registry.neonforge.net/test-image", response.AllText);

                //-------------------------------------------------------------
                // Verify that the registry is using [Ceph] to persist the images,
                // as opposed to a standard Docker volume.

                Assert.True(manager.DirectoryExists("/mnt/hivefs/docker/neon-registry/docker/registry/v2"));
            }
        }

        [Fact]
        public void Prune()
        {
            // We're going to create a temporary Ansible working folder and copy 
            // the test secrets file there so we can reference it from the playbooks.

            using (var folder = new TempFolder())
            {
                File.Copy(TestHelper.AnsibleSecretsPath, Path.Combine(folder.Path, "secrets.yaml"));

                //-------------------------------------------------------------
                // Deploy a local Docker registry so we can test registry pruning.

                var playbook =
@"
- name: test
  hosts: localhost
  vars_files:
    - secrets.yaml
  tasks:
    - name: registry
      neon_docker_registry:
        state: present
        hostname: xunit-registry.neonforge.net
        certificate: ""{{ _neonforge_net_pem }}""
        username: test
        password: password
        secret: secret
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                var taskResult = results.GetTaskResult("registry");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                Assert.Single(hiveFixture.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry.neonforge.net"));
                Assert.Single(hiveFixture.ListTrafficManagers("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(hiveFixture.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                var registryCredentials = hive.Registry.GetCredentials("xunit-registry.neonforge.net");

                Assert.NotNull(registryCredentials);
                Assert.Equal("xunit-registry.neonforge.net", registryCredentials.Registry);
                Assert.Equal("test", registryCredentials.Username);
                Assert.Equal("password", registryCredentials.Password);

                Assert.Equal("xunit-registry.neonforge.net", hive.Registry.GetLocalHostname());
                Assert.Equal("secret", hive.Registry.GetLocalSecret());

                foreach (var node in hive.Managers)
                {
                    Assert.Single(hiveFixture.ListVolumes(node.Name).Where(name => name == "neon-registry"));
                }

                var dnsEntry = hive.Dns.Get("xunit-registry.neonforge.net");

                Assert.NotNull(dnsEntry);
                Assert.True(dnsEntry.IsSystem);

                //-------------------------------------------------------------
                // Verify that prune doesn't barf.

                // $todo(jeff.lill);
                //
                // Implement a test that actually verifies that image layers are pruned.

                playbook =
@"
- name: test
  hosts: localhost
  vars_files:
    - secrets.yaml
  tasks:
    - name: registry
      neon_docker_registry:
        state: prune
        hostname: xunit-registry.neonforge.net
";
                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);

                taskResult = results.GetTaskResult("registry");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                //-------------------------------------------------------------
                // Verify that the registry is read/write after pruning.

                var manager = hive.GetReachableManager();

                // Ensure that any test images are removed from previous test runs.

                manager.DockerCommand(RunOptions.None, "docker", "image", "rm", "nhive/test:latest");
                manager.DockerCommand(RunOptions.None, "docker", "image", "rm", "xunit-registry.neonforge.net/test-image:latest");

                // Pull a test image from the Docker public registry.

                var response = manager.DockerCommand(RunOptions.None, "docker", "pull", "nhive/test:latest");

                if (response.ExitCode != 0)
                {
                    throw new Exception(response.OutputText);
                }

                // Tag the image for the new registry.

                response = manager.DockerCommand(RunOptions.None, "docker", "tag", "nhive/test:latest", "xunit-registry.neonforge.net/test-image:latest");

                if (response.ExitCode != 0)
                {
                    throw new Exception(response.OutputText);
                }

                // Push the image to the new registry.

                response = manager.DockerCommand(RunOptions.None, "docker", "push", "xunit-registry.neonforge.net/test-image:latest");

                if (response.ExitCode != 0)
                {
                    throw new Exception(response.OutputText);
                }

                // Remove the two local images and verify that they are no longer present.

                manager.DockerCommand(RunOptions.None, "docker", "image", "rm", "nhive/test:latest");
                manager.DockerCommand(RunOptions.None, "docker", "image", "rm", "xunit-registry.neonforge.net/test-image:latest");

                response = manager.DockerCommand(RunOptions.None, "docker", "image", "ls");

                Assert.Equal(0, response.ExitCode);
                Assert.DoesNotContain("nhive/test", response.AllText);
                Assert.DoesNotContain("xunit-registry.neonforge.net/test-image", response.AllText);

                // Pull the image from new registry and verify.

                response = manager.DockerCommand(RunOptions.None, "docker", "pull", "xunit-registry.neonforge.net/test-image:latest");

                if (response.ExitCode != 0)
                {
                    throw new Exception(response.OutputText);
                }

                response = manager.DockerCommand(RunOptions.None, "docker", "image", "ls");
                Assert.Contains("xunit-registry.neonforge.net/test-image", response.AllText);

                // Remove the local image we just pulled and verify.

                manager.DockerCommand(RunOptions.None, "docker", "image", "rm", "xunit-registry.neonforge.net/test-image:latest");

                response = manager.DockerCommand(RunOptions.None, "docker", "image", "ls");

                Assert.Equal(0, response.ExitCode);
                Assert.DoesNotContain("xunit-registry.neonforge.net/test-image", response.AllText);

                //-------------------------------------------------------------
                // Pull the image via Ansible and verify.

                
            }
        }
    }
}
