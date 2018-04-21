//-----------------------------------------------------------------------------
// FILE:	    Test_DockerSwarmFixture.cs
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

namespace TestCouchbase
{
    public class Test_DockerSwarmFixture : IClassFixture<DockerSwarmFixture>
    {
        private DockerSwarmFixture fixture;

        public Test_DockerSwarmFixture(DockerSwarmFixture fixture)
        {
            this.fixture = fixture;

            fixture.Initialize(
                () =>
                {
                    fixture.CreateSecret("secret_text", "hello");
                    fixture.CreateSecret("secret_data", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                    fixture.CreateConfig("config_text", "hello");
                    fixture.CreateConfig("config_data", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                    fixture.StartService("test-service", "alpine", serviceArgs: new string[] { "sleep", "1000000" });

                    var composeText =
@"version: '3'

services:
  sleeper:
    image: alpine
    command: sleep 1000000
";
                    fixture.DeployStack("test-stack", composeText);
                    fixture.WaitForStackTask("test-stack_sleeper");
                });
        }

        [Fact]
        public void Verify()
        {
            // Verify that the secrets, configs, service, and stack were created.

            var result = NeonHelper.ExecuteCaptureStreams("docker", new object[] { "secret", "ls" });

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("secret_text", result.OutputText);
            Assert.Contains("secret_data", result.OutputText);

            result = NeonHelper.ExecuteCaptureStreams("docker", new object[] { "config", "ls" });

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("config_text", result.OutputText);
            Assert.Contains("config_data", result.OutputText);

            result = NeonHelper.ExecuteCaptureStreams("docker", new object[] { "service", "ls" });

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("test-service", result.OutputText);

            result = NeonHelper.ExecuteCaptureStreams("docker", new object[] { "stack", "ls" });

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("test-stack", result.OutputText);

            result = NeonHelper.ExecuteCaptureStreams("docker", new object[] { "ps" });

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("test-stack_sleeper", result.OutputText);
        }
    }
}
