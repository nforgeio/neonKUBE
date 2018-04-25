//-----------------------------------------------------------------------------
// FILE:	    Test_DockerResetInConstructor.cs
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

namespace TestDocker
{
    // This example ensures that the local Docker daemon state is reset
    // in the constructor before each test method is invoked.

    public class Test_DockerResetInConstructor : IClassFixture<DockerFixture>
    {
        private DockerFixture   docker;
        private HostsFixture    hosts;

        public Test_DockerResetInConstructor(DockerFixture docker)
        {
            this.docker = docker;

            if (docker.Initialize())
            {
                // We're going to add a [HostsFixture] so tests can modify
                // the local [hosts] file to customize DNS lookups.  These
                // subfixtures are identified by name.

                docker.AddFixture("hosts", new HostsFixture());

                // This call resets the local Docker daemon state if the
                // Initialize() call above didn't already do this.  This
                // saves doing an extra reset for the first executed test.
                //
                // This call ensures that Docker state is reset before
                // the test runner invokes each test.

                docker.Reset();
            }

            // Fetch the hosts fixture so the test methods can use it.

            hosts = (HostsFixture)docker["hosts"];
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public void SimpleContainer()
        {
            // Confirm that Docker starts out with no running contsainers.

            Assert.Empty(docker.ListContainers());

            // Spin up a do-nothing service and verify that it's running.

            docker.RunContainer("nothing-container", "alpine", containerArgs: new string[] { "sleep", "10000000" });
            Assert.Single(docker.ListContainers().Where(s => s.Name == "nothing-container"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public void SimpleService()
        {
            // Confirm that Docker starts out with no running services.

            Assert.Empty(docker.ListServices());

            // Spin up a do-nothing service and verify that it's running.

            docker.CreateService("nothing-service", "alpine", serviceArgs: new string[] { "sleep", "10000000" });
            Assert.Single(docker.ListServices().Where(s => s.Name == "nothing-service"));
        }
    }
}
