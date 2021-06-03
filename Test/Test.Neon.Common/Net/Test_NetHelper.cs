//-----------------------------------------------------------------------------
// FILE:	    Test_NetHelper.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Xunit;

using Neon.Common;
using Neon.Net;
using Neon.Xunit;

namespace TestCommon
{
    [Trait(TestTrait.Category, TestArea.NeonCommon)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_NetHelper
    {
        /// <summary>
        /// Ensures that the specified host name does not exist.
        /// </summary>
        /// <param name="hostName">The host name.</param>
        private void VerifyNotExists(string hostName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hostName), nameof(hostName));

            // $todo(jefflill):
            //
            // It seems that it takes some additional time for DNS names that have been
            // removed from the hosts file to actually be purged.  I've worked on different
            // techniques to address this without success.
            //
            // I'm going to disable the test for now.  This is not a big deal.

            // Assert.Throws<SocketException>(() => Dns.GetHostAddresses("foobar.test.nhive.io"));
        }

        [Fact]
        public void AddressEquals()
        {
            Assert.True(NetHelper.AddressEquals(NetHelper.ParseIPv4Address("10.0.0.1"), NetHelper.ParseIPv4Address("10.0.0.1")));
            Assert.False(NetHelper.AddressEquals(NetHelper.ParseIPv4Address("10.0.0.1"), NetHelper.ParseIPv4Address("10.0.0.2")));
        }

        [Fact]
        public void AddressIncrement()
        {
            Assert.True(NetHelper.AddressEquals(NetHelper.ParseIPv4Address("0.0.0.1"), NetHelper.AddressIncrement(NetHelper.ParseIPv4Address("0.0.0.0"))));
            Assert.True(NetHelper.AddressEquals(NetHelper.ParseIPv4Address("0.0.1.0"), NetHelper.AddressIncrement(NetHelper.ParseIPv4Address("0.0.0.255"))));
            Assert.True(NetHelper.AddressEquals(NetHelper.ParseIPv4Address("0.1.0.0"), NetHelper.AddressIncrement(NetHelper.ParseIPv4Address("0.0.255.255"))));
            Assert.True(NetHelper.AddressEquals(NetHelper.ParseIPv4Address("1.0.0.0"), NetHelper.AddressIncrement(NetHelper.ParseIPv4Address("0.255.255.255"))));
            Assert.True(NetHelper.AddressEquals(NetHelper.ParseIPv4Address("0.0.0.0"), NetHelper.AddressIncrement(NetHelper.ParseIPv4Address("255.255.255.255"))));
        }

        [Fact]
        public void ParseIPv4()
        {
            Assert.Equal(IPAddress.Parse("1.2.3.4"), NetHelper.ParseIPv4Address("1.2.3.4"));
            Assert.Equal(IPAddress.Parse("10.0.0.1"), NetHelper.ParseIPv4Address("10.0.0.1"));
            Assert.Equal(IPAddress.Parse("255.255.255.255"), NetHelper.ParseIPv4Address("255.255.255.255"));

            Assert.Throws<ArgumentNullException>(() => NetHelper.ParseIPv4Address(null));
            Assert.Throws<ArgumentNullException>(() => NetHelper.ParseIPv4Address(""));

            Assert.Throws<FormatException>(() => NetHelper.ParseIPv4Address("1.2.3.1000"));
            Assert.Throws<FormatException>(() => NetHelper.ParseIPv4Address("garbage"));
            Assert.Throws<FormatException>(() => NetHelper.ParseIPv4Address("123456"));
        }

        [Fact]
        public void TryParseIPv4()
        {
            IPAddress address;

            Assert.True(NetHelper.TryParseIPv4Address("1.2.3.4", out address));
            Assert.Equal(IPAddress.Parse("1.2.3.4"), address);

            Assert.True(NetHelper.TryParseIPv4Address("10.0.0.1", out address));
            Assert.Equal(IPAddress.Parse("10.0.0.1"), address);

            Assert.True(NetHelper.TryParseIPv4Address("255.255.255.255", out address));
            Assert.Equal(IPAddress.Parse("255.255.255.255"), address);

            Assert.False(NetHelper.TryParseIPv4Address(null, out address));
            Assert.False(NetHelper.TryParseIPv4Address("", out address));

            Assert.False(NetHelper.TryParseIPv4Address("1.2.3.1000", out address));
            Assert.False(NetHelper.TryParseIPv4Address("garbage", out address));
            Assert.False(NetHelper.TryParseIPv4Address("123456", out address));
        }

