//-----------------------------------------------------------------------------
// FILE:	    Test_Dockers.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Couchbase;

using Neon.Common;
using Neon.Retry;

using Xunit;
using Xunit.Neon;

namespace TestSamples
{
    // This sample demonstrates how to use the [DockerFixture] to implement
    // more complex test scenarios that deploy containers, services, databases,
    // and manage Docker swarm state.

    public class Test_Dockers : IClassFixture<DockerFixture>
    {
        private DockerFixture       docker;
        private HostsFixture        hosts;
        private CouchbaseFixture    couchbase;
        private NeonBucket          bucket;         

        public Test_Dockers(DockerFixture docker)
        {
            this.docker = docker;

            var reset = docker.Initialize(
                () =>
                {
                    // We're going to add a [HostsFixture] so tests can modify
                    // the local [hosts] file to customize DNS lookups.  Note
                    // that subfixtures are identified by name and can be
                    // retrieved later using a fixture indexer.

                    docker.AddFixture("hosts", new HostsFixture());

                    // Add a Couchbase instance to the test.

                    docker.AddFixture("couchbase", new CouchbaseFixture(), subFixture => subFixture.Start());
                });

            // Fetch the hosts fixture so it'll be easy to access from
            // the tests.

            hosts     = (HostsFixture)docker["hosts"];
            couchbase = (CouchbaseFixture)docker["couchbase"];
            bucket    = couchbase.Bucket;

            if (!reset)
            {
                // Reset the fixture state if the [Initialize()]
                // method hasn't already done so.

                hosts.Reset();
                docker.Reset();
                couchbase.Flush();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public void SimpleContainer()
        {
            // Confirm that Docker starts out with no running containers,
            // besides the one for the Couchbase fixture.

            Assert.Empty(docker.ListContainers().Where(c => c.Name != couchbase.ContainerName));

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
        public void ManageSecrets()
        {
            // We should start out with no swarm secrets.

            Assert.Empty(docker.ListSecrets());

            // Test adding and removing a secret.

            docker.CreateSecret("my-secret", "Don't tell anyone!");
            Assert.Single(docker.ListSecrets());
            Assert.Equal("my-secret", docker.ListSecrets().First().Name);

            docker.RemoveSecret("my-secret");
            Assert.Empty(docker.ListSecrets());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public void ManageConfigs()
        {
            // We should start out with no swarm configs.

            Assert.Empty(docker.ListConfigs());

            // Test adding and removing a secret.

            docker.CreateConfig("my-config", "my settings");
            Assert.Single(docker.ListConfigs());
            Assert.Equal("my-config", docker.ListConfigs().First().Name);

            docker.RemoveConfig("my-config");
            Assert.Empty(docker.ListConfigs());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public void ManageNetworks()
        {
            // We should start out with no swarm networks.

            Assert.Empty(docker.ListNetworks());

            // Test adding and removing a network.

            docker.CreateNetwork("my-network");
            Assert.Single(docker.ListNetworks());
            Assert.Equal("my-network", docker.ListNetworks().First().Name);

            docker.RemoveNetwork("my-network");
            Assert.Empty(docker.ListNetworks());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public async Task HostsDbStacks()
        {
            // Deploy a couple of simple NodeJS based services as stacks, one 
            // listening on  port 8080 and the other on 8081.  We're also going
            // to use the [HostsFixture] to map a couple of DNS names to the local 
            // loopback address and then use these to query the services and finally,
            // we're going to do some things to Couchbase.

            // Confirm that Docker starts out with no running stacks or services.

            Assert.Empty(docker.ListStacks());
            Assert.Empty(docker.ListServices());

            // Use the [HostsFixture] to initialize a couple DNS entries and then verify that these work.

            hosts.AddHostAddress("foo.com", "127.0.0.1", deferCommit: true);
            hosts.AddHostAddress("bar.com", "127.0.0.1", deferCommit: true);
            hosts.Commit();

            Assert.Equal(new IPAddress[] { IPAddress.Parse("127.0.0.1") }, Dns.GetHostAddresses("foo.com"));
            Assert.Equal(new IPAddress[] { IPAddress.Parse("127.0.0.1") }, Dns.GetHostAddresses("bar.com"));

            // Spin up a couple of NodeJS services configuring them to return
            // different text using the OUTPUT environment variable.

            var fooCompose =
@"version: '3'

services:
  web:
    image: neoncluster/node
    ports:
      - ""8080:80""
    environment:
      - ""OUTPUT=FOO""
";
            docker.DeployStack("foo-stack", fooCompose);

            var barCompose =
@"version: '3'

services:
  web:
    image: neoncluster/node
    ports:
      - ""8081:80""
    environment:
      - ""OUTPUT=BAR""
";
            docker.DeployStack("bar-stack", barCompose);

            // Verify that each of the services are returning the expected output.

            using (var client = new HttpClient())
            {
                Assert.Equal("FOO", client.GetStringAsync("http://foo.com:8080").Result.Trim());
                Assert.Equal("BAR", client.GetStringAsync("http://bar.com:8081").Result.Trim());
            }

            // Do some Couchbase operations to prove that we can.

            bucket.UpsertSafeAsync("one", "1").Wait();
            bucket.UpsertSafeAsync("two", "2").Wait();

            Assert.Equal("1", await bucket.GetSafeAsync<string>("one"));
            Assert.Equal("2", await bucket.GetSafeAsync<string>("two"));

            // Remove one of the stacks and verify.

            docker.RemoveStack("foo-stack");
            Assert.Empty(docker.ListStacks().Where(s => s.Name == "foo-stack"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public async Task HostsDbServices()
        {
            // Deploy a couple of simple NodeJS based services, one listening on 
            // port 8080 and the other on 8081.  We're also going to use the
            // [HostsFixture] to map a couple of DNS names to the local loopback
            // address and then use these to query the services and finally,
            // we're going to do some things to Couchbase.

            // Confirm that Docker starts out with no running stacks or services.

            Assert.Empty(docker.ListStacks());
            Assert.Empty(docker.ListStacks());

            // Use the [HostsFixture] to initialize a couple DNS entries and then verify that these work.

            hosts.AddHostAddress("foo.com", "127.0.0.1", deferCommit: true);
            hosts.AddHostAddress("bar.com", "127.0.0.1", deferCommit: true);
            hosts.Commit();

            Assert.Equal(new IPAddress[] { IPAddress.Parse("127.0.0.1") }, Dns.GetHostAddresses("foo.com"));
            Assert.Equal(new IPAddress[] { IPAddress.Parse("127.0.0.1") }, Dns.GetHostAddresses("bar.com"));

            // Spin up a couple of NodeJS as stacks configuring them to return
            // different text using the OUTPUT environment variable.

            docker.CreateService("foo", "neoncluster/node", dockerArgs: new string[] { "--publish", "8080:80" }, env: new string[] { "OUTPUT=FOO" });
            docker.CreateService("bar", "neoncluster/node", dockerArgs: new string[] { "--publish", "8081:80" }, env: new string[] { "OUTPUT=BAR" });

            // Verify that each of the services are returning the expected output.

            using (var client = new HttpClient())
            {
                Assert.Equal("FOO", client.GetStringAsync("http://foo.com:8080").Result.Trim());
                Assert.Equal("BAR", client.GetStringAsync("http://bar.com:8081").Result.Trim());
            }

            // Do some Couchbase operations to prove that we can.

            bucket.UpsertSafeAsync("one", "1").Wait();
            bucket.UpsertSafeAsync("two", "2").Wait();

            Assert.Equal("1", await bucket.GetSafeAsync<string>("one"));
            Assert.Equal("2", await bucket.GetSafeAsync<string>("two"));

            // Remove one of the service and verify.

            docker.RemoveStack("foo-service");
            Assert.Empty(docker.ListServices().Where(s => s.Name == "foo-service"));
        }
    }
}
