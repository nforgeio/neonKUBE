//-----------------------------------------------------------------------------
// FILE:	    Test_HiveDns.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Hive;
using Neon.Xunit;
using Neon.Xunit.Hive;

using Xunit;

namespace TestHive
{
    public class Test_HiveDns : IClassFixture<HiveFixture>
    {
        private HiveFixture     hiveFixture;
        private HiveProxy       hive;

        public Test_HiveDns(HiveFixture fixture)
        {
            if (!fixture.LoginAndInitialize())
            {
                fixture.Reset();
            }

            this.hiveFixture = fixture;
            this.hive        = fixture.Hive;

            // Wait for the hive DNS and all node resolvers to be in
            // a consistent state.  This is slow (about a minute).

            fixture.ConvergeDns();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public void Test()
        {
            //-----------------------------------------------------------------
            // Verify that the hive starts out without any non-system DNS entries.

            Assert.Empty(hiveFixture.ListDnsEntries());

            //-----------------------------------------------------------------
            // Add a DNS entry and then verify that it can be listed and also
            // that it actually resolves correctly on a manager, worker, and
            // pet (if the hive includes workers and pets).

            hiveFixture.SetDnsEntry(
                new DnsEntry()
                {
                    Hostname  = "foo.test.com",
                    Endpoints = new List<DnsEndpoint>()
                    {
                        new DnsEndpoint()
                        {
                            Target = "1.2.3.4",
                            Check  = false
                        }
                    }
                });

            var item = hiveFixture.ListDnsEntries().SingleOrDefault(i => i.Hostname == "foo.test.com");

            Assert.NotNull(item);
            Assert.Equal("foo.test.com", item.Hostname);
            Assert.Single(item.Endpoints);

            var endpoint = item.Endpoints.First();

            Assert.Equal("1.2.3.4", endpoint.Target);
            Assert.False(endpoint.Check);

            // Test a DNS entry marked as belonging to the system.

            hiveFixture.SetDnsEntry(
                new DnsEntry()
                {
                    Hostname  = "bar.test.com",
                    IsSystem  = true,
                    Endpoints = new List<DnsEndpoint>()
                    {
                        new DnsEndpoint()
                        {
                            Target = "5.6.7.8",
                            Check  = false
                        }
                    }
                },
                waitUntilPropagated: true);

            //-----------------------------------------------------------------
            // Verify that the system DNS entry DOES NOT appear in the normal listing
            // and DOES appear when we include system entries.

            Assert.Single(hiveFixture.ListDnsEntries());
            Assert.Equal(2, hiveFixture.ListDnsEntries(includeSystem: true).Count);
            Assert.True(hiveFixture.ListDnsEntries(includeSystem: true).Single(i => i.Hostname == "bar.test.com").IsSystem);

            //-----------------------------------------------------------------
            // Verify the new system entry.

            item = hiveFixture.ListDnsEntries(includeSystem: true).SingleOrDefault(i => i.Hostname == "bar.test.com");

            Assert.NotNull(item);
            Assert.Equal("bar.test.com", item.Hostname);
            Assert.True(item.IsSystem);
            Assert.Single(item.Endpoints);

            endpoint = item.Endpoints.First();

            Assert.Equal("5.6.7.8", endpoint.Target);
            Assert.False(endpoint.Check);

            //-----------------------------------------------------------------
            // Verify that the two hostnames resolve correctly on various node types.
            // Note that we don't need to wait for the hive DNS hosts to converge 
            // because we specified [waitUntilPropagated: true] when we persisted
            // the DNS entry above.

            var manager = hive.Managers.First();   // We'll always have at least one manager.

            if (manager != null)
            {
                var response = manager.RunCommand("nslookup foo.test.com", RunOptions.None);

                Assert.Equal(0, response.ExitCode);
                Assert.Contains("Address: 1.2.3.4", response.OutputText);

                response = manager.RunCommand("nslookup bar.test.com", RunOptions.None);

                Assert.Equal(0, response.ExitCode);
                Assert.Contains("Address: 5.6.7.8", response.OutputText);
            }

            var worker = hive.Workers.FirstOrDefault();

            if (worker != null)
            {
                var response = worker.RunCommand("nslookup foo.test.com", RunOptions.None);

                Assert.Equal(0, response.ExitCode);
                Assert.Contains("Address: 1.2.3.4", response.OutputText);

                response = worker.RunCommand("nslookup bar.test.com", RunOptions.None);

                Assert.Equal(0, response.ExitCode);
                Assert.Contains("Address: 5.6.7.8", response.OutputText);
            }

            var pet = hive.Pets.FirstOrDefault();

            if (pet != null)
            {
                var response = pet.RunCommand("nslookup foo.test.com", RunOptions.None);

                Assert.Equal(0, response.ExitCode);
                Assert.Contains("Address: 1.2.3.4", response.OutputText);

                response = pet.RunCommand("nslookup bar.test.com", RunOptions.None);

                Assert.Equal(0, response.ExitCode);
                Assert.Contains("Address: 5.6.7.8", response.OutputText);
            }
        }
    }
}
