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

using Couchbase;
using Couchbase.Core;
using Couchbase.Linq;
using Couchbase.Linq.Extensions;
using Couchbase.N1QL;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Cluster;
using Neon.Xunit.Couchbase;

using Neon.Data;
using Neon.Cluster;

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
            clusterProxy.Consul.KV.Delete($"{NeonClusterConst.ConsulDnsEntriesKey}/{NeonClusterConst.SystemDnsHostnamePrefix}neon-registry").Wait();
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
        hostname: registry.neonforge.net
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
                Assert.Single(cluster.ListDnsEntries(includeSystem: true).Where(item => item.Name == $"{NeonClusterConst.SystemDnsHostnamePrefix}neon-registry"));
                Assert.Single(cluster.ListLoadBalancerRules("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(cluster.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                //-------------------------------------------------------------
                // Run the playbook again and verify that nothing changed.

                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("registry");

                Assert.True(taskResult.Success);
                Assert.False(taskResult.Changed);

                Assert.Single(cluster.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Single(cluster.ListDnsEntries(includeSystem: true).Where(item => item.Name == $"{NeonClusterConst.SystemDnsHostnamePrefix}neon-registry"));
                Assert.Single(cluster.ListLoadBalancerRules("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Single(cluster.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

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
        hostname: registry.neonforge.net
";
                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("registry");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                Assert.Empty(cluster.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Empty(cluster.ListDnsEntries(includeSystem: true).Where(item => item.Name == $"{NeonClusterConst.SystemDnsHostnamePrefix}neon-registry"));
                Assert.Empty(cluster.ListLoadBalancerRules("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Empty(cluster.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));

                //-------------------------------------------------------------
                // Run the playbook again and verify that nothing changed.

                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("registry");

                Assert.True(taskResult.Success);
                Assert.False(taskResult.Changed);

                Assert.Empty(cluster.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));
                Assert.Empty(cluster.ListDnsEntries(includeSystem: true).Where(item => item.Name == $"{NeonClusterConst.SystemDnsHostnamePrefix}neon-registry"));
                Assert.Empty(cluster.ListLoadBalancerRules("public", includeSystem: true).Where(item => item.Name == "neon-registry"));
                Assert.Empty(cluster.ListCertificates(includeSystem: true).Where(name => name == "neon-registry"));
            }
        }
    }
}
