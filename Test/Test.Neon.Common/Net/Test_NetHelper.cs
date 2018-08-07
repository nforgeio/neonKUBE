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
using System.Threading;
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
        public void ModifyLocalHosts_Basic()
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
                // Ensure that we reset the local hosts before exiting the test.

                NetHelper.ModifyLocalHosts();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ModifyLocalHosts_Reliability()
        {
            try
            {
                // Verify that we start out with an undefined test host.

                //Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foobar.test.hive"));
if (File.Exists(@"C:\temp\dns.log")) File.Delete(@"C:\temp\dns.log");

                // We're going to perform multiple updates to ensure that
                // the DNS resolver is reliably picking up the changes.

                var hostEntries = new Dictionary<string, IPAddress>();

                for (int i = 0; i < 60; i++)
                {
                    var testAddress = $"1.2.3.{i}";

                    hostEntries.Clear();
                    hostEntries.Add("foobar.test.hive", IPAddress.Parse(testAddress));

                    NetHelper.ModifyLocalHosts(hostEntries);
                    Assert.Equal(testAddress, Dns.GetHostAddresses("foobar.test.hive").Single().ToString());

                    // Reset the hosts and verify.

                    NetHelper.ModifyLocalHosts();
                    //Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foobar.test.hive"));

                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }
            finally
            {
                // Ensure that we reset the local hosts before exiting the test.

                NetHelper.ModifyLocalHosts();
            }
        }

        [Fact]
        public void ModifyLocalHosts_IPValidation()
        {
            if (NeonHelper.IsWindows)
            {
                // The Windows DNS resolver doesn't consider all IP addresses to be valid.
                // Specifically, I'm seeing problems with addresses greater than or equal
                // to [240.0.0.0]. and also addresses with a leading 0, like [0.x.x.x].
                //
                // This test munges the hosts file to include hosts with addresses for each
                // valid possible leading number so that to ensure that we've identified
                // all of the exceptions.

                var hostsPath     = @"C:\Windows\System32\Drivers\etc\hosts";
                var nameToAddress = new Dictionary<string, IPAddress>();
                var addressBytes  = new byte[] { 0, 1, 2, 3 };

                for (int i = 1; i < 240; i++)
                {
                    addressBytes[0] = (byte)i;
                    nameToAddress.Add($"test-{i}.neon", new IPAddress(addressBytes));
                }

                var savedHosts = File.ReadAllText(hostsPath);

                try
                {
                    // Add the test entries to the hosts file and then wait for
                    // a bit to ensure that the resolver has picked up the changes.

                    var sbUpdatedHosts = new StringBuilder();

                    sbUpdatedHosts.AppendLine(savedHosts);

                    foreach (var item in nameToAddress)
                    {
                        sbUpdatedHosts.AppendLine($"{item.Value} {item.Key}");
                    }

                    File.WriteAllText(hostsPath, sbUpdatedHosts.ToString());
                    Thread.Sleep(2000);

                    // Verify that all of the test host resolve.

                    foreach (var item in nameToAddress)
                    {
                        var addresses = Dns.GetHostAddresses(item.Key);

                        if (addresses.Length == 0)
                        {
                            throw new Exception($"{item.Key} did not resolve.");
                        }
                        else if (!addresses[0].Equals(item.Value))
                        {
                            throw new Exception($"{item.Key} resolved to [{addresses[0]}] instead of [{item.Value}].");
                        }
                    }
                }
                finally
                {
                    // Restore the original hosts file.

                    File.WriteAllText(hostsPath, savedHosts);
                }
            }
        }
    }
}
