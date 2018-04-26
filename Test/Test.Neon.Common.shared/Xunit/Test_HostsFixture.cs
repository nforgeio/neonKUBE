//-----------------------------------------------------------------------------
// FILE:	    Test_HostsFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

using Xunit;
using Xunit.Neon;

namespace TestCommon
{
    public class Test_HostsFixture : IClassFixture<HostsFixture>
    {
        private static readonly string HostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "system32", "drivers", "etc", "hosts");

        private HostsFixture hosts;

        public Test_HostsFixture(HostsFixture hosts)
        {
            this.hosts = hosts;

            hosts.Initialize(
                () =>
                {
                    // Add some entries using deferred commit.

                    hosts.AddHostAddress("www.foo.com", "1.2.3.4", deferCommit: true);
                    hosts.AddHostAddress("www.bar.com", "5.6.7.8", deferCommit: true);
                    hosts.Commit();

                    // Add an entry using auto commit.

                    hosts.AddHostAddress("www.foobar.com", "1.1.1.1");
                });
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Lookup()
        {
            Assert.Equal(new IPAddress[] { IPAddress.Parse("1.2.3.4") }, Dns.GetHostAddresses("www.foo.com"));
            Assert.Equal(new IPAddress[] { IPAddress.Parse("5.6.7.8") }, Dns.GetHostAddresses("www.bar.com"));
            Assert.Equal(new IPAddress[] { IPAddress.Parse("1.1.1.1") }, Dns.GetHostAddresses("www.foobar.com"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Reset()
        {
            // Verify that we can reset the hosts.

            hosts.Reset();

            Assert.NotEqual(new IPAddress[] { IPAddress.Parse("1.2.3.4") }, Dns.GetHostAddresses("www.foo.com"));
            Assert.NotEqual(new IPAddress[] { IPAddress.Parse("5.6.7.8") }, Dns.GetHostAddresses("www.bar.com"));
            Assert.NotEqual(new IPAddress[] { IPAddress.Parse("1.1.1.1") }, Dns.GetHostAddresses("www.foobar.com"));

            // Restore the hosts so that other tests will work.

            hosts.AddHostAddress("www.foo.com", "1.2.3.4", deferCommit: true);
            hosts.AddHostAddress("www.bar.com", "5.6.7.8", deferCommit: true);
            hosts.AddHostAddress("www.foobar.com", "1.1.1.1", deferCommit: true);
            hosts.Commit();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void NoDuplicates()
        {
            // Ensure that duplicate host/IP mappings are iognored and are 
            // not added to the fixture.

            try
            {
                hosts.Reset();
                hosts.AddHostAddress("www.foobar.com", "1.1.1.1");
                hosts.AddHostAddress("www.foobar.com", "1.1.1.1");

                Assert.Equal(new IPAddress[] { IPAddress.Parse("1.1.1.1") }, Dns.GetHostAddresses("www.foobar.com"));
            }
            finally
            {
                // Restore the hosts so the remaining tests won't be impacted.

                hosts.Reset();
                hosts.AddHostAddress("www.foo.com", "1.2.3.4", deferCommit: true);
                hosts.AddHostAddress("www.bar.com", "5.6.7.8", deferCommit: true);
                hosts.AddHostAddress("www.foobar.com", "1.1.1.1", deferCommit: true);
                hosts.Commit();
            }
        }
    }
}
