//-----------------------------------------------------------------------------
// FILE:	    Test_HiveLoadBalancer.cs
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

namespace TestHive
{
    public class Test_HiveLoadBalancer : IClassFixture<HiveFixture>
    {
        private HiveFixture     hiveFixture;
        private HiveProxy       hive;

        public Test_HiveLoadBalancer(HiveFixture fixture)
        {
            if (!fixture.LoginAndInitialize())
            {
                fixture.Reset();
            }

            this.hiveFixture = fixture;
            this.hive = fixture.Hive;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public void Basic()
        {
            // $todo(jeff.lill): We actually need add some load balancer rules and test those.

            // Deploy a couple of simple NodeJS based services, one listening on 
            // port 8080 and the other on 8081.  We're also going to use the
            // [HostsFixture] to map a couple of DNS names to a hive manager
            // address and then use these to verify that we can call the services.

            // Confirm that the hive starts out with no running stacks or services.

            Assert.Empty(hiveFixture.ListStacks());
            Assert.Empty(hiveFixture.ListServices());

            // Spin up a couple of NodeJS as stacks configuring them to return
            // different text using the OUTPUT environment variable.

            hiveFixture.CreateService("foo", "nhive/node", dockerArgs: new string[] { "--publish", "8080:80" }, env: new string[] { "OUTPUT=FOO" });
            hiveFixture.CreateService("bar", "nhive/node", dockerArgs: new string[] { "--publish", "8081:80" }, env: new string[] { "OUTPUT=BAR" });

            // Add the temporary host records.

            hiveFixture.Hosts.AddHostAddress("foo.com", hiveFixture.Hive.FirstManager.PrivateAddress.ToString(), deferCommit: true);
            hiveFixture.Hosts.AddHostAddress("bar.com", hiveFixture.Hive.FirstManager.PrivateAddress.ToString(), deferCommit: false);

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
