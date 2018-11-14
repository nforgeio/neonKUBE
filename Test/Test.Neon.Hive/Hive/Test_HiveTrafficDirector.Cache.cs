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
            var proxyPort      = 80;
            var uuid           = Guid.NewGuid().ToString("D");  // Use to avoid cache conflicts from previous test runs.

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

                client.BaseAddress = new Uri($"http://{manager.PrivateAddress}:{proxyPort}/");

                // Configure the traffic director rules (with caching enabled).

                for (int serverId = 0; serverId < 2; serverId++)
                {
                    var rule = new TrafficDirectorHttpRule()
                    {
                        Name         = $"vegomatic-{serverId}",
                        CheckExpect  = "status 200",
                        CheckSeconds = 1,
                        Cache        = new TrafficDirectorHttpCache() { Enabled = true }
                    };

                    rule.Frontends.Add(
                        new TrafficDirectorHttpFrontend()
                        {
                            Host      = $"vegomatic-{serverId}",
                            ProxyPort = proxyPort
                        });

                    rule.Backends.Add(
                        new TrafficDirectorHttpBackend()
                        {
                            Server = $"vegomatic-{serverId}",
                            Port   = 80
                        });

                    trafficManager.SetRule(rule);
                }

                // Spin up a two [vegomatic] service instances [vegomatic-0] and [vegomatic-1]
                // so that we can ensure that purging one origin server's content doesn't impact
                // the other's cached content.

                NeonHelper.WaitForParallel(
                    new Action[]
                    {
                        () =>
                        {
                            manager.SudoCommand($"docker service create --name vegomatic-0 --network {network} --replicas 1 {vegomaticImage} test-server").EnsureSuccess();
                            WaitUntilReadyAsync(client.BaseAddress, "vegomatic-0").Wait();
                        },
                        () =>
                        {
                            manager.SudoCommand($"docker service create --name vegomatic-1 --network {network} --replicas 1 {vegomaticImage} test-server").EnsureSuccess();
                            WaitUntilReadyAsync(client.BaseAddress, "vegomatic-1").Wait();
                        }
                    });

                // Submit several requests to preload the cache for both origin servers.
                // and verify that the items were cached.

                HttpRequestMessage      request;
                HttpResponseMessage     response;

                var expireSeconds = 300;

                var testUris = new string[]
                {
                    $"/{uuid}/test0.jpg?expires={expireSeconds}",
                    $"/{uuid}/test1.jpg?expires={expireSeconds}",
                    $"/{uuid}/test2.jpg?expires={expireSeconds}",
                    $"/{uuid}/test0.png?expires={expireSeconds}",
                    $"/{uuid}/test1.png?expires={expireSeconds}",
                    $"/{uuid}/test2.png?expires={expireSeconds}",
                    $"/{uuid}/foo/test.htm?expires={expireSeconds}",
                    $"/{uuid}/foo/test.jpg?expires={expireSeconds}",
                    $"/{uuid}/bar/test.htm?expires={expireSeconds}",
                    $"/{uuid}/bar/test.jpg?expires={expireSeconds}",
                };

                for (int serverId = 0; serverId < 2; serverId++)
                {
                    var originHost = $"vegomatic-{serverId}";

                    foreach (var uri in testUris)
                    {
                        // Load the item into the cache.

                        request              = new HttpRequestMessage(HttpMethod.Get, $"{uri}");
                        request.Headers.Host = originHost;

                        response = await client.SendAsync(request);

                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        Assert.True(ViaVarnish(response));

                        // Ensure that it was actually cached.

                        request = new HttpRequestMessage(HttpMethod.Get, $"{uri}");

                        request.Headers.Host = originHost;

                        response = await client.SendAsync(request);

                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        Assert.True(ViaVarnish(response));
                        Assert.True(CacheHit(response));
                    }
                }

                //-------------------------------------------------------------
                // We're going to be purging items from [vegomatic-0], leaving the [vegomatic-1]
                // cache alone for now.

                var purgeWaitTime = TimeSpan.FromSeconds(2);     // Time to wait while cache purging completes.

                // Purge a single URI and verify.

                trafficManager.Purge($"http://vegomatic-0/{uuid}/test0.jpg?expires={expireSeconds}");
                await Task.Delay(purgeWaitTime);

                request              = new HttpRequestMessage(HttpMethod.Get, $"/{uuid}/test0.jpg?expires={expireSeconds}");
                request.Headers.Host = "vegomatic-0";

                response = await client.SendAsync(request);

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True(ViaVarnish(response));
                Assert.False(CacheHit(response));   // Shouldn't be a cache hit because we just purged it.
            }
        }
    }
}
