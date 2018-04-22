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

namespace TestCouchbase
{
    public class Test_HostsFixture : IClassFixture<HostsFixture>
    {
        private static readonly string HostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "system32", "drivers", "etc", "hosts");

        public Test_HostsFixture(HostsFixture fixture)
        {
            fixture.Initialize(
                () =>
                {
                    // Add some entries using deferred commit.

                    fixture.AddHostAddress("www.foo.com", "1.2.3.4", deferCommit: true);
                    fixture.AddHostAddress("www.bar.com", "5.6.7.8", deferCommit: true);
                    fixture.Commit();

                    // Add an entry using auto commit.

                    fixture.AddHostAddress("www.foobar.com", "1.1.1.1");
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
    }
}
