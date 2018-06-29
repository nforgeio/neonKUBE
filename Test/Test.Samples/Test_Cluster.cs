//-----------------------------------------------------------------------------
// FILE:	    Test_Cluster.cs
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
using Neon.Hive;
using Neon.Xunit;
using Neon.Xunit.Hive;

using Xunit;

namespace TestSamples
{
    // This sample demonstrates how to use the [ClusterFixture] to execute
    // unit tests against a real neonHIVE.
    //
    // Prerequisites:
    //
    //  * Deployed test cluster
    //
    //  * Login for the cluster imported for the current user
    //
    //  * NEON_TEST_CLUSTER environment variable set to the
    //    login for the test cluster, like:
    //
    //      NEON_TEST_CLUSTER=root@mycluster

    public class Test_Cluster : IClassFixture<ClusterFixture>
    {
        private ClusterFixture  fixture;
        private ClusterProxy    cluster;
        private HostsFixture    hosts;

        public Test_Cluster(ClusterFixture fixture)
        {
            if (!fixture.LoginAndInitialize(action:
                () =>
                {
                    // This adds a [HostsFixture] to the [ClusterFixture] (which inherits
                    // from [TestFixtureSet]).  We'll name the HostFixture so we can use
                    // it to setup local DNS entries for the tests.

                    fixture.AddFixture("hosts", hosts = new HostsFixture());
                }))
            {
                // This call ensures that the cluster is reset to a
                // pristine state before each test is invoked.

                fixture.Reset();

                // Retrieve the hosts fixture and reset it.

                hosts = (HostsFixture)fixture["hosts"];
                hosts.Reset();
            }

            this.fixture = fixture;
            this.cluster = fixture.Cluster;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Test()
        {
            // Deploy a couple of simple NodeJS based services, one listening on 
            // port 8080 and the other on 8081.  We're also going to use the
            // [HostsFixture] to map a couple of DNS names to the local loopback
            // address and then use these to query the services.

            // Confirm that the cluster starts out with no running stacks or services.

            Assert.Empty(fixture.ListStacks());
            Assert.Empty(fixture.ListServices());

            // Add the local DNS entries for the services we'll deploy.  We're
            // publishing these on host ports, so we'll use map the DNS entries
            // to the local loopback address.

            hosts.AddHostAddress("foo.com", "127.0.0.1", deferCommit: true);
            hosts.AddHostAddress("bar.com", "127.0.0.1", deferCommit: true);
            hosts.Commit();

            // Spin up a couple of NodeJS as stacks configuring them to return
            // different text using the OUTPUT environment variable.

            fixture.CreateService("foo", "neoncluster/node", dockerArgs: new string[] { "--publish", "8080:80" }, env: new string[] { "OUTPUT=FOO" });
            fixture.CreateService("bar", "neoncluster/node", dockerArgs: new string[] { "--publish", "8081:80" }, env: new string[] { "OUTPUT=BAR" });

            // Verify that each of the services are returning the expected output.

            using (var client = new HttpClient())
            {
                Assert.Equal("FOO", client.GetStringAsync("http://foo.com:8080").Result.Trim());
                Assert.Equal("BAR", client.GetStringAsync("http://bar.com:8081").Result.Trim());
            }

            // Remove one of the services and verify.

            fixture.RemoveStack("foo-service");
            Assert.Empty(fixture.ListServices().Where(s => s.Name == "foo-service"));
        }
    }
}
