//-----------------------------------------------------------------------------
// FILE:	    Test_DockerResetInConstructor.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Retry;

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
                // the local [hosts] file to customize DNS lookups.  Note
                // that subfixtures are identified by name and can be
                // retrieved later using a fixture indexer.

                docker.AddFixture("hosts", new HostsFixture());
            }
            else
            {
                // Reset the fixture state.  We could have done this down
                // below (outside of the IF statement), but doing this here
                // will be a bit faster for the first test method invoked,
                // because [Initialize()] already resets the fixture 
                // the first time it's called for the test class.

                docker.Reset();
            }

            // Fetch the hosts fixture so it'll be easy to access from
            // the tests.

            hosts = (HostsFixture)docker["hosts"];
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public void SimpleContainer()
        {
            // Confirm that Docker starts out with no running contsainers.

            Assert.Empty(docker.ListContainers());

            // Spin up a sleeping container and verify that it's running.

            docker.RunContainer("sleeping-container", "alpine", containerArgs: new string[] { "sleep", "10000000" });
            Assert.Single(docker.ListContainers().Where(s => s.Name == "sleeping-container"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public void SimpleService()
        {
            // Confirm that Docker starts out with no running services.

            Assert.Empty(docker.ListServices());

            // Spin up a sleeping service and verify that it's running.

            docker.CreateService("sleeping-service", "alpine", serviceArgs: new string[] { "sleep", "10000000" });
            Assert.Single(docker.ListServices().Where(s => s.Name == "sleeping-service"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public void HostsAndServices()
        {
            // Deploy a couple of simple NodeJS based services, one listening on 
            // port 8080 and the other on 8081.  We're also going to use the
            // [HostsFixture] to map a couple of DNS names to the local loopback
            // address and then use these to query the services.

            // Confirm that Docker starts out with no running services.

            Assert.Empty(docker.ListServices());

            // Spinup a couple of NodeJS services configuring them to return
            // different string using the OUTPUT environment variable.

            docker.CreateService("foo", "neoncluster/node", dockerArgs: new string[] { "--publish", "8080:80" }, env: new string[] { "OUTPUT=FOO" });
            docker.CreateService("bar", "neoncluster/node", dockerArgs: new string[] { "--publish", "8081:80" }, env: new string[] { "OUTPUT=BAR" });

            using (var client = new HttpClient())
            {
                // Verify that each of the services are returning the expected output.

                Assert.Equal("FOO", client.GetStringAsync("http://foo.com:8080").Result);
                Assert.Equal("BAR", client.GetStringAsync("http://bar.com:8081").Result);
            }
        }
    }
}
