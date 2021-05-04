//-----------------------------------------------------------------------------
// FILE:	    Test_NetworkCidr.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Net;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    [Trait(TestTrait.Area, TestArea.NeonCommon)]
    public class Test_NetworkCidr
    {
        [Fact]
        public void Parse()
        {
            var cidr = NetworkCidr.Parse("10.1.2.3/8");

            Assert.Equal(IPAddress.Parse("10.0.0.0"), cidr.Address);
            Assert.True(NetHelper.AddressEquals(IPAddress.Parse("255.0.0.0"), cidr.Mask));
            Assert.Equal(8, cidr.PrefixLength);

            cidr = NetworkCidr.Parse("10.1.2.3/16");

            Assert.Equal(IPAddress.Parse("10.1.0.0"), cidr.Address);
            Assert.True(NetHelper.AddressEquals(IPAddress.Parse("255.255.0.0"), cidr.Mask));
            Assert.Equal(16, cidr.PrefixLength);

            cidr = NetworkCidr.Parse("10.1.2.3/24");

            Assert.Equal(IPAddress.Parse("10.1.2.0"), cidr.Address);
            Assert.True(NetHelper.AddressEquals(IPAddress.Parse("255.255.255.0"), cidr.Mask));
            Assert.Equal(24, cidr.PrefixLength);
        }

        [Fact]
        public void ParseErrors()
        {
            Assert.Throws<ArgumentNullException>(() => NetworkCidr.Parse(null));
            Assert.Throws<ArgumentNullException>(() => NetworkCidr.Parse(string.Empty));
            Assert.Throws<ArgumentException>(() => NetworkCidr.Parse("10.0.0.1"));
            Assert.Throws<ArgumentException>(() => NetworkCidr.Parse("/6"));
            Assert.Throws<ArgumentException>(() => NetworkCidr.Parse("10.0.0.1/-1"));
            Assert.Throws<ArgumentException>(() => NetworkCidr.Parse("10.0.0.1/33"));
            Assert.Throws<ArgumentException>(() => NetworkCidr.Parse("10.A.0.1/8"));
        }

        [Fact]
        public void TryParse()
        {
            NetworkCidr cidr;

            Assert.True(NetworkCidr.TryParse("10.1.2.3/8", out cidr));

            Assert.Equal(IPAddress.Parse("10.0.0.0"), cidr.Address);
            Assert.Equal(8, cidr.PrefixLength);
            Assert.Equal(IPAddress.Parse("255.0.0.0"), cidr.Mask);

            Assert.True(NetworkCidr.TryParse("10.1.2.3/16", out cidr));

            Assert.Equal(IPAddress.Parse("10.1.0.0"), cidr.Address);
            Assert.Equal(16, cidr.PrefixLength);
            Assert.Equal(IPAddress.Parse("255.255.0.0"), cidr.Mask);

            Assert.True(NetworkCidr.TryParse("10.1.2.3/24", out cidr));

            Assert.Equal(IPAddress.Parse("10.1.2.0"), cidr.Address);
            Assert.Equal(24, cidr.PrefixLength);
            Assert.Equal(IPAddress.Parse("255.255.255.0"), cidr.Mask);
        }

        [Fact]
        public void TryParseErrors()
        {
            NetworkCidr cidr;

            Assert.False(NetworkCidr.TryParse(null, out cidr));
            Assert.False(NetworkCidr.TryParse(string.Empty, out cidr));
            Assert.False(NetworkCidr.TryParse("10.0.0.1", out cidr));
            Assert.False(NetworkCidr.TryParse("/6", out cidr));
            Assert.False(NetworkCidr.TryParse("10.0.0.1/-1", out cidr));
            Assert.False(NetworkCidr.TryParse("10.0.0.1/33", out cidr));
            Assert.False(NetworkCidr.TryParse("10.A.0.1/8", out cidr));
        }

        [Fact]
        public void Init()
        {
            var cidr = new NetworkCidr(IPAddress.Parse("10.1.2.3"), 8);

            Assert.Equal(IPAddress.Parse("10.0.0.0"), cidr.Address);
            Assert.Equal(8, cidr.PrefixLength);
            Assert.Equal(IPAddress.Parse("255.0.0.0"), cidr.Mask);

            cidr = new NetworkCidr(IPAddress.Parse("10.1.2.3"), 16);

            Assert.Equal(IPAddress.Parse("10.1.0.0"), cidr.Address);
            Assert.Equal(16, cidr.PrefixLength);
            Assert.Equal(IPAddress.Parse("255.255.0.0"), cidr.Mask);

            cidr = new NetworkCidr(IPAddress.Parse("10.1.2.3"), 24);

            Assert.Equal(IPAddress.Parse("10.1.2.0"), cidr.Address);
            Assert.Equal(24, cidr.PrefixLength);
            Assert.Equal(IPAddress.Parse("255.255.255.0"), cidr.Mask);
        }

        [Fact]
        public void InitErrors()
        {
            Assert.Throws<ArgumentNullException>(() => new NetworkCidr(null, 8));
            Assert.Throws<ArgumentException>(() => new NetworkCidr(IPAddress.Parse("255.255.0.0"), -1));
            Assert.Throws<ArgumentException>(() => new NetworkCidr(IPAddress.Parse("255.255.0.0"), 33));
        }

        [Fact]
        public void Compare()
        {
            Assert.True(NetworkCidr.Parse("10.0.0.1/8") == NetworkCidr.Parse("10.0.0.1/8"));
            Assert.True(NetworkCidr.Parse("10.0.0.1/8").Equals(NetworkCidr.Parse("10.0.0.1/8")));
            Assert.True(NetworkCidr.Parse("10.0.0.1/8") == NetworkCidr.Parse("10.0.2.1/8"));
            Assert.False(NetworkCidr.Parse("10.0.0.1/8") == NetworkCidr.Parse("10.0.0.1/16"));

            Assert.False(NetworkCidr.Parse("10.0.0.1/8") != NetworkCidr.Parse("10.0.0.1/8"));
            Assert.True(NetworkCidr.Parse("10.0.0.1/8") == NetworkCidr.Parse("10.0.2.1/8"));
            Assert.True(NetworkCidr.Parse("10.0.0.1/8") != NetworkCidr.Parse("10.0.0.1/16"));

            Assert.Equal(NetworkCidr.Parse("10.0.0.1/8").GetHashCode(), NetworkCidr.Parse("10.0.0.1/8").GetHashCode());
            Assert.NotEqual(NetworkCidr.Parse("10.0.0.1/8").GetHashCode(), NetworkCidr.Parse("10.0.0.1/16").GetHashCode());
            Assert.Equal(NetworkCidr.Parse("10.0.0.1/8").GetHashCode(), NetworkCidr.Parse("10.0.0.2/8").GetHashCode());

            Assert.True((NetworkCidr)null == (NetworkCidr)null);
            Assert.False((NetworkCidr)null != (NetworkCidr)null);

            Assert.False(NetworkCidr.Parse("10.0.0.1/8") == (NetworkCidr)null);
            Assert.False((NetworkCidr)null == NetworkCidr.Parse("10.0.0.1/8"));

            Assert.True(NetworkCidr.Parse("10.0.0.1/8") != (NetworkCidr)null);
            Assert.True((NetworkCidr)null != NetworkCidr.Parse("10.0.0.1/8"));
        }

        [Fact]
        public void ContainsIP()
        {
            Assert.True(NetworkCidr.Parse("10.0.0.0/24").Contains(IPAddress.Parse("10.0.0.0")));
            Assert.True(NetworkCidr.Parse("10.0.0.0/24").Contains(IPAddress.Parse("10.0.0.1")));
            Assert.True(NetworkCidr.Parse("10.0.0.0/24").Contains(IPAddress.Parse("10.0.0.255")));
            Assert.True(NetworkCidr.Parse("10.0.1.0/24").Contains(IPAddress.Parse("10.0.1.1")));

            Assert.False(NetworkCidr.Parse("10.0.0.0/24").Contains(IPAddress.Parse("10.0.1.0")));
            Assert.False(NetworkCidr.Parse("10.0.0.0/24").Contains(IPAddress.Parse("10.0.1.1")));
            Assert.False(NetworkCidr.Parse("10.0.0.0/24").Contains(IPAddress.Parse("10.0.1.255")));
        }

        [Fact]
        public void ContainsSubnet()
        {
            Assert.True(NetworkCidr.Parse("10.0.0.0/24").Contains(NetworkCidr.Parse("10.0.0.0/24")));
            Assert.True(NetworkCidr.Parse("10.0.0.0/24").Contains(NetworkCidr.Parse("10.0.0.0/25")));
            Assert.False(NetworkCidr.Parse("10.0.0.0/24").Contains(NetworkCidr.Parse("10.0.0.0/23")));
            Assert.False(NetworkCidr.Parse("10.0.0.0/24").Contains(NetworkCidr.Parse("10.0.2.0/24")));
        }

        [Fact]
        public void AddressCount()
        {
            Assert.Equal(Math.Pow(2, 32), NetworkCidr.Parse("10.0.0.1/0").AddressCount);
            Assert.Equal(Math.Pow(2, 24), NetworkCidr.Parse("10.0.0.1/8").AddressCount);
            Assert.Equal(Math.Pow(2, 16), NetworkCidr.Parse("10.0.0.1/16").AddressCount);
            Assert.Equal(Math.Pow(2, 8), NetworkCidr.Parse("10.0.0.1/24").AddressCount);
        }

        [Fact]
        public void FirstLastAndNext()
        {
            var subnet = NetworkCidr.Parse("127.0.0.0/24");

            Assert.Equal("127.0.0.0", subnet.FirstAddress.ToString());
            Assert.Equal("127.0.0.1", subnet.FirstUsableAddress.ToString());
            Assert.Equal("127.0.0.255", subnet.LastAddress.ToString());
            Assert.Equal("127.0.1.0", subnet.NextAddress.ToString());

            subnet = NetworkCidr.Parse("10.0.1.0/16");

            Assert.Equal("10.0.0.0", subnet.FirstAddress.ToString());
            Assert.Equal("10.0.255.255", subnet.LastAddress.ToString());
            Assert.Equal("10.1.0.0", subnet.NextAddress.ToString());
        }

        [Fact]
        public void Overlaps()
        {
            var subnet = NetworkCidr.Parse("10.0.1.0/24");

            Assert.True(subnet.Overlaps(NetworkCidr.Parse("10.0.1.0/24")));
            Assert.True(subnet.Overlaps(NetworkCidr.Parse("10.0.1.0/16")));
            Assert.True(subnet.Overlaps(NetworkCidr.Parse("10.0.0.0/23")));
            Assert.True(subnet.Overlaps(NetworkCidr.Parse("10.0.1.16/28")));

            Assert.False(subnet.Overlaps(NetworkCidr.Parse("10.0.0.0/24")));
            Assert.False(subnet.Overlaps(NetworkCidr.Parse("10.0.2.0/24")));
        }

        [Fact]
        public void MaskedAddress()
        {
            var subnet = NetworkCidr.Parse("10.0.1.5/24");

            Assert.Equal(IPAddress.Parse("255.255.255.0"), subnet.Mask);
            Assert.Equal(IPAddress.Parse("10.0.1.0"), subnet.Address);
        }

        [Fact]
        public void Normalize()
        {
            Assert.Equal("10.0.0.0/14", NetworkCidr.Normalize(NetworkCidr.Parse("10.0.0.0/14").ToString()));
            Assert.Equal("10.168.0.0/14", NetworkCidr.Normalize(NetworkCidr.Parse("10.170.0.0/14").ToString()));
        }

        [Fact]
        public void SubnetMask()
        {
            Assert.Equal("1.2.3.4/32", new NetworkCidr(IPAddress.Parse("1.2.3.4"), IPAddress.Parse("255.255.255.255")));
            Assert.Equal("1.2.3.0/24", new NetworkCidr(IPAddress.Parse("1.2.3.4"), IPAddress.Parse("255.255.255.0")));
            Assert.Equal("1.2.0.0/16", new NetworkCidr(IPAddress.Parse("1.2.3.4"), IPAddress.Parse("255.255.0.0")));
            Assert.Equal("1.0.0.0/8", new NetworkCidr(IPAddress.Parse("1.2.3.4"), IPAddress.Parse("255.0.0.0")));

            // Verify that we check for holes in the subnet prefix.

            Assert.Throws<ArgumentException>(() => new NetworkCidr(IPAddress.Parse("1.2.3.4"), IPAddress.Parse("255.0.255.0")));
        }
    }
}
