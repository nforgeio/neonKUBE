//-----------------------------------------------------------------------------
// FILE:	    Test_NetHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Net;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_NetHelper
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void AddressEquals()
        {
            Assert.True(NetHelper.AddressEquals(IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.1")));
            Assert.False(NetHelper.AddressEquals(IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.2")));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void AddressIncrement()
        {
            Assert.True(NetHelper.AddressEquals(IPAddress.Parse("0.0.0.1"), NetHelper.AddressIncrement(IPAddress.Parse("0.0.0.0"))));
            Assert.True(NetHelper.AddressEquals(IPAddress.Parse("0.0.1.0"), NetHelper.AddressIncrement(IPAddress.Parse("0.0.0.255"))));
            Assert.True(NetHelper.AddressEquals(IPAddress.Parse("0.1.0.0"), NetHelper.AddressIncrement(IPAddress.Parse("0.0.255.255"))));
            Assert.True(NetHelper.AddressEquals(IPAddress.Parse("1.0.0.0"), NetHelper.AddressIncrement(IPAddress.Parse("0.255.255.255"))));
            Assert.True(NetHelper.AddressEquals(IPAddress.Parse("0.0.0.0"), NetHelper.AddressIncrement(IPAddress.Parse("255.255.255.255"))));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Conversions()
        {
            Assert.True(NetHelper.AddressEquals(IPAddress.Parse("0.0.0.0"), NetHelper.UintToAddress(0)));
            Assert.True(NetHelper.AddressEquals(IPAddress.Parse("255.0.0.0"), NetHelper.UintToAddress(0xFF000000)));
            Assert.True(NetHelper.AddressEquals(IPAddress.Parse("1.2.3.4"), NetHelper.UintToAddress(0x01020304)));

            Assert.Equal(0x00000000L, NetHelper.AddressToUint(IPAddress.Parse("0.0.0.0")));
            Assert.Equal(0xFF000000L, NetHelper.AddressToUint(IPAddress.Parse("255.0.0.0")));
            Assert.Equal(0x01020304L, NetHelper.AddressToUint(IPAddress.Parse("1.2.3.4")));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ModifyLocalHosts()
        {
            try
            {
                // Verify that we start out with an undefined test host.

                Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foobar.test.hive"));

                // Add the test entry and verify.

                var hostEntries = new Dictionary<string, IPAddress>();

                hostEntries.Add("foobar.test.hive", IPAddress.Parse("1.2.3.4"));
                NetHelper.ModifyLocalHosts(hostEntries);
                Assert.Equal("1.2.3.4", Dns.GetHostAddresses("foobar.test.hive").Single().ToString());

                // Reset the hosts and verify.

                NetHelper.ModifyLocalHosts();
                Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foobar.test.hive"));
            }
            finally
            {
                // Ensure that we reset the local hosts when any of the
                // tests above fail.

                NetHelper.ModifyLocalHosts();
            }
        }
    }
}
