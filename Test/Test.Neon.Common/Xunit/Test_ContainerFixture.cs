//-----------------------------------------------------------------------------
// FILE:	    Test_ContainerFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    /// <summary>
    // Verify that we can launch a Docker container fixture during tests.
    /// </summary>
    public class Test_ContainerFixture : IClassFixture<ContainerFixture>
    {
        private ContainerFixture fixture;

        public Test_ContainerFixture(ContainerFixture fixture)
        {
            this.fixture = fixture;

            fixture.Initialize(
                () =>
                {
                    // Have the Docker container fixture launch a test image that
                    // sleeps for a (long) time.

                    fixture.RunContainer(
                        name: "test",
                        image: "nhive/test",
                        dockerArgs: new string[] { "--detach" });
                });
        }

        /// <summary>
        /// Verify that the container is running.
        /// </summary>
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Verify()
        {
            // We're going to use [docker ps --format "{{.ID}}"] to list the current
            // containers by ID.  This returns the short form of the container ID.
            // We'll verify that one of the IDs returned matches the container
            // launched by the fixture.

            var result = NeonHelper.ExecuteCapture("docker", new object[] { "ps", "--format", "{{.ID}}" });

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