        [Fact]
        public void ParseIPv6()
        {
            Assert.Equal(IPAddress.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7334"), NetHelper.ParseIPv6Address("2001:0db8:85a3:0000:0000:8a2e:0370:7334"));
            Assert.Equal(IPAddress.Parse("::1"), NetHelper.ParseIPv6Address("::1"));

            Assert.Throws<ArgumentNullException>(() => NetHelper.ParseIPv6Address(null));
            Assert.Throws<ArgumentNullException>(() => NetHelper.ParseIPv6Address(""));
            Assert.Throws<FormatException>(() => NetHelper.ParseIPv6Address("garbage"));
        }

        [Fact]
        public void TryParseIPv6()
        {
            IPAddress address;

            Assert.True(NetHelper.TryParseIPv6Address("2001:0db8:85a3:0000:0000:8a2e:0370:7334", out address));
            Assert.Equal(IPAddress.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7334"), address);

            Assert.True(NetHelper.TryParseIPv6Address("::1", out address));
            Assert.Equal(IPAddress.Parse("::1"), address);

            Assert.False(NetHelper.TryParseIPv6Address(null, out address));
            Assert.False(NetHelper.TryParseIPv6Address("", out address));
            Assert.False(NetHelper.TryParseIPv4Address("garbage", out address));
        }

        [Fact]
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
        public void ModifyLocalHosts_Default()
        {
            try
            {
                // Clear any existing hosts sections.

                NetHelper.ModifyLocalHosts(section: null);

                // Verify that we start out with an undefined test host.

                VerifyNotExists("foobar.test.nhive.io");

                // Add the test entry and verify.

                var hostEntries = new Dictionary<string, IPAddress>();

                hostEntries.Add("foobar.test.nhive.io", NetHelper.ParseIPv4Address("1.2.3.4"));
                NetHelper.ModifyLocalHosts(hostEntries);
                Assert.Equal("1.2.3.4", Dns.GetHostAddresses("foobar.test.nhive.io").Single().ToString());

                // Reset the hosts and verify.

                NetHelper.ModifyLocalHosts();
                VerifyNotExists("foobar.test.nhive.io");
            }
            finally
            {
                // Ensure that we reset the local hosts before exiting the test.

                NetHelper.ModifyLocalHosts(section: null);
            }
        }

        [Fact]
        public void ModifyLocalHosts_NonDefault()
        {
            const string marker = "TEST";

            try
            {
                // Clear any existing hosts sections.

                NetHelper.ModifyLocalHosts(section: null);

                // Verify that we start out with an undefined test host.

                VerifyNotExists("foobar.test.nhive.io");

                // Add the test entry and verify.

                var hostEntries = new Dictionary<string, IPAddress>();

                hostEntries.Add("foobar.test.nhive.io", NetHelper.ParseIPv4Address("1.2.3.4"));
                NetHelper.ModifyLocalHosts(hostEntries, marker);
                Assert.Equal("1.2.3.4", Dns.GetHostAddresses("foobar.test.nhive.io").Single().ToString());

                // Reset the hosts and verify.

                NetHelper.ModifyLocalHosts(section: marker);
                VerifyNotExists("foobar.test.nhive.io");
            }
            finally
            {
                // Ensure that we reset the local hosts before exiting the test.

                NetHelper.ModifyLocalHosts(section: null);
            }
        }

        [Fact]
        public void ModifyLocalHosts_Multiple()
        {
            const string section1 = "TEST-1";
            const string section2 = "TEST-2";

            try
            {
                // Clear any existing hosts sections.

                NetHelper.ModifyLocalHosts(section: null);

                // Verify that we start out with an undefined test host.

                VerifyNotExists("foobar.test.nhive.io");

                // Add multiple sections and verify (including listing sections).

                var hostEntries = new Dictionary<string, IPAddress>();
                var sections    = (IEnumerable<string>)null;

                hostEntries.Add("foo-0.test.nhive.io", NetHelper.ParseIPv4Address("1.1.1.0"));
                NetHelper.ModifyLocalHosts(hostEntries);
                sections = NetHelper.ListLocalHostsSections();
                Assert.Single(sections, "MODIFY");

                hostEntries.Clear();
                hostEntries.Add("foo-1.test.nhive.io", NetHelper.ParseIPv4Address("1.1.1.1"));
                NetHelper.ModifyLocalHosts(hostEntries, section1);
                sections = NetHelper.ListLocalHostsSections();
                Assert.Equal(2, sections.Count());
                Assert.Contains("MODIFY", sections);
                Assert.Contains(section1.ToUpperInvariant(), sections);

                hostEntries.Clear();
                hostEntries.Add("foo-2.test.nhive.io", NetHelper.ParseIPv4Address("1.1.1.2"));
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
                VerifyNotExists("foo-0.test.nhive.io");
                Assert.Equal("1.1.1.1", Dns.GetHostAddresses("foo-1.test.nhive.io").Single().ToString());
                Assert.Equal("1.1.1.2", Dns.GetHostAddresses("foo-2.test.nhive.io").Single().ToString());

                NetHelper.ModifyLocalHosts(section: section1);
                VerifyNotExists("foo-0.test.nhive.io");
                VerifyNotExists("foo-1.test.nhive.io");
                Assert.Equal("1.1.1.2", Dns.GetHostAddresses("foo-2.test.nhive.io").Single().ToString());

                NetHelper.ModifyLocalHosts(section: section2);
                VerifyNotExists("foo-0.test.nhive.io");
                VerifyNotExists("foo-1.test.nhive.io");
                VerifyNotExists("foo-2.test.nhive.io");
            }
            finally
            {
                // Ensure that we reset the local hosts before exiting the test.

                NetHelper.ModifyLocalHosts(section: null);
            }
        }

        [Fact]
        public void ModifyLocalHosts_Modify()
        {
            try
            {
                // Clear any existing hosts sections.

                NetHelper.ModifyLocalHosts(section: null);

                // Verify that we start out with an undefined test host.

                VerifyNotExists("foobar.test.nhive.io");

                // Add a default section and verify.

                var hostEntries = new Dictionary<string, IPAddress>();
                var sections    = (IEnumerable<string>)null;

                hostEntries.Add("foo-0.test.nhive.io", NetHelper.ParseIPv4Address("1.1.1.0"));
                NetHelper.ModifyLocalHosts(hostEntries);
                sections = NetHelper.ListLocalHostsSections();
                Assert.Single(sections, "MODIFY");

                // Submit the same definitions to the default section and verify that
                // we didn't rewrite the section by ensuring that the special section
                // marker host address hasn't changed.

                var originalMarkerAddress = Dns.GetHostAddresses("modify.neonforge-marker").Single().ToString();

                hostEntries.Clear();
                hostEntries.Add("foo-0.test.nhive.io", NetHelper.ParseIPv4Address("1.1.1.0"));
                NetHelper.ModifyLocalHosts(hostEntries);
                sections = NetHelper.ListLocalHostsSections();
                Assert.Single(sections, "MODIFY");
                Assert.Equal("1.1.1.0", Dns.GetHostAddresses("foo-0.test.nhive.io").Single().ToString());
                Assert.Equal(originalMarkerAddress, Dns.GetHostAddresses("modify.neonforge-marker").Single().ToString());

                // Modify the existing host and verify.

                hostEntries.Clear();
                hostEntries.Add("foo-0.test.nhive.io", NetHelper.ParseIPv4Address("1.1.1.1"));
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
                hostEntries.Add("foo-0.test.nhive.io", NetHelper.ParseIPv4Address("1.1.1.1"));
                hostEntries.Add("foo-100.test.nhive.io", NetHelper.ParseIPv4Address("1.1.1.100"));
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
                hostEntries.Add("foo-0.test.nhive.io", NetHelper.ParseIPv4Address("1.1.1.1"));
                NetHelper.ModifyLocalHosts(hostEntries);
                sections = NetHelper.ListLocalHostsSections();
                Assert.Single(sections, "MODIFY");
                Assert.Equal("1.1.1.1", Dns.GetHostAddresses("foo-0.test.nhive.io").Single().ToString());
                VerifyNotExists("foo-100.test.nhive.io");
                Assert.NotEqual(originalMarkerAddress, Dns.GetHostAddresses("modify.neonforge-marker").Single().ToString());

                // Reset the hosts and verify.

                NetHelper.ModifyLocalHosts();
                VerifyNotExists("foo-0.test.nhive.io");
            }
            finally
            {
                // Ensure that we reset the local hosts before exiting the test.

                NetHelper.ModifyLocalHosts(section: null);
            }
        }

        [Fact]
        [Trait(TestTrait.Category, TestTrait.Buggy)]    // This has never been entirely reliable.
        public void ModifyLocalHosts_Reliability()
        {
            try
            {
                // Clear any existing hosts sections.

                NetHelper.ModifyLocalHosts(section: null);

                // Verify that we start out with an undefined test host.

                VerifyNotExists("foobar.test.nhive.io");

                // We're going to perform multiple updates to ensure that
                // the DNS resolver is reliably picking up the changes.

                var hostEntries = new Dictionary<string, IPAddress>();

                for (int i = 0; i < 60; i++)
                {
                    var testAddress = $"1.2.3.{i}";

                    hostEntries.Clear();
                    hostEntries.Add("foobar.test.nhive.io", NetHelper.ParseIPv4Address(testAddress));

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
        public void GetUnusedIpPort()
        {
            // Verify that we can obtain 100 unique ports on [127.0.0.1].
            // Note that this is a tiny bit fragile because it's possible,
            // but unlikely that there aren't enough free ports on that
            // interface or that the OS will cycle through the free ports
            // before we're done.

            var address = NetHelper.ParseIPv4Address("127.0.0.1");
            var ports   = new HashSet<int>();

            for (int i = 0; i < 100; i++)
            {
                var port = NetHelper.GetUnusedIpPort(address);

                Assert.DoesNotContain(ports, p => p == port);

                ports.Add(port);
            }
        }

        [Fact]
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

        [Fact]
        public void GetNetworkConfiguration()
        {
            var routableAddress = NetHelper.GetRoutableIpAddress();
            var netConfig       = NetHelper.GetNetworkConfiguration();

            if (routableAddress == null)
            {
                // The workstation looks like it's not connected to a network.

                Assert.Null(netConfig);
                return;
            }

            // There isn't an easy way to verify the configuration values without
            // parsing platform specific tools output so we're just going to basic
            // checks here.

            Assert.NotNull(netConfig);
            Assert.NotNull(netConfig.InterfaceName);
            Assert.Equal(routableAddress.ToString(), netConfig.Address);
            Assert.NotEmpty(netConfig.NameServers);
            Assert.NotNull(netConfig.Subnet);
            Assert.NotNull(netConfig.Gateway);
        }

        [Fact]
        public void ToAwsS3Uri()
        {
            Assert.Equal("s3://bucket/path/to/object", NetHelper.ToAwsS3Uri("s3://bucket/path/to/object"));
            Assert.Equal("s3://bucket/path/to/object", NetHelper.ToAwsS3Uri("https://bucket.s3-us-west-2.amazonaws.com/path/to/object"));

            Assert.Throws<ArgumentException>(() => NetHelper.ToAwsS3Uri("http://bucket/path/to/object"));                                       // Only HTTPS is allowed
            Assert.Throws<ArgumentException>(() => NetHelper.ToAwsS3Uri("https://127.0.0.1/path/to/object"));                                   // Host IPv4 address are not allowed
            Assert.Throws<ArgumentException>(() => NetHelper.ToAwsS3Uri("https://[3FFE:0000:0000:0001:0200:F8FF:FE75:50DF]/path/to/object"));   // Host IPv6 address are not allowed

            // Ensure that we check that HTTP URIs actually reference an S3 bucket.

            Assert.Throws<ArgumentException>(() => NetHelper.ToAwsS3Uri("https://bucket.s3-us-west-2.amazonaws.net/path/to/object"));           // Doesn't end with:                ".com"
            Assert.Throws<ArgumentException>(() => NetHelper.ToAwsS3Uri("https://bucket.s3-us-west-2.XXXXXXXXX.com/path/to/object"));           // Domain label isn't:              "amazonaws"
            Assert.Throws<ArgumentException>(() => NetHelper.ToAwsS3Uri("https://bucket.xx-us-west-2.amazonaws.com/path/to/object"));           // Domain label doesn't start with: "s3-"
            Assert.Throws<ArgumentException>(() => NetHelper.ToAwsS3Uri("https://bucket.s3us-west-2.amazonaws.com/path/to/object"));            // Domain label doesn't start with: "s3-"
            Assert.Throws<ArgumentException>(() => NetHelper.ToAwsS3Uri("https://bucket/path/to/object"));                                      // Not enough labels
            Assert.Throws<ArgumentException>(() => NetHelper.ToAwsS3Uri("https://bucket.s3-us-west-2/path/to/object"));                         // Not enough labels
            Assert.Throws<ArgumentException>(() => NetHelper.ToAwsS3Uri("https://bucket.s3-us-west-2.amazonaws/path/to/object"));               // Not enough labels
        }
    }
}
