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
using Neon.Data;
using Neon.IO;
using Neon.Hive;
using Neon.Xunit;
using Neon.Xunit.Couchbase;
using Neon.Xunit.Hive;

using Xunit;

// $todo(jeff.lill):
//
// Complete this implementation.  Note that we also need to update
// registry cache credentials when we're logging into or out of
// the Docker public registry.

namespace TestHive
{
    public class Test_AnsibleDockerLogin : IClassFixture<HiveFixture>
    {
        private HiveFixture     hiveFixture;
        private HiveProxy       hive;

        public Test_AnsibleDockerLogin(HiveFixture fixture)
        {
            fixture.LoginAndInitialize();

            this.hiveFixture = fixture;
            this.hive        = fixture.Hive;

            // Ensure that we're not already logged into Docker Hub.

            this.hive.Registry.Logout(HiveConst.DockerPublicRegistry);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void LoginAndOut()
        {
            using (var folder = new TempFolder())
            {
                // We're going to create a temporary Ansible working folder and copy 
                // the test secrets file there so we can reference it from the playbooks.

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
        registry: docker.io
        username: ""{{ DOCKER_TEST_USERNAME }}""
        password: ""{{ DOCKER_TEST_PASSWORD }}""
";
                var results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                var taskResult = results.GetTaskResult("login");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);
                Assert.NotNull(hive.Registry.GetCredentials(HiveConst.DockerPublicRegistry));

                // Verify the login by examining the [/home/USER/.docker/conf.json] file on one
                // of the nodes and then verifying we're authenticated.

                var userDockerConfPath   = $"/home/{hive.HiveLogin.SshUsername}/.docker/config.json";
                var firstManager         = hive.FirstManager;
                var dockerAuthId         = "https://index.docker.io/v1/";

                Assert.True(firstManager.FileExists(userDockerConfPath));

                var userConfJson = firstManager.DownloadText(userDockerConfPath);
                var userConf     = NeonHelper.JsonDeserialize<dynamic>(userConfJson);

                Assert.True(!string.IsNullOrEmpty((string)userConf.auths[dockerAuthId].auth));

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
        registry: docker.io
        username: ""{{ DOCKER_TEST_USERNAME }}""
        password: ""{{ DOCKER_TEST_PASSWORD }}""
";
                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("login");

                Assert.True(taskResult.Success);
                Assert.False(taskResult.Changed);

                Assert.NotNull(hive.Registry.GetCredentials(HiveConst.DockerPublicRegistry));

                Assert.True(firstManager.FileExists(userDockerConfPath));

                userConfJson = firstManager.DownloadText(userDockerConfPath);
                userConf     = NeonHelper.JsonDeserialize<dynamic>(userConfJson);

                Assert.True(!string.IsNullOrEmpty((string)userConf.auths[dockerAuthId].auth));

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
        registry: docker.io
";
                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("login");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                Assert.Null(hive.Registry.GetCredentials(HiveConst.DockerPublicRegistry));

                // Verify the logout by ensuring that the [/home/USER/.docker/conf.json]
                // and [/root/.docker/conf.json] doesn't exist on one of the nodes or that
                // it lacks credentials for [docker.io].

                if (firstManager.FileExists(userDockerConfPath))
                {
                    // Both config files should exist if one of them is present.

                    Assert.True(firstManager.FileExists(userDockerConfPath));

                    userConfJson = firstManager.DownloadText(userDockerConfPath);
                    userConf     = NeonHelper.JsonDeserialize<dynamic>(userConfJson);

                    Assert.Null(userConf.auths[dockerAuthId]);
                }

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
        registry: docker.io
";
                results = AnsiblePlayer.PlayInFolderNoGather(folder.Path, playbook, "--vault-id", TestHelper.AnsiblePasswordFile);
                taskResult = results.GetTaskResult("login");

                Assert.True(taskResult.Success);
                Assert.False(taskResult.Changed);

                Assert.Null(hive.Registry.GetCredentials(HiveConst.DockerPublicRegistry));

                // Verify the logout by ensuring that the [/home/USER/.docker/conf.json]
                // and [/root/.docker.conf.json] doesn't exist on one of the nodes or that
                // it lacks credentials for [docker.io].

                if (firstManager.FileExists(userDockerConfPath))
                {
                    // Both config files should exist if one of them is present.

                    Assert.True(firstManager.FileExists(userDockerConfPath));

                    userConfJson = firstManager.DownloadText(userDockerConfPath);
                    userConf = NeonHelper.JsonDeserialize<dynamic>(userConfJson);

                    Assert.Null(userConf.auths[dockerAuthId]);
                }
            }
        }

        [Fact(Skip = "TODO")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void RegistryCache()
        {
            // $todo(jeff.lill):
            //
            // We need to verify that this works for hives with registry caches as
            // well as those without.  Here's some things to test:
            //
            //      * Are registry caches restarted with the new credentials?
            //
            //      * For hives with caches, we shouldn't change the node
            //        credentials for the Docker public registry because
            //        nodes don't authenticate against the cache and only
            //        the cache authenticates against the registry.
            //
            //      * Verify that we actually use the current image from
            //        the running cache containers before we restart them.
            //
            //      * Verify that we fall-back to a reasonable image if
            //        cache containers aren't running.
            //
            //      * Verify the correct behavior when there's no cache enabled.
        }
    }
}
