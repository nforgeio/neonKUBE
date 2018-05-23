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

using Neon.Cluster;
using Neon.Data;
using Neon.Common;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Cluster;
using Neon.Xunit.Couchbase;

using Xunit;

namespace TestNeonCluster
{
    public class Test_AnsibleDockerRegistry : IClassFixture<ClusterFixture>
    {
        private ClusterFixture cluster;
        private ClusterProxy clusterProxy;

        public Test_AnsibleDockerRegistry(ClusterFixture cluster)
        {
            if (!cluster.LoginAndInitialize())
            {
                cluster.ClearVolumes();
            }

            this.cluster = cluster;
            this.clusterProxy = cluster.Cluster;

            // Ensure that tests start without a local registry
            // and related assets.

            var manager = clusterProxy.GetHealthyManager();

            if (clusterProxy.InspectService("neon-registry") != null)
            {
                manager.DockerCommand(RunOptions.None, "docker service rm neon-registry");
            }

            clusterProxy.Certificate.Remove("neon-registry");
            clusterProxy.PublicLoadBalancer.RemoveRule("neon-registry");
            clusterProxy.Hosts.Remove("xunit-registry.neonforge.net");
            clusterProxy.Hosts.Remove("xunit-registry2.neonforge.net");
            clusterProxy.Registry.Remove("xunit-registry.neonforge.net");
            clusterProxy.Registry.Remove("xunit-registry2.neonforge.net");
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

                Assert.Single(cluster.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Single(cluster.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry.neonforge.net"));
                Assert.Single(cluster.ListLoadBalancerRules("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(cluster.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                var registryCredentials = clusterProxy.Registry.Get("xunit-registry.neonforge.net");

                Assert.NotNull(registryCredentials);
                Assert.Equal("xunit-registry.neonforge.net", registryCredentials.Registry);
                Assert.Equal("test", registryCredentials.Username);
                Assert.Equal("password", registryCredentials.Password);

                Assert.Equal("xunit-registry.neonforge.net", clusterProxy.Registry.GetLocalHostname());
                Assert.Equal("secret", clusterProxy.Registry.GetLocalSecret());

                foreach (var manager in clusterProxy.Managers)
                {
                    Assert.Single(cluster.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                var dnsEntry = clusterProxy.Hosts.Get("xunit-registry.neonforge.net");

                Assert.NotNull(dnsEntry);
                Assert.True(dnsEntry.IsSystem);

                //-------------------------------------------------------------
                // Run the playbook again and verify that nothing changed.

                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("registry");

                Assert.True(taskResult.Success);
                Assert.False(taskResult.Changed);

                Assert.Single(cluster.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Single(cluster.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry.neonforge.net"));
                Assert.Single(cluster.ListLoadBalancerRules("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(cluster.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                registryCredentials = clusterProxy.Registry.Get("xunit-registry.neonforge.net");

                Assert.NotNull(registryCredentials);
                Assert.Equal("xunit-registry.neonforge.net", registryCredentials.Registry);
                Assert.Equal("test", registryCredentials.Username);
                Assert.Equal("password", registryCredentials.Password);

                Assert.Equal("xunit-registry.neonforge.net", clusterProxy.Registry.GetLocalHostname());
                Assert.Equal("secret", clusterProxy.Registry.GetLocalSecret());

                foreach (var manager in clusterProxy.Managers)
                {
                    Assert.Single(cluster.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                dnsEntry = clusterProxy.Hosts.Get("xunit-registry.neonforge.net");

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

                Assert.Empty(cluster.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Empty(cluster.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry.neonforge.net"));
                Assert.Empty(cluster.ListLoadBalancerRules("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Empty(cluster.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                registryCredentials = clusterProxy.Registry.Get("xunit-registry.neonforge.net");

                Assert.Null(registryCredentials);

                Assert.Null(clusterProxy.Registry.GetLocalHostname());
                Assert.Null(clusterProxy.Registry.GetLocalSecret());

                foreach (var manager in clusterProxy.Managers)
                {
                    Assert.Empty(cluster.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                dnsEntry = clusterProxy.Hosts.Get("xunit-registry.neonforge.net");

                Assert.Null(dnsEntry);

                //-------------------------------------------------------------
                // Run the playbook again and verify that nothing changed.

                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("registry");

                Assert.True(taskResult.Success);
                Assert.False(taskResult.Changed);

                Assert.Empty(cluster.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Empty(cluster.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry.neonforge.net"));
                Assert.Empty(cluster.ListLoadBalancerRules("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Empty(cluster.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                registryCredentials = clusterProxy.Registry.Get("xunit-registry.neonforge.net");

                Assert.Null(registryCredentials);

                Assert.Null(clusterProxy.Registry.GetLocalHostname());
                Assert.Null(clusterProxy.Registry.GetLocalSecret());

                foreach (var manager in clusterProxy.Managers)
                {
                    Assert.Empty(cluster.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                dnsEntry = clusterProxy.Hosts.Get("xunit-registry.neonforge.net");

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

                Assert.Single(cluster.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Single(cluster.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry.neonforge.net"));
                Assert.Single(cluster.ListLoadBalancerRules("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(cluster.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                var registryCredentials = clusterProxy.Registry.Get("xunit-registry.neonforge.net");

                Assert.NotNull(registryCredentials);
                Assert.Equal("xunit-registry.neonforge.net", registryCredentials.Registry);
                Assert.Equal("test", registryCredentials.Username);
                Assert.Equal("password", registryCredentials.Password);

                Assert.Equal("xunit-registry.neonforge.net", clusterProxy.Registry.GetLocalHostname());
                Assert.Equal("secret", clusterProxy.Registry.GetLocalSecret());

                foreach (var manager in clusterProxy.Managers)
                {
                    Assert.Single(cluster.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                var dnsEntry = clusterProxy.Hosts.Get("xunit-registry.neonforge.net");

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

                Assert.Single(cluster.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Single(cluster.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry2.neonforge.net"));
                Assert.Single(cluster.ListLoadBalancerRules("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(cluster.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                registryCredentials = clusterProxy.Registry.Get("xunit-registry2.neonforge.net");

                Assert.NotNull(registryCredentials);
                Assert.Equal("xunit-registry2.neonforge.net", registryCredentials.Registry);
                Assert.Equal("test", registryCredentials.Username);
                Assert.Equal("password", registryCredentials.Password);

                Assert.Equal("xunit-registry2.neonforge.net", clusterProxy.Registry.GetLocalHostname());
                Assert.Equal("secret", clusterProxy.Registry.GetLocalSecret());

                foreach (var manager in clusterProxy.Managers)
                {
                    Assert.Single(cluster.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                dnsEntry = clusterProxy.Hosts.Get("xunit-registry2.neonforge.net");

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

                Assert.Single(cluster.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Single(cluster.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry2.neonforge.net"));
                Assert.Single(cluster.ListLoadBalancerRules("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(cluster.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                registryCredentials = clusterProxy.Registry.Get("xunit-registry2.neonforge.net");

                Assert.NotNull(registryCredentials);
                Assert.Equal("xunit-registry2.neonforge.net", registryCredentials.Registry);
                Assert.Equal("test2", registryCredentials.Username);
                Assert.Equal("password", registryCredentials.Password);

                Assert.Equal("xunit-registry2.neonforge.net", clusterProxy.Registry.GetLocalHostname());
                Assert.Equal("secret", clusterProxy.Registry.GetLocalSecret());

                foreach (var manager in clusterProxy.Managers)
                {
                    Assert.Single(cluster.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                dnsEntry = clusterProxy.Hosts.Get("xunit-registry2.neonforge.net");

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

                Assert.Single(cluster.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Single(cluster.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry2.neonforge.net"));
                Assert.Single(cluster.ListLoadBalancerRules("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(cluster.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                registryCredentials = clusterProxy.Registry.Get("xunit-registry2.neonforge.net");

                Assert.NotNull(registryCredentials);
                Assert.Equal("xunit-registry2.neonforge.net", registryCredentials.Registry);
                Assert.Equal("test2", registryCredentials.Username);
                Assert.Equal("password2", registryCredentials.Password);

                Assert.Equal("xunit-registry2.neonforge.net", clusterProxy.Registry.GetLocalHostname());
                Assert.Equal("secret", clusterProxy.Registry.GetLocalSecret());

                foreach (var manager in clusterProxy.Managers)
                {
                    Assert.Single(cluster.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                dnsEntry = clusterProxy.Hosts.Get("xunit-registry2.neonforge.net");

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

                Assert.Single(cluster.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Single(cluster.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry2.neonforge.net"));
                Assert.Single(cluster.ListLoadBalancerRules("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(cluster.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                registryCredentials = clusterProxy.Registry.Get("xunit-registry2.neonforge.net");

                Assert.NotNull(registryCredentials);
                Assert.Equal("xunit-registry2.neonforge.net", registryCredentials.Registry);
                Assert.Equal("test2", registryCredentials.Username);
                Assert.Equal("password2", registryCredentials.Password);

                Assert.Equal("xunit-registry2.neonforge.net", clusterProxy.Registry.GetLocalHostname());
                Assert.Equal("secret2", clusterProxy.Registry.GetLocalSecret());

                foreach (var manager in clusterProxy.Managers)
                {
                    Assert.Single(cluster.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                dnsEntry = clusterProxy.Hosts.Get("xunit-registry2.neonforge.net");

                Assert.NotNull(dnsEntry);
                Assert.True(dnsEntry.IsSystem);

                //-------------------------------------------------------------
                // Run the playbook again and verify that nothing changed this time.

                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("registry");

                Assert.True(taskResult.Success);
                Assert.False(taskResult.Changed);

                Assert.Single(cluster.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Single(cluster.ListDnsEntries(includeSystem: true).Where(item => item.Hostname == "xunit-registry2.neonforge.net"));
                Assert.Single(cluster.ListLoadBalancerRules("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(cluster.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                registryCredentials = clusterProxy.Registry.Get("xunit-registry2.neonforge.net");

                Assert.NotNull(registryCredentials);
                Assert.Equal("xunit-registry2.neonforge.net", registryCredentials.Registry);
                Assert.Equal("test2", registryCredentials.Username);
                Assert.Equal("password2", registryCredentials.Password);

                Assert.Equal("xunit-registry2.neonforge.net", clusterProxy.Registry.GetLocalHostname());
                Assert.Equal("secret2", clusterProxy.Registry.GetLocalSecret());

                foreach (var manager in clusterProxy.Managers)
                {
                    Assert.Single(cluster.ListVolumes(manager.Name).Where(name => name == "neon-registry"));
                }

                dnsEntry = clusterProxy.Hosts.Get("xunit-registry2.neonforge.net");

                Assert.NotNull(dnsEntry);
                Assert.True(dnsEntry.IsSystem);
            }
        }
    }
}
