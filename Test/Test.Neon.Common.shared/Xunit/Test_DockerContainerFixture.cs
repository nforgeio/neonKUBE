//-----------------------------------------------------------------------------
// FILE:	    Test_DockerContainerFixture.cs
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
    /// <summary>
    // Verify that we can launch a Docker container fixture during tests.
    /// </summary>
    public class Test_DockerContainerFixture : IClassFixture<DockerContainerFixture>
    {
        private DockerContainerFixture fixture;

        public Test_DockerContainerFixture(DockerContainerFixture fixture)
        {
            this.fixture = fixture;

            fixture.Initialize(
                () =>
                {
                    // Have the Docker container fixture launch an Alpine image and
                    // sleep for a (long) time.

                    fixture.RunContainer(
                        name: "couchbase",
                        image: "alpine",
                        dockerArgs: new string[] { "--detach" },
                        containerArgs: new string[] { "sleep", "10000000" });
                });
        }

        /// <summary>
        /// Verify that the container is running.
        /// </summary>
        [Fact]
        public void Verify()
        {
            // We're going to use [docker ps --format "{{.ID}}"] to list the current
            // containers by ID.  This returns the short form of the container ID.
            // We'll verify that one of the IDs returned matches the container
            // launched by the fixture.

            var result = NeonHelper.ExecuteCaptureStreams("docker", new object[] { "ps", "--format", "{{.ID}}" });

            if (result.ExitCode != 0)
            {
                throw new Exception("[docker ps] failed.");
            }

            using (var reader = new StringReader(result.OutputText))
            {
                foreach (var line in reader.Lines())
                {
                    if (line.StartsWith(fixture.ContainerId))
                    {
                        return; // The container is running.
                    }
                }
            }

            throw new Exception($"Container [{fixture.ContainerId}] is not running.");
        }
    }
}
