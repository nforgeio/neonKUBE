//-----------------------------------------------------------------------------
// FILE:	    Test_HostsFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestXunit
{
    public class Test_HostsFixture : IClassFixture<HostsFixture>
    {
        private static readonly string HostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "system32", "drivers", "etc", "hosts");

        private HostsFixture fixture;

        public Test_HostsFixture(HostsFixture fixture)
        {
            this.fixture = fixture;

            fixture.Start(
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

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Reset()
        {
            // Verify that we can reset the hosts.

            fixture.Reset();

            Assert.NotEqual(new IPAddress[] { IPAddress.Parse("1.2.3.4") }, Dns.GetHostAddresses("www.foo.com"));
            Assert.NotEqual(new IPAddress[] { IPAddress.Parse("5.6.7.8") }, Dns.GetHostAddresses("www.bar.com"));
            Assert.NotEqual(new IPAddress[] { IPAddress.Parse("1.1.1.1") }, Dns.GetHostAddresses("www.foobar.com"));

            // Restore the hosts so that other tests will work.

            fixture.AddHostAddress("www.foo.com", "1.2.3.4", deferCommit: true);
            fixture.AddHostAddress("www.bar.com", "5.6.7.8", deferCommit: true);
            fixture.AddHostAddress("www.foobar.com", "1.1.1.1", deferCommit: true);
            fixture.Commit();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void NoDuplicates()
        {
            // Ensure that duplicate host/IP mappings are iognored and are 
            // not added to the fixture.

            try
            {
                fixture.Reset();
                fixture.AddHostAddress("www.foobar.com", "1.1.1.1");
                fixture.AddHostAddress("www.foobar.com", "1.1.1.1");

                Assert.Equal(new IPAddress[] { IPAddress.Parse("1.1.1.1") }, Dns.GetHostAddresses("www.foobar.com"));
            }
            finally
            {
                // Restore the hosts so the remaining tests won't be impacted.

                fixture.Reset();
                fixture.AddHostAddress("www.foo.com", "1.2.3.4", deferCommit: true);
                fixture.AddHostAddress("www.bar.com", "5.6.7.8", deferCommit: true);
                fixture.AddHostAddress("www.foobar.com", "1.1.1.1", deferCommit: true);
                fixture.Commit();
            }
        }
    }
}
