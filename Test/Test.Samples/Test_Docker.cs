//-----------------------------------------------------------------------------
// FILE:	    Test_Docker.cs
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
using Neon.Xunit;
using Neon.Xunit.Couchbase;
using Neon.Xunit.Hive;

using Xunit;

namespace TestSamples
{
    // This sample demonstrates how to use the [DockerFixture] to implement
    // more complex test scenarios that deploy containers, services, databases,
    // and manage Docker swarm state.

    public class Test_Docker : IClassFixture<DockerFixture>
    {
        private DockerFixture       fixture;
        private HostsFixture        hosts;
        private CouchbaseFixture    couchbase;
        private NeonBucket          bucket;         

        public Test_Docker(DockerFixture fixture)
        {
            this.fixture = fixture;

            var reset = fixture.Initialize(
                () =>
                {
                    // We're going to add a [HostsFixture] so tests can modify
                    // the local [hosts] file to customize DNS lookups.  Note
                    // that subfixtures are identified by name and can be
                    // retrieved later using a fixture indexer.

                    fixture.AddFixture("hosts", new HostsFixture());

                    // Add a Couchbase instance to the test.

                    fixture.AddFixture("couchbase", new CouchbaseFixture(), subFixture => subFixture.StartInAction());
                });

            // Fetch the hosts fixture so it'll be easy to access from
            // the tests.

            hosts     = (HostsFixture)fixture["hosts"];
            couchbase = (CouchbaseFixture)fixture["couchbase"];
            bucket    = couchbase.Bucket;

            if (!reset)
            {
                // Reset the fixture state if the [Initialize()]
                // method hasn't already done so.

                hosts.Reset();
                fixture.Reset();
                couchbase.Clear();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public void SimpleContainer()
        {
            // Confirm that Docker starts out with no running containers,
            // besides the one for the Couchbase fixture.

            Assert.Empty(fixture.ListContainers().Where(c => c.Name != couchbase.ContainerName));

            // Spin up a sleeping container and verify that it's running.

            fixture.RunContainer("sleeping-container", "nhive/test");
            Assert.Single(fixture.ListContainers().Where(s => s.Name == "sleeping-container"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public void SimpleService()
        {
            // Confirm that Docker starts out with no running services.

            Assert.Empty(fixture.ListServices());

            // Spin up a sleeping service and verify that it's running.

            fixture.CreateService("sleeping-service", "nhive/test");
            Assert.Single(fixture.ListServices().Where(s => s.Name == "sleeping-service"));
        }


        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public void ManageSecrets()
        {
            // We should start out with no swarm secrets.

            Assert.Empty(fixture.ListSecrets());

            // Test adding and removing a secret.

            fixture.CreateSecret("my-secret", "Don't tell anyone!");
            Assert.Single(fixture.ListSecrets());
            Assert.Equal("my-secret", fixture.ListSecrets().First().Name);

            fixture.RemoveSecret("my-secret");
            Assert.Empty(fixture.ListSecrets());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public void ManageConfigs()
        {
            // We should start out with no swarm configs.

            Assert.Empty(fixture.ListConfigs());

            // Test adding and removing a secret.

            fixture.CreateConfig("my-config", "my settings");
            Assert.Single(fixture.ListConfigs());
            Assert.Equal("my-config", fixture.ListConfigs().First().Name);

            fixture.RemoveConfig("my-config");
            Assert.Empty(fixture.ListConfigs());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.Sample)]
        public void ManageNetworks()
        {
            // We should start out with no swarm networks.

            Assert.Empty(fixture.ListNetworks());

            // Test adding and removing a network.

            fixture.CreateNetwork("my-network");
            Assert.Single(fixture.ListNetworks());
            Assert.Equal("my-network", fixture.ListNetworks().First().Name);

            fixture.RemoveNetwork("my-network");
            Assert.Empty(fixture.ListNetworks());
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

            Assert.Empty(fixture.ListStacks());
            Assert.Empty(fixture.ListServices());

            // Use the [HostsFixture] to initialize a couple DNS entries and then verify that these work.

            hosts.AddHostAddress("test-foo.com", "127.0.0.1", deferCommit: true);
            hosts.AddHostAddress("test-bar.com", "127.0.0.1", deferCommit: true);
            hosts.Commit();

            Assert.Equal(new IPAddress[] { IPAddress.Parse("127.0.0.1") }, Dns.GetHostAddresses("test-foo.com"));
            Assert.Equal(new IPAddress[] { IPAddress.Parse("127.0.0.1") }, Dns.GetHostAddresses("test-bar.com"));

            // Spin up a couple of NodeJS services configuring them to return
            // different text using the OUTPUT environment variable.

            var fooCompose =
@"version: '3'

services:
  web:
    image: nhive/node
    ports:
      - ""8080:80""
    environment:
      - ""OUTPUT=FOO""
";
            fixture.DeployStack("foo-stack", fooCompose);

            var barCompose =
@"version: '3'

services:
  web:
    image: nhive/node
    ports:
      - ""8081:80""
    environment:
      - ""OUTPUT=BAR""
";
            fixture.DeployStack("bar-stack", barCompose);

            // Verify that each of the services are returning the expected output.

            using (var client = new HttpClient())
            {
                Assert.Equal("FOO", client.GetStringAsync("http://test-foo.com:8080").Result.Trim());
                Assert.Equal("BAR", client.GetStringAsync("http://test-bar.com:8081").Result.Trim());
            }

            // Do some Couchbase operations to prove that we can.

            bucket.UpsertSafeAsync("one", "1").Wait();
            bucket.UpsertSafeAsync("two", "2").Wait();

            Assert.Equal("1", await bucket.GetSafeAsync<string>("one"));
            Assert.Equal("2", await bucket.GetSafeAsync<string>("two"));

            // Remove one of the stacks and verify.

            fixture.RemoveStack("foo-stack");
            Assert.Empty(fixture.ListStacks().Where(s => s.Name == "foo-stack"));
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

            Assert.Empty(fixture.ListStacks());
            Assert.Empty(fixture.ListStacks());

            // Use the [HostsFixture] to initialize a couple DNS entries and then verify that these work.

            hosts.AddHostAddress("test-foo.com", "127.0.0.1", deferCommit: true);
            hosts.AddHostAddress("test-bar.com", "127.0.0.1", deferCommit: true);
            hosts.Commit();

            Assert.Equal(new IPAddress[] { IPAddress.Parse("127.0.0.1") }, Dns.GetHostAddresses("test-foo.com"));
            Assert.Equal(new IPAddress[] { IPAddress.Parse("127.0.0.1") }, Dns.GetHostAddresses("test-bar.com"));

            // Spin up a couple of NodeJS as stacks configuring them to return
            // different text using the OUTPUT environment variable.

            fixture.CreateService("foo", "nhive/node", dockerArgs: new string[] { "--publish", "8080:80" }, env: new string[] { "OUTPUT=FOO" });
            fixture.CreateService("bar", "nhive/node", dockerArgs: new string[] { "--publish", "8081:80" }, env: new string[] { "OUTPUT=BAR" });

            // Verify that each of the services are returning the expected output.

            using (var client = new HttpClient())
            {
                Assert.Equal("FOO", client.GetStringAsync("http://test-foo.com:8080").Result.Trim());
                Assert.Equal("BAR", client.GetStringAsync("http://test-bar.com:8081").Result.Trim());
            }

            // Do some Couchbase operations to prove that we can.

            bucket.UpsertSafeAsync("one", "1").Wait();
            bucket.UpsertSafeAsync("two", "2").Wait();

            Assert.Equal("1", await bucket.GetSafeAsync<string>("one"));
            Assert.Equal("2", await bucket.GetSafeAsync<string>("two"));

            // Remove one of the services and verify.

            fixture.RemoveStack("foo-service");
            Assert.Empty(fixture.ListServices().Where(s => s.Name == "foo-service"));
        }
    }
}
