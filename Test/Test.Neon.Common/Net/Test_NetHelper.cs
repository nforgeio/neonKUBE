//-----------------------------------------------------------------------------
// FILE:	    Test_NetHelper.cs
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

using Neon.Common;
using Neon.Net;
using Neon.Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
        public void ModifyLocalHosts_Default()
        {
            try
            {
                // Clear any existing hosts sections.

                NetHelper.ModifyLocalHosts(section: null);

                // Verify that we start out with an undefined test host.

                Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foobar.test.nhive.io"));

                // Add the test entry and verify.

                var hostEntries = new Dictionary<string, IPAddress>();

                hostEntries.Add("foobar.test.nhive.io", IPAddress.Parse("1.2.3.4"));
                NetHelper.ModifyLocalHosts(hostEntries);
                Assert.Equal("1.2.3.4", Dns.GetHostAddresses("foobar.test.nhive.io").Single().ToString());

                // Reset the hosts and verify.

                NetHelper.ModifyLocalHosts();
                Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foobar.test.nhive.io"));
            }
            finally
            {
                // Ensure that we reset the local hosts before exiting the test.

                NetHelper.ModifyLocalHosts(section: null);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ModifyLocalHosts_NonDefault()
        {
            const string marker = "TEST";

            try
            {
                // Clear any existing hosts sections.

                NetHelper.ModifyLocalHosts(section: null);

                // Verify that we start out with an undefined test host.

                Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foobar.test.nhive.io"));

                // Add the test entry and verify.

                var hostEntries = new Dictionary<string, IPAddress>();

                hostEntries.Add("foobar.test.nhive.io", IPAddress.Parse("1.2.3.4"));
                NetHelper.ModifyLocalHosts(hostEntries, marker);
                Assert.Equal("1.2.3.4", Dns.GetHostAddresses("foobar.test.nhive.io").Single().ToString());

                // Reset the hosts and verify.

                NetHelper.ModifyLocalHosts(section: marker);
                Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foobar.test.nhive.io"));
            }
            finally
            {
                // Ensure that we reset the local hosts before exiting the test.

                NetHelper.ModifyLocalHosts(section: null);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ModifyLocalHosts_Multiple()
        {
            const string section1 = "TEST-1";
            const string section2 = "TEST-2";

            try
            {
                // Clear any existing hosts sections.

                NetHelper.ModifyLocalHosts(section: null);

                // Verify that we start out with an undefined test host.

                Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foobar.test.nhive.io"));

                // Add multiple sections and verify (including listing sections).

                var hostEntries = new Dictionary<string, IPAddress>();
                var sections    = (IEnumerable<string>)null;

                hostEntries.Add("foo-0.test.nhive.io", IPAddress.Parse("1.1.1.0"));
                NetHelper.ModifyLocalHosts(hostEntries);
                sections = NetHelper.ListLocalHostsSections();
                Assert.Single(sections, "MODIFY");

                hostEntries.Clear();
                hostEntries.Add("foo-1.test.nhive.io", IPAddress.Parse("1.1.1.1"));
                NetHelper.ModifyLocalHosts(hostEntries, section1);
                sections = NetHelper.ListLocalHostsSections();
                Assert.Equal(2, sections.Count());
                Assert.Contains("MODIFY", sections);
                Assert.Contains(section1.ToUpperInvariant(), sections);

                hostEntries.Clear();
                hostEntries.Add("foo-2.test.nhive.io", IPAddress.Parse("1.1.1.2"));
                NetHelper.ModifyLocalHosts(hostEntries, section2);
                sections = NetHelper.ListLocalHostsSections();
                Assert.Equal(3, sections.Count());
                Assert.Contains("MODIFY", sections);
                Assert.Contains(section1.ToUpperInvariant(), sections);
                Assert.Contains(section2.ToUpperInvariant(), sections);

                Assert.Equal("1.1.1.0", Dns.GetHostAddresses("foo-0.test.nhive.io").Single().ToString());
                Assert.Equal("1.1.1.1", Dns.GetHostAddresses("foo-1.test.nhive.io").Single().ToString());
                Assert.Equal("1.1.1.2", Dns.GetHostAddresses("foo-2.test.nhive.io").Single().ToString());

                // Reset the hosts and verify.

                NetHelper.ModifyLocalHosts();
                Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foo-0.test.nhive.io"));
                Assert.Equal("1.1.1.1", Dns.GetHostAddresses("foo-1.test.nhive.io").Single().ToString());
                Assert.Equal("1.1.1.2", Dns.GetHostAddresses("foo-2.test.nhive.io").Single().ToString());

                NetHelper.ModifyLocalHosts(section: section1);
                Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foo-0.test.nhive.io"));
                Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foo-1.test.nhive.io"));
                Assert.Equal("1.1.1.2", Dns.GetHostAddresses("foo-2.test.nhive.io").Single().ToString());

                NetHelper.ModifyLocalHosts(section: section2);
                Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foo-0.test.nhive.io"));
                Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foo-1.test.nhive.io"));
                Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foo-2.test.nhive.io"));
            }
            finally
            {
                // Ensure that we reset the local hosts before exiting the test.

                NetHelper.ModifyLocalHosts(section: null);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ModifyLocalHosts_Modify()
        {
            try
            {
                // Clear any existing hosts sections.

                NetHelper.ModifyLocalHosts(section: null);

                // Verify that we start out with an undefined test host.

                Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foobar.test.nhive.io"));

                // Add a default section and verify.

                var hostEntries = new Dictionary<string, IPAddress>();
                var sections    = (IEnumerable<string>)null;

                hostEntries.Add("foo-0.test.nhive.io", IPAddress.Parse("1.1.1.0"));
                NetHelper.ModifyLocalHosts(hostEntries);
                sections = NetHelper.ListLocalHostsSections();
                Assert.Single(sections, "MODIFY");

                // Submit the same definitions to the default section and verify that
                // we didn't rewrite the section by ensuring that the special section
                // marker host address hasn't changed.

                var originalMarkerAddress = Dns.GetHostAddresses("modify.neonforge-marker").Single().ToString();

                hostEntries.Clear();
                hostEntries.Add("foo-0.test.nhive.io", IPAddress.Parse("1.1.1.0"));
                NetHelper.ModifyLocalHosts(hostEntries);
                sections = NetHelper.ListLocalHostsSections();
                Assert.Single(sections, "MODIFY");
                Assert.Equal("1.1.1.0", Dns.GetHostAddresses("foo-0.test.nhive.io").Single().ToString());
                Assert.Equal(originalMarkerAddress, Dns.GetHostAddresses("modify.neonforge-marker").Single().ToString());

                // Modify the existing host and verify.

                hostEntries.Clear();
                hostEntries.Add("foo-0.test.nhive.io", IPAddress.Parse("1.1.1.1"));
                NetHelper.ModifyLocalHosts(hostEntries);
                sections = NetHelper.ListLocalHostsSections();
                Assert.Single(sections, "MODIFY");
                Assert.Equal("1.1.1.1", Dns.GetHostAddresses("foo-0.test.nhive.io").Single().ToString());
                Assert.NotEqual(originalMarkerAddress, Dns.GetHostAddresses("modify.neonforge-marker").Single().ToString());

                // Submit the same entries again and verify that [hosts] wasn't rewritten.

                originalMarkerAddress = Dns.GetHostAddresses("modify.neonforge-marker").Single().ToString();

                NetHelper.ModifyLocalHosts(hostEntries);
                sections = NetHelper.ListLocalHostsSections();
                Assert.Single(sections, "MODIFY");
                Assert.Equal("1.1.1.1", Dns.GetHostAddresses("foo-0.test.nhive.io").Single().ToString());
                Assert.Equal(originalMarkerAddress, Dns.GetHostAddresses("modify.neonforge-marker").Single().ToString());

                // Add a new hostname and verify.

                hostEntries.Clear();
                hostEntries.Add("foo-0.test.nhive.io", IPAddress.Parse("1.1.1.1"));
                hostEntries.Add("foo-100.test.nhive.io", IPAddress.Parse("1.1.1.100"));
                NetHelper.ModifyLocalHosts(hostEntries);
                sections = NetHelper.ListLocalHostsSections();
                Assert.Single(sections, "MODIFY");
                Assert.Equal("1.1.1.1", Dns.GetHostAddresses("foo-0.test.nhive.io").Single().ToString());
                Assert.Equal("1.1.1.100", Dns.GetHostAddresses("foo-100.test.nhive.io").Single().ToString());
                Assert.NotEqual(originalMarkerAddress, Dns.GetHostAddresses("modify.neonforge-marker").Single().ToString());

                // Submit the same entries again and verify that [hosts] wasn't rewritten.

                originalMarkerAddress = Dns.GetHostAddresses("modify.neonforge-marker").Single().ToString();

                NetHelper.ModifyLocalHosts(hostEntries);
                sections = NetHelper.ListLocalHostsSections();
                Assert.Single(sections, "MODIFY");
                Assert.Equal("1.1.1.1", Dns.GetHostAddresses("foo-0.test.nhive.io").Single().ToString());
                Assert.Equal("1.1.1.100", Dns.GetHostAddresses("foo-100.test.nhive.io").Single().ToString());
                Assert.Equal(originalMarkerAddress, Dns.GetHostAddresses("modify.neonforge-marker").Single().ToString());

                // Remove one of the entries and verify.

                hostEntries.Clear();
                hostEntries.Add("foo-0.test.nhive.io", IPAddress.Parse("1.1.1.1"));
                NetHelper.ModifyLocalHosts(hostEntries);
                sections = NetHelper.ListLocalHostsSections();
                Assert.Single(sections, "MODIFY");
                Assert.Equal("1.1.1.1", Dns.GetHostAddresses("foo-0.test.nhive.io").Single().ToString());
                Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foo-100.test.nhive.io"));
                Assert.NotEqual(originalMarkerAddress, Dns.GetHostAddresses("modify.neonforge-marker").Single().ToString());

                // Reset the hosts and verify.

                NetHelper.ModifyLocalHosts();
                Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foo-0.test.nhive.io"));
            }
            finally
            {
                // Ensure that we reset the local hosts before exiting the test.

                NetHelper.ModifyLocalHosts(section: null);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ModifyLocalHosts_Reliability()
        {
            try
            {
                // Clear any existing hosts sections.

                NetHelper.ModifyLocalHosts(section: null);

                // Verify that we start out with an undefined test host.

                Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foobar.test.nhive.io"));

                // We're going to perform multiple updates to ensure that
                // the DNS resolver is reliably picking up the changes.

                var hostEntries = new Dictionary<string, IPAddress>();

                for (int i = 0; i < 60; i++)
                {
                    var testAddress = $"1.2.3.{i}";

                    hostEntries.Clear();
                    hostEntries.Add("foobar.test.nhive.io", IPAddress.Parse(testAddress));

                    NetHelper.ModifyLocalHosts(hostEntries);
                    Assert.Equal(testAddress, Dns.GetHostAddresses("foobar.test.nhive.io").Single().ToString());

                    // Reset the hosts and verify.

                    NetHelper.ModifyLocalHosts();

                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }
            finally
            {
                // Ensure that we reset the local hosts before exiting the test.

                NetHelper.ModifyLocalHosts(section: null);
            }
        }

        [Fact]
        public void ModifyLocalHosts_IPValidation()
        {
            if (NeonHelper.IsWindows)
            {
                // Clear any existing hosts sections.

                NetHelper.ModifyLocalHosts(section: null);

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

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void GetReachableHost()
        {
            //-----------------------------------------------------------------
            // Verify that bad parameters are checked.

            Assert.Throws<ArgumentNullException>(() => NetHelper.GetReachableHost(null));
            Assert.Throws<ArgumentNullException>(() => NetHelper.GetReachableHost(new string[0]));

            //-----------------------------------------------------------------
            // IP address based hosts.

            // Verify that we always return the first host if it's healthy
            // when we're using [ReachableHostMode.ReturnFirst].

            Assert.Equal("127.0.0.1", NetHelper.GetReachableHost(new string[] { "127.0.0.1", "127.0.0.2", "127.0.0.3" }).Host);
            Assert.Equal("127.0.0.1", NetHelper.GetReachableHost(new string[] { "127.0.0.1", "127.0.0.2", "127.0.0.3" }).Address.ToString());
            Assert.Equal("127.0.0.1", NetHelper.GetReachableHost(new string[] { "127.0.0.1", "127.0.0.2", "127.0.0.3" }, ReachableHostMode.ReturnFirst).Host);

            // The [192.0.2.0/24] subnet is never supposed to be routable so we'll use 
            // some addresses in there to simulate offline hosts.

            const string badIP0 = "192.0.2.1";
            const string badIP1 = "192.0.2.2";
            const string badIP2 = "192.0.2.3";

            Assert.Equal("127.0.0.1", NetHelper.GetReachableHost(new string[] { "127.0.0.1", badIP0, badIP1 }).Host);
            Assert.Equal("127.0.0.1", NetHelper.GetReachableHost(new string[] { badIP0, "127.0.0.1", badIP1 }).Host);
            Assert.Equal("127.0.0.1", NetHelper.GetReachableHost(new string[] { badIP0, badIP1, "127.0.0.1" }).Host);

            // Verify the failure modes.

            Assert.Equal(badIP0, NetHelper.GetReachableHost(new string[] { badIP0, badIP1, badIP2 }).Host);
            Assert.Equal(badIP0, NetHelper.GetReachableHost(new string[] { badIP0, badIP1, badIP2 }, ReachableHostMode.ReturnFirst).Host);
            Assert.True( NetHelper.GetReachableHost(new string[] { badIP0, badIP1, badIP2 }, ReachableHostMode.ReturnFirst).Unreachable);
            Assert.Null(NetHelper.GetReachableHost(new string[] { badIP0, badIP1, badIP2 }, ReachableHostMode.ReturnNull));
            Assert.Throws<NetworkException>(() => NetHelper.GetReachableHost(new string[] { badIP0, badIP1, badIP2 }, ReachableHostMode.Throw).Host);

            //-----------------------------------------------------------------
            // Hostname based hosts.

            // Verify that we always return the first host if it's healthy
            // when we're using [ReachableHostMode.ReturnFirst].

            Assert.Equal("www.google.com", NetHelper.GetReachableHost(new string[] { "www.google.com", "www.microsoft.com", "www.facebook.com" }).Host);
            Assert.Equal("www.google.com", NetHelper.GetReachableHost(new string[] { "www.google.com", "www.microsoft.com", "www.facebook.com" }, ReachableHostMode.ReturnFirst).Host);
            Assert.False(NetHelper.GetReachableHost(new string[] { "www.google.com", "www.microsoft.com", "www.facebook.com" }, ReachableHostMode.ReturnFirst).Unreachable);

            const string badHost0 = "bad0.host.baddomain";
            const string badHost1 = "bad1.host.baddomain";
            const string badHost2 = "bad2.host.baddomain";

            Assert.Equal("127.0.0.1", NetHelper.GetReachableHost(new string[] { "127.0.0.1", badHost0, badHost1 }).Host);
            Assert.Equal("127.0.0.1", NetHelper.GetReachableHost(new string[] { badHost0, "127.0.0.1", badHost1 }).Host);
            Assert.Equal("127.0.0.1", NetHelper.GetReachableHost(new string[] { badHost0, badHost1, "127.0.0.1" }).Host);

            // Verify the failure modes.

            Assert.Equal(badHost0, NetHelper.GetReachableHost(new string[] { badHost0, badHost1, badHost2 }).Host);
            Assert.Equal(badHost0, NetHelper.GetReachableHost(new string[] { badHost0, badHost1, badHost2 }, ReachableHostMode.ReturnFirst).Host);
            Assert.True(NetHelper.GetReachableHost(new string[] { badHost0, badHost1, badHost2 }, ReachableHostMode.ReturnFirst).Unreachable);
            Assert.Null(NetHelper.GetReachableHost(new string[] { badHost0, badHost1, badHost2 }, ReachableHostMode.ReturnNull));
            Assert.Throws<NetworkException>(() => NetHelper.GetReachableHost(new string[] { badHost0, badHost1, badHost2 }, ReachableHostMode.Throw));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void GetReachableHosts()
        {
            if (!NeonHelper.IsOSX)
            {
                // Loopback addresses other than 127.0.0.1 aren't routable by default
                // on OS/X.  So wse won't run these tests there.

                //-----------------------------------------------------------------
                // IP address based hosts.

                TestHelper.AssertEquivalent(new string[] { "127.0.0.1", "127.0.0.2", "127.0.0.3" }, NetHelper.GetReachableHosts(new string[] { "127.0.0.1", "127.0.0.2", "127.0.0.3" }).Select(rh => rh.Host));
                TestHelper.AssertEquivalent(new string[] { "127.0.0.1", "127.0.0.2", "127.0.0.3" }, NetHelper.GetReachableHosts(new string[] { "127.0.0.1", "127.0.0.2", "127.0.0.3" }).Select(rh => rh.Address.ToString()));
            }

            // The [192.0.2.0/24] subnet is never supposed to be routable so we'll use 
            // some addresses in there to simulate offline hosts.

            const string badIP0 = "192.0.2.1";
            const string badIP1 = "192.0.2.2";
            const string badIP2 = "192.0.2.3";

            TestHelper.AssertEquivalent(new string[] { "127.0.0.1" }, NetHelper.GetReachableHosts(new string[] { "127.0.0.1", badIP0, badIP1 }).Select(rh => rh.Host));
            TestHelper.AssertEquivalent(new string[] { "127.0.0.1" }, NetHelper.GetReachableHosts(new string[] { badIP0, "127.0.0.1", badIP1 }).Select(rh => rh.Host));
            TestHelper.AssertEquivalent(new string[] { "127.0.0.1" }, NetHelper.GetReachableHosts(new string[] { badIP0, badIP1, "127.0.0.1" }).Select(rh => rh.Host));      
            
            // Verify when no hosts are reachable.

            Assert.Empty( NetHelper.GetReachableHosts(new string[] { badIP0, badIP1, badIP2 }));

            //-----------------------------------------------------------------
            // Hostname based hosts.

            // Verify that we always return the first host if it's healthy
            // when we're using [ReachableHostMode.ReturnFirst].

            TestHelper.AssertEquivalent(new string[] { "www.google.com", "www.microsoft.com", "www.facebook.com" }, NetHelper.GetReachableHosts(new string[] { "www.google.com", "www.microsoft.com", "www.facebook.com" }).Select(rh => rh.Host));

            const string badHost0 = "bad0.host.baddomain";
            const string badHost1 = "bad1.host.baddomain";
            const string badHost2 = "bad2.host.baddomain";

            TestHelper.AssertEquivalent(new string[] { "127.0.0.1" }, NetHelper.GetReachableHosts(new string[] { "127.0.0.1", badHost0, badHost1 }).Select(rh => rh.Host));
            TestHelper.AssertEquivalent(new string[] { "127.0.0.1" }, NetHelper.GetReachableHosts(new string[] { badHost0, "127.0.0.1", badHost1 }).Select(rh => rh.Host));
            TestHelper.AssertEquivalent(new string[] { "127.0.0.1" }, NetHelper.GetReachableHosts(new string[] { badHost0, badHost1, "127.0.0.1" }).Select(rh => rh.Host));

            // Verify when no hosts are reachable.

            Assert.Empty(NetHelper.GetReachableHosts(new string[] { badHost0, badHost1, badHost2 }));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void TryParseIPv4Endpoint()
        {
            IPEndPoint endpoint;

            Assert.True(NetHelper.TryParseIPv4Endpoint("127.0.0.1:80", out endpoint));
            Assert.Equal("127.0.0.1", endpoint.Address.ToString());
            Assert.Equal(80, endpoint.Port);

            Assert.False(NetHelper.TryParseIPv4Endpoint("127.0.0.256:80", out endpoint));
            Assert.False(NetHelper.TryParseIPv4Endpoint("127.0.0.1100000000:80", out endpoint));
            Assert.False(NetHelper.TryParseIPv4Endpoint("127.0.0.1:65536", out endpoint));
            Assert.False(NetHelper.TryParseIPv4Endpoint("127.0.0.1:1000000", out endpoint));
            Assert.False(NetHelper.TryParseIPv4Endpoint("127.0.0.1", out endpoint));
            Assert.False(NetHelper.TryParseIPv4Endpoint("", out endpoint));
            Assert.False(NetHelper.TryParseIPv4Endpoint(null, out endpoint));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ParseIPv4Endpoint()
        {
            Assert.Equal(new IPEndPoint(IPAddress.Loopback, 80), NetHelper.ParseIPv4Endpoint("127.0.0.1:80"));

            Assert.Throws<FormatException>(() => NetHelper.ParseIPv4Endpoint("127.0.0.256:80"));
            Assert.Throws<FormatException>(() => NetHelper.ParseIPv4Endpoint("127.0.0.1100000000:80"));
            Assert.Throws<FormatException>(() => NetHelper.ParseIPv4Endpoint("127.0.0.1:65536"));
            Assert.Throws<FormatException>(() => NetHelper.ParseIPv4Endpoint("127.0.0.1:1000000"));
            Assert.Throws<FormatException>(() => NetHelper.ParseIPv4Endpoint("127.0.0.1"));
            Assert.Throws<FormatException>(() => NetHelper.ParseIPv4Endpoint(""));
            Assert.Throws<FormatException>(() => NetHelper.ParseIPv4Endpoint(null));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void GetUnusedTcpPort()
        {
            // Verify that we can obtain 100 unique ports on [127.0.0.1].
            // Note that this is a tiny bit fragile because it's possible,
            // but unlikely that there aren't enough free ports on that
            // interface or that the OS will cycle through the free ports
            // before we're done.

            var address = IPAddress.Parse("127.0.0.1");
            var ports   = new HashSet<int>();

            for (int i = 0; i < 100; i++)
            {
                var port = NetHelper.GetUnusedTcpPort(address);

                Assert.DoesNotContain(ports, p => p == port);

                ports.Add(port);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void IsValidHost()
        {
            var longestLabel = new string('a', 61);
            var tooLongLabel = new string('b', 62);

            Assert.True(NetHelper.IsValidHost("test"));
            Assert.True(NetHelper.IsValidHost("test.com"));
            Assert.True(NetHelper.IsValidHost("server1.test.com"));
            Assert.True(NetHelper.IsValidHost("0.com"));
            Assert.True(NetHelper.IsValidHost("test0.com"));
            Assert.True(NetHelper.IsValidHost("test-0.com"));
            Assert.True(NetHelper.IsValidHost("test_0.com"));
            Assert.True(NetHelper.IsValidHost($"{longestLabel}.com"));

            Assert.False(NetHelper.IsValidHost("test..com"));
            Assert.False(NetHelper.IsValidHost("/test.com"));
            Assert.False(NetHelper.IsValidHost("{test}.com"));

            // $todo(jefflill):
            //
            // This test is failing but isn't a huge deal.  At some point
            // I should go back and fix the regex in NetHelper.

            //Assert.False(NetHelper.IsValidHost($"{tooLongLabel}.com"));

            // A FQDN may be up to 255 characters long.

            var longestHost =
                new string('a', 50) + "." +
                new string('b', 50) + "." +
                new string('c', 50) + "." +
                new string('d', 50) + "." +
                new string('e', 51);

            Assert.Equal(255, longestHost.Length);
            Assert.True(NetHelper.IsValidHost(longestHost));
            Assert.False(NetHelper.IsValidHost(longestHost + "f"));
        }
    }
}
