//-----------------------------------------------------------------------------
// FILE:	    Test_HiveTrafficDirector.Public.Https.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Hive;
using Neon.Xunit;
using Neon.Xunit.Hive;

using Xunit;

namespace TestHive
{
    public partial class Test_HiveTrafficDirector : IClassFixture<HiveFixture>
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Cache_Purge()
        {
            var trafficManager = hive.PublicTraffic;
            var network        = HiveConst.PublicNetwork;
            var proxyPort      = HiveHostPorts.ProxyPublicHttp;
            var hostname       = "vegomatic.test";
            var serviceName    = "vegomatic";

            // We're going to configure a Vegomatic test instance with a 
            // caching load balancer rule and then fetch several different
            // requests thru the cache, verify that the responses were 
            // cache hits.  Then we'll test various purge patterns and
            // verify that subsequent requests will be cache misses.

            var manager = hive.GetReachableManager();

            using (var client = new TestHttpClient(disableConnectionReuse: true))
            {
                // Setup the client to query the [vegomatic] service through the
                // proxy without needing to configure a hive DNS entry.

                client.BaseAddress                = new Uri($"http://{manager.PrivateAddress}:{proxyPort}/");
                client.DefaultRequestHeaders.Host = testHostname;

                // Spin up a single [vegomatic] service instance.

                manager.SudoCommand($"docker service create --name vegomatic --network {network} --replicas 1 {vegomaticImage} test-server").EnsureSuccess();
                await WaitUntilReadyAsync(client.BaseAddress, hostname);

                // Configure the traffic director rule.

                var rule = new TrafficDirectorHttpRule()
                {
                    Name         = "vegomatic",
                    CheckExpect  = "status 200",
                    CheckSeconds = 1,
                    Cache        = new TrafficDirectorHttpCache() { Enabled = true }
                };

                rule.Frontends.Add(
                    new TrafficDirectorHttpFrontend()
                    {
                        Host      = hostname,
                        ProxyPort = proxyPort
                    });

                rule.Backends.Add(
                    new TrafficDirectorHttpBackend()
                    {
                        Server = serviceName,
                        Port   = 80
                    });

                trafficManager.SetRule(rule);

                // Submit several requests to preload the cache.
            }
        }
    }
}
