//-----------------------------------------------------------------------------
// FILE:	    Test_ClusterFixture.cs
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
    public class Test_ClusterFixture : IClassFixture<ClusterFixture>
    {
        private ClusterFixture cluster;

        public Test_ClusterFixture(ClusterFixture fixture)
        {
            this.cluster = fixture;

            // We're passing [login=null] below to connect to the cluster specified
            // by the NEON_TEST_CLUSTER environment variable.  This need to be initialized
            // with the login for a deployed cluster.

            cluster.Initialize(null,
                () =>
                {
                    cluster.CreateSecret("secret_text", "hello");
                    cluster.CreateSecret("secret_data", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                    cluster.CreateConfig("config_text", "hello");
                    cluster.CreateConfig("config_data", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                    cluster.CreateService("test-service", "alpine", serviceArgs: new string[] { "sleep", "1000000" });

                    var composeText =
@"version: '3'

services:
  sleeper:
    image: alpine
    command: sleep 1000000
    deploy:
      replicas: 2
";
                    cluster.DeployStack("test-stack", composeText);
                    cluster.CreateNetwork("test-network");
                });
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCluster)]
        public void Verify()
        {
            // Verify that the secrets, configs, networks, container, service, and stack were created.

            Assert.Single(cluster.ListSecrets().Where(item => item.Name == "secret_text"));
            Assert.Single(cluster.ListSecrets().Where(item => item.Name == "secret_data"));

            Assert.Single(cluster.ListConfigs().Where(item => item.Name == "config_text"));
            Assert.Single(cluster.ListConfigs().Where(item => item.Name == "config_data"));

            Assert.Single(cluster.ListServices().Where(item => item.Name == "test-service"));

            var stack = cluster.ListStacks().SingleOrDefault(item => item.Name == "test-stack");

            Assert.NotNull(stack);
            Assert.Equal(1, stack.ServiceCount);
            Assert.Single(cluster.ListServices().Where(item => item.Name.Equals("test-stack_sleeper")));
        }
    }
}
