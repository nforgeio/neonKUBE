//-----------------------------------------------------------------------------
// FILE:	    Test_ClusterDns.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Cluster;
using Neon.Common;
using Neon.Xunit;
using Neon.Xunit.Cluster;

using Xunit;

namespace TestNeonCluster
{
    public class Test_ClusterDns : IClassFixture<ClusterFixture>
    {
        private ClusterFixture cluster;
        private ClusterProxy clusterProxy;

        public Test_ClusterDns(ClusterFixture cluster)
        {
            if (!cluster.LoginAndInitialize())
            {
                cluster.Reset();
            }

            this.cluster = cluster;
            this.clusterProxy = cluster.Cluster;

            // Wait for the cluster DNS and all node resolvers to be in
            // a consistent state.  This is slow (about a minute).

            cluster.ConvergeDns();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Test()
        {
            //-----------------------------------------------------------------
            // Verify that the cluster starts out without any non-system DNS entries.

            Assert.Empty(cluster.ListDnsEntries());

            //-----------------------------------------------------------------
            // Add a DNS entry and then verify that it can be listed and also
            // that it actually resolves correctly on a manager, worker, and
            // pet (if the cluster includes workers and pets).

            cluster.SetDnsEntry("foo.test.com",
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

            var item = cluster.ListDnsEntries().SingleOrDefault(i => i.Name == "foo.test.com");

            Assert.NotNull(item);
            Assert.Equal("foo.test.com", item.Entry.Hostname);
            Assert.Single(item.Entry.Endpoints);

            var endpoint = item.Entry.Endpoints.First();

            Assert.Equal("1.2.3.4", endpoint.Target);
            Assert.False(endpoint.Check);

            // Test a DNS entry marked as belonging to the system.

            cluster.SetDnsEntry("(neon)-bar.test.com",
                new DnsEntry()
                {
                    Hostname  = "bar.test.com",
                    Endpoints = new List<DnsEndpoint>()
                    {
                        new DnsEndpoint()
                        {
                            Target = "5.6.7.8",
                            Check  = false
                        }
                    }
                });

            //-----------------------------------------------------------------
            // Verify that the system DNS entry DOES NOT appear in the normal listing
            // and DOES appear when we include system entries.

            Assert.Single(cluster.ListDnsEntries());
            Assert.Equal(2, cluster.ListDnsEntries(includeSystem: true).Count);

            //-----------------------------------------------------------------
            // Re-verify the non-system entry to ensure it didn't get munged.

            item = cluster.ListDnsEntries().SingleOrDefault(i => i.Name == "foo.test.com");

            Assert.NotNull(item);
            Assert.Equal("foo.test.com", item.Entry.Hostname);
            Assert.Single(item.Entry.Endpoints);

            endpoint = item.Entry.Endpoints.First();

            Assert.Equal("1.2.3.4", endpoint.Target);
            Assert.False(endpoint.Check);

            //-----------------------------------------------------------------
            // Verify the new system entry.

            item = cluster.ListDnsEntries(includeSystem: true).SingleOrDefault(i => i.Name == "(neon)-bar.test.com");

            Assert.NotNull(item);
            Assert.Equal("bar.test.com", item.Entry.Hostname);
            Assert.Single(item.Entry.Endpoints);

            endpoint = item.Entry.Endpoints.First();

            Assert.Equal("5.6.7.8", endpoint.Target);
            Assert.False(endpoint.Check);

            //-----------------------------------------------------------------
            // Wait for the cluster DNS to converge and then verify that the
            // two hostnames resolve correctly on various node types.

            cluster.ConvergeDns();

            var manager = clusterProxy.Managers.First();   // We'll always have at least one manager.

            if (manager != null)
            {
                var response = manager.RunCommand("nslookup foo.test.com", RunOptions.None);

                Assert.Equal(0, response.ExitCode);
                Assert.Contains("Address: 1.2.3.4", response.OutputText);

                response = manager.RunCommand("nslookup bar.test.com", RunOptions.None);

                Assert.Equal(0, response.ExitCode);
                Assert.Contains("Address: 5.6.7.8", response.OutputText);
            }

            var worker = clusterProxy.Workers.FirstOrDefault();

            if (worker != null)
            {
                var response = worker.RunCommand("nslookup foo.test.com", RunOptions.None);

                Assert.Equal(0, response.ExitCode);
                Assert.Contains("Address: 1.2.3.4", response.OutputText);

                response = worker.RunCommand("nslookup bar.test.com", RunOptions.None);

                Assert.Equal(0, response.ExitCode);
                Assert.Contains("Address: 5.6.7.8", response.OutputText);
            }

            var pet = clusterProxy.Pets.FirstOrDefault();

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
