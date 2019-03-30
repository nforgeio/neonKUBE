//-----------------------------------------------------------------------------
// FILE:	    Test_NetworkCidr.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Net;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_Pinger
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task Basic()
        {
            using (var pinger = new Pinger())
            {
                // The loopback address should always answer.

                Assert.Equal(IPStatus.Success, (await pinger.SendPingAsync("127.0.0.1")).Status);
                Assert.Equal(IPStatus.Success, (await pinger.SendPingAsync(IPAddress.Parse("127.0.0.1"))).Status);

                // FRAGILE:
                //
                // Verify that the ping times out for an IP address that's very unlikely to have
                // anything running on it (at least in our test/dev environments).

                var status = (await pinger.SendPingAsync(IPAddress.Parse("10.227.126.253"))).Status;

                Assert.True(status == IPStatus.TimedOut || status == IPStatus.DestinationNetworkUnreachable);

                // The [240.0.0.0/4] subnet is currently reserved and should not
                // not be routable (and probably never will be).

                await Assert.ThrowsAsync<PingException>(async () => await pinger.SendPingAsync("240.0.0.0"));
                await Assert.ThrowsAsync<PingException>(async () => await pinger.SendPingAsync(IPAddress.Parse("240.0.0.0")));
            }
        }
    }
}
