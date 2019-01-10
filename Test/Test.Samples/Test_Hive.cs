//-----------------------------------------------------------------------------
// FILE:	    Test_Hive.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;
using Neon.Xunit;
using Neon.Xunit.Hive;

using Xunit;

namespace TestSamples
{
    // This sample demonstrates how to use the [HiveFixture] to execute
    // unit tests against a real neonHIVE.
    //
    // Prerequisites:
    //
    //  * Deployed neonHIVE cluster
    //
    //  * Login for the hive imported for the current user
    //
    //  * NEON_TEST_HIVE environment variable set to the
    //    login for the test hive, like:
    //
    //      NEON_TEST_HIVE=root@myhive

    public class Test_Hive : IClassFixture<HiveFixture>
    {
        private HiveFixture     hiveFixture;
        private ClusterProxy       hive;
        private HostsFixture    hosts;

        public Test_Hive(HiveFixture fixture)
        {
            if (!fixture.LoginAndInitialize(action:
                () =>
                {
                    // This adds a [HostsFixture] to the [HiveFixture] (which inherits
                    // from [TestFixtureSet]).  We'll name the HostFixture so we can use
                    // it to setup local DNS entries for the tests.

                    fixture.AddFixture("hosts", hosts = new HostsFixture());
                }))
            {
                // This call ensures that the hive is reset to a
                // pristine state before each test is invoked.

                fixture.Reset();

                // Retrieve the hosts fixture and reset it.

                hosts = (HostsFixture)fixture["hosts"];
                hosts.Reset();
            }

            this.hiveFixture = fixture;
            this.hive = fixture.Hive;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Test()
        {
            // Deploy a couple of simple NodeJS based services, one listening on 
            // port 8080 and the other on 8081.  We're also going to use the
            // [HostsFixture] to map a couple of DNS names to a hive manager
            // address and then use these to query the services.

            // Confirm that the hive starts out with no running stacks or services.

            Assert.Empty(hiveFixture.ListStacks());
            Assert.Empty(hiveFixture.ListServices());

            // Add the local DNS entries for the services we'll deploy.  We're
            // publishing these on host ports, so we'll map the DNS entries to
            // the local loopback address.

            var managerAddress = hive.FirstManager.PrivateAddress.ToString();

            hosts.AddHostAddress("foo.com", managerAddress, deferCommit: true);
            hosts.AddHostAddress("bar.com", managerAddress, deferCommit: true);
            hosts.Commit();

            // Spin up a couple of NodeJS as stacks configuring them to return
            // different text using the OUTPUT environment variable.

            hiveFixture.CreateService("foo", "nhive/node", dockerArgs: new string[] { "--publish", "8080:80" }, env: new string[] { "OUTPUT=FOO" });
            hiveFixture.CreateService("bar", "nhive/node", dockerArgs: new string[] { "--publish", "8081:80" }, env: new string[] { "OUTPUT=BAR" });

            // Verify that each of the services are returning the expected output.

            using (var client = new HttpClient())
            {
                Assert.Equal("FOO", client.GetStringAsync("http://foo.com:8080").Result.Trim());
                Assert.Equal("BAR", client.GetStringAsync("http://bar.com:8081").Result.Trim());
            }

            // Remove one of the services and verify.

            hiveFixture.RemoveStack("foo-service");
            Assert.Empty(hiveFixture.ListServices().Where(s => s.Name == "foo-service"));
        }
    }
}
