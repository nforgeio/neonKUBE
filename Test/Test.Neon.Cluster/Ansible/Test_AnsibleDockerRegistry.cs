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
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void DeployAndRemove()
        {
            //-----------------------------------------------------------------
            // Verify that we can deploy a local Docker registry.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: registry
      neon_docker_registry:
        state: present
        hostname: registry.neonforge.com
        certificate: ""{{ _neonforge_net_pem }}""
        username: test
        password: password
        secret: secret
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("registry");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            Assert.Single(cluster.ListServices(includeSystem: true).Where(s => s.Name == "neon-registry"));

            //-----------------------------------------------------------------
            // Wait for the DNS changes to converge and then verify that the
            // registry hostname has been redirected to the cluster managers.

            cluster.ConvergeDns();

            var manager = clusterProxy.GetHealthyManager();
        }
    }
}
