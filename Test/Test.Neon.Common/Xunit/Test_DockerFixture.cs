//-----------------------------------------------------------------------------
// FILE:	    Test_DockerFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using Neon.Xunit;
using System.Linq;
using Xunit;

namespace TestCommon
{
    public class Test_DockerFixture : IClassFixture<DockerFixture>
    {
        private DockerFixture fixture;

        public Test_DockerFixture(DockerFixture fixture)
        {
            this.fixture = fixture;

            if (!this.fixture.Initialize())
            {
                this.fixture.Reset();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Basics()
        {
            // Initialize

            this.fixture.CreateSecret("secret_text", "hello");
            this.fixture.CreateSecret("secret_data", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

            this.fixture.CreateConfig("config_text", "hello");
            this.fixture.CreateConfig("config_data", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

            this.fixture.CreateService("test-service", "nhive/test");

            var composeText =
@"version: '3'

services:
  sleeper:
    image: nhive/test
    deploy:
      replicas: 2
";
            this.fixture.DeployStack("test-stack", composeText);
            this.fixture.RunContainer("test-container", "nhive/test");
            this.fixture.CreateNetwork("test-network");

            // Verify that the secrets, configs, networks, container, service, and stack were created.

            Assert.Single(fixture.ListSecrets().Where(item => item.Name == "secret_text"));
            Assert.Single(fixture.ListSecrets().Where(item => item.Name == "secret_data"));

            Assert.Single(fixture.ListConfigs().Where(item => item.Name == "config_text"));
            Assert.Single(fixture.ListConfigs().Where(item => item.Name == "config_data"));

            Assert.Single(fixture.ListServices().Where(item => item.Name == "test-service"));

            Assert.Single(fixture.ListStacks().Where(item => item.Name == "test-stack"));
            Assert.Equal(1, fixture.ListStacks().First().ServiceCount);

            Assert.Single(fixture.ListContainers().Where(item => item.Name == "test-container"));
            Assert.Single(fixture.ListContainers().Where(item => item.Name.StartsWith("test-stack_sleeper.1.")));

            // Verify that restarting a service doesn't barf.

            fixture.RestartService("test-service");
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public void ClearVolumes()
        {
            //-----------------------------------------------------------------
            // Create a test volume on the hive node and then verify
            // that ClearVolumes() removes it.

            fixture.DockerExecute("docker volume create test-volume");
            fixture.ClearVolumes();

            var response = fixture.DockerExecute("volume ls --format \"{{.Name}}\"");

            Assert.Equal(0, response.ExitCode);
            Assert.DoesNotContain("test-volume", response.OutputText);
        }
    }
}
