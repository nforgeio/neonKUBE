//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleDockerLogin.cs
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

using Neon.Cluster;
using Neon.Data;

using Xunit;

// $todo(jeff.lill):
//
// Complete this implementation.  Note that we also need to update
// registry cache credentials when we're logging into or out of
// the Docker public registry.

namespace TestNeonCluster
{
    public class Test_AnsibleDockerLogin : IClassFixture<ClusterFixture>
    {
        private ClusterFixture  cluster;
        private ClusterProxy    clusterProxy;

        public Test_AnsibleDockerLogin(ClusterFixture cluster)
        {
            cluster.LoginAndInitialize();

            this.cluster = cluster;
            this.clusterProxy = cluster.Cluster;

            // Ensure that we're not already logged into Docker Hub.

            clusterProxy.Registry.Logout(NeonClusterConst.DockerPublicRegistry);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void LoginAndOut()
        {
            // $todo(jeff.lill): I'm not actually verifying the login status on the nodes.

            // We're going to create a temporary Ansible working folder and copy 
            // the test secrets file there so we can reference it from the playbooks.

            using (var folder = new TempFolder())
            {
                File.Copy(TestHelper.AnsibleSecretsPath, Path.Combine(folder.Path, "secrets.yaml"));

                //-----------------------------------------------------------------
                // Verify that we log into a test Docker hub account.

                var playbook =
@"
- name: test
  hosts: localhost
  vars_files:
    - secrets.yaml
  tasks:
    - name: login
      neon_docker_login:
        state: present
        registry: registry-1.docker.io
        username: ""{{ DOCKER_TEST_USERNAME }}""
        password: ""{{ DOCKER_TEST_PASSWORD }}""
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                var taskResult = results.GetTaskResult("login");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                Assert.NotNull(clusterProxy.Registry.GetCredentials(NeonClusterConst.DockerPublicRegistry));

                //-----------------------------------------------------------------
                // Run the play again and verify that [changed=false].

                playbook =
@"
- name: test
  hosts: localhost
  vars_files: 
    - secrets.yaml
  tasks:
    - name: login
      neon_docker_login:
        state: present
        registry: registry-1.docker.io
        username: ""{{ DOCKER_TEST_USERNAME }}""
        password: ""{{ DOCKER_TEST_PASSWORD }}""
";
                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("login");

                Assert.True(taskResult.Success);
                Assert.False(taskResult.Changed);

                Assert.NotNull(clusterProxy.Registry.GetCredentials(NeonClusterConst.DockerPublicRegistry));

                //-----------------------------------------------------------------
                // Verify that we log off the test Docker hub account.

                playbook =
@"
- name: test
  hosts: localhost
  vars_files: 
    - secrets.yaml
  tasks:
    - name: login
      neon_docker_login:
        state: absent
        registry: registry-1.docker.io
";
                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("login");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                Assert.Null(clusterProxy.Registry.GetCredentials(NeonClusterConst.DockerPublicRegistry));

                //-----------------------------------------------------------------
                // Run the play again and verify that [changed=false].

                playbook =
@"
- name: test
  hosts: localhost
  vars_files: 
    - secrets.yaml
  tasks:
    - name: login
      neon_docker_login:
        state: absent
        registry: registry-1.docker.io
";
                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("login");

                Assert.True(taskResult.Success);
                Assert.False(taskResult.Changed);

                Assert.Null(clusterProxy.Registry.GetCredentials(NeonClusterConst.DockerPublicRegistry));
            }
        }

        [Fact(Skip = "TODO")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void RegistryCache()
        {
            // $todo(jeff.lill):
            //
            // We need to verify that this works for clusters with registry caches as
            // well as those without.  Here's some things to test:
            //
            //      * Are registry caches restarted with the new credentials?
            //
            //      * For clusters with caches, we shouldn't change the node
            //        credentials for the Docker public registry because
            //        nodes don't authenticate against the cache and only
            //        the cache authenticates against the registry.
            //
            //      * Verify that we actually use the current image from
            //        the running cache containers before we restart them.
            //
            //      * Verify that we fall-back to a reasonable image if
            //        cache containers arfen't running.
            //
            //      * Verify the correct behavior when there's no cache enabled.
        }
    }
}
