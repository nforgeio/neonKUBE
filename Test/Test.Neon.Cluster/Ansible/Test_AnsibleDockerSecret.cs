//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleDockerSecret.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

using Xunit;
using Xunit.Neon;

namespace TestNeonCluster
{
    public class Test_AnsibleDockerSecret : IClassFixture<ClusterFixture>
    {
        private ClusterFixture cluster;

        public Test_AnsibleDockerSecret(ClusterFixture cluster)
        {
            this.cluster = cluster;

            // We're going to use different secret names for each test
            // so we only need to reset the test fixture once for
            // all tests implemented by this class.

            cluster.Initialize(login: null);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void CreateText()
        {
            using (TestHelper.TempFolder("test.yaml",
@"
- name: test
  hosts: localhost
  tasks:
    - name: create secret
      neon_docker_secret:
        name: text-secret
        state: present
        text: password
"))
            {
                var response = NeonHelper.ExecuteCaptureStreams("neon", new object[] { "ansible", "play", "--", "test.yaml"});

                Assert.Equal(0, response.ExitCode);
                Assert.Single(cluster.ListSecrets().Where(s => s.Name == "test-secret"));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Remove()
        {
        }
    }
}
