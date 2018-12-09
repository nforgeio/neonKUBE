//-----------------------------------------------------------------------------
// FILE:	    Test_HiveTrafficManager.Public.Https.cs
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
    public partial class Test_HiveTrafficManager : IClassFixture<HiveFixture>
    {
        //---------------------------------------------------------------------
        // Private types

        private class CacheInfo
        {
            public bool ViaVarnish { get; set; }
            public bool CacheHit { get; set; }
        }

        /// <summary>
        /// Performs a GET request on a URI and returns its caching status.
        /// </summary>
        /// <param name="client">The HTTP client to use for submitting the request.</param>
        /// <param name="uriString">The URI.</param>
        /// <returns>The <see cref="CacheInfo"/> for the response.</returns>
        private async Task<CacheInfo> GetCachingStatusAsync(TestHttpClient client, string uriString)
        {
            var uri = new Uri(uriString);
            var request = new HttpRequestMessage(HttpMethod.Get, uri.PathAndQuery);

            request.Headers.Host = uri.Host;

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            return new CacheInfo()
            {
                ViaVarnish = ViaVarnish(response),
                CacheHit = CacheHit(response)
            };
        }

        //---------------------------------------------------------------------
        // Implementation

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Cache_Purge()
        {
            var trafficManager = hive.PublicTraffic;
            var network        = HiveConst.PublicNetwork;
            var proxyPort      = 80;
            var uuid           = Guid.NewGuid().ToString("D");  // Used to avoid cache conflicts from previous test runs.

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

                // Configure the traffic manager rules (with caching enabled).

                for (int serverId = 0; serverId < 2; serverId++)
                {
                    var rule = new TrafficHttpRule()
                    {
                        Name = $"vegomatic-{serverId}",
                        CheckExpect = "status 200",
                        CheckSeconds = 1,
                        Cache = new TrafficHttpCache() { Enabled = true }
                    };

                    rule.Frontends.Add(
                        new TrafficHttpFrontend()
                        {
                            Host = $"vegomatic-{serverId}",
                            ProxyPort = proxyPort
                        });

                    rule.Backends.Add(
                        new TrafficHttpBackend()
                        {
                            Server = $"vegomatic-{serverId}",
                            Port = 80
                        });

                    trafficManager.SetRule(rule);
                }

                // Spin up a two [vegomatic] service instances [vegomatic-0] and [vegomatic-1]
                // so that we can ensure that purging one origin server's content doesn't impact
                // the other's cached content.  The default response expiration time will be
                // configured as 300 seconds (5 minutes) so we can test cache purging.

                var expireSeconds = 300;

                NeonHelper.WaitForParallel(
                    new Action[]
                    {
                        () =>
                        {
                            manager.SudoCommand($"docker service create --name vegomatic-0 --network {network} --replicas 1 {vegomaticImage} test-server expires={expireSeconds}").EnsureSuccess();
                            WaitUntilReadyAsync(client.BaseAddress, "vegomatic-0").Wait();
                        },
                        () =>
                        {
                            manager.SudoCommand($"docker service create --name vegomatic-1 --network {network} --replicas 1 {vegomaticImage} test-server expires={expireSeconds}").EnsureSuccess();
                            WaitUntilReadyAsync(client.BaseAddress, "vegomatic-1").Wait();
                        }
                    });

                // Submit several requests to preload the cache for both origin servers.
                // and verify that the items were cached.

                CacheInfo status;

                var testUris = new string[]
                {
                    $"/{uuid}/test.htm",
                    $"/{uuid}/test0.jpg",
                    $"/{uuid}/test1.jpg",
                    $"/{uuid}/test2.jpg",
                    $"/{uuid}/test0.png",
                    $"/{uuid}/test1.png",
                    $"/{uuid}/test2.png",
                    $"/{uuid}/foo/test.htm",
                    $"/{uuid}/foo/test.jpg",
                    $"/{uuid}/foo/test.png",
                    $"/{uuid}/bar/test.htm",
                    $"/{uuid}/bar/test.jpg",
                    $"/{uuid}/bar/test.png",
                };

                for (int serverId = 0; serverId < 2; serverId++)
                {
                    var host = $"vegomatic-{serverId}";

                    foreach (var uri in testUris)
                    {
                        // Load the item into the cache.

                        status = await GetCachingStatusAsync(client, $"http://{host}:80{uri}");

                        Assert.True(status.ViaVarnish);

                        // Ensure that it was actually cached.

                        status = await GetCachingStatusAsync(client, $"http://{host}:80{uri}");
                        Assert.True(status.ViaVarnish);
                        Assert.True(status.CacheHit);
                    }
                }

                //-------------------------------------------------------------
                // We're going to be purging items from [vegomatic-0], leaving the [vegomatic-1]
                // cache alone for now.

                var purgeWaitTime = TimeSpan.FromSeconds(2);     // Time to wait while cache purging completes.

                // Purge a single URI and verify its no longer cached.

                trafficManager.Purge(new string[] { $"http://vegomatic-0:80/{uuid}/test0.jpg" });
                await Task.Delay(purgeWaitTime);

                status = await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/test0.jpg");

                Assert.True(status.ViaVarnish);
                Assert.False(status.CacheHit);  // Shouldn't be a cache hit because we just purged it.

                // Verify that a sample of the remaining [vegomatic-0] responses are still cached.

                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/test.htm")).CacheHit);
                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/test1.jpg")).CacheHit);
                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/test2.jpg")).CacheHit);
                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/test0.png")).CacheHit);
                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/test1.png")).CacheHit);
                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/test2.png")).CacheHit);
                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/foo/test.htm")).CacheHit);

                // Purge the top-level [*.png] files and verify that they are no longer cached.

                trafficManager.Purge(new string[] { $"http://vegomatic-0:80/{uuid}/*.png" });
                await Task.Delay(purgeWaitTime);

                Assert.False((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/test0.png")).CacheHit);
                Assert.False((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/test1.png")).CacheHit);
                Assert.False((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/test2.png")).CacheHit);

                // Verify that a sample of the remaining [vegomatic-0] responses are still cached.

                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/test.htm")).CacheHit);
                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/test1.jpg")).CacheHit);
                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/test2.jpg")).CacheHit);
                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/foo/test.htm")).CacheHit);
                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/foo/test.png")).CacheHit);

                // Use a glob pattern to purge all [*.jpg] responses at all nesting levels.

                trafficManager.Purge(new string[] { $"http://vegomatic-0:80/**/*.jpg" });
                await Task.Delay(purgeWaitTime);

                Assert.False((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/test0.jpg")).CacheHit);
                Assert.False((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/test1.jpg")).CacheHit);
                Assert.False((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/test2.jpg")).CacheHit);
                Assert.False((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/foo/test.jpg")).CacheHit);
                Assert.False((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/bar/test.jpg")).CacheHit);

                // Verify that the remaining [vegomatic-0] responses are still cached.

                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/test.htm")).CacheHit);
                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/foo/test.htm")).CacheHit);
                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/foo/test.png")).CacheHit);
                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/bar/test.htm")).CacheHit);
                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/bar/test.png")).CacheHit);

                // Use a glob pattern to purge all [vegomatic-0] responses.

                trafficManager.Purge(new string[] { $"http://vegomatic-0:80/**" });
                await Task.Delay(purgeWaitTime);

                Assert.False((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/test.htm")).CacheHit);
                Assert.False((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/foo/test.htm")).CacheHit);
                Assert.False((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/foo/test.png")).CacheHit);
                Assert.False((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/bar/test.htm")).CacheHit);
                Assert.False((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/bar/test.png")).CacheHit);

                //-------------------------------------------------------------
                // Verify that all the [vegomatic-0] purging didn't impact any of the cached [vegomatic-1] responses.

                foreach (var uri in testUris)
                {
                    Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-1:80{uri}")).CacheHit);
                }

                //-------------------------------------------------------------
                // Reload all responses into the cache and then do a purge ALL and verify.

                for (int serverId = 0; serverId < 2; serverId++)
                {
                    var host = $"vegomatic-{serverId}";

                    foreach (var uri in testUris)
                    {
                        await GetCachingStatusAsync(client, $"http://{host}:80{uri}");
                    }
                }

                trafficManager.PurgeAll();
                await Task.Delay(purgeWaitTime);

                for (int serverId = 0; serverId < 2; serverId++)
                {
                    var host = $"vegomatic-{serverId}";

                    foreach (var uri in testUris)
                    {
                        Assert.False((await GetCachingStatusAsync(client, $"http://{host}:80{uri}")).CacheHit);
                    }
                }

                //-------------------------------------------------------------
                // Test case-sensitive purge patterns.

                Assert.False((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/case.test")).CacheHit);
                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/case.test")).CacheHit);

                Assert.False((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/CASE.TEST")).CacheHit);
                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/CASE.TEST")).CacheHit);

                trafficManager.Purge(new string[] { $"http://vegomatic-0:80/{uuid}/case.test" }, caseSensitive: true);
                await Task.Delay(purgeWaitTime);

                Assert.False((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/case.test")).CacheHit);
                Assert.True((await GetCachingStatusAsync(client, $"http://vegomatic-0:80/{uuid}/CASE.TEST")).CacheHit);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Cache_Purge_Security()
        {
            var trafficManager = hive.PublicTraffic;
            var manager        = hive.GetReachableManager();
            var network        = HiveConst.PublicNetwork;
            var proxyPort      = 80;

            // Configure a traffic manager rule (with caching enabled).

            var rule = new TrafficHttpRule()
            {
                Name         = $"vegomatic",
                CheckExpect  = "status 200",
                CheckSeconds = 1,
                Cache        = new TrafficHttpCache() { Enabled = true }
            };

            rule.Frontends.Add(
                new TrafficHttpFrontend()
                {
                    Host      = $"vegomatic",
                    ProxyPort = proxyPort
                });

            rule.Backends.Add(
                new TrafficHttpBackend()
                {
                    Server = $"vegomatic",
                    Port   = 80
                });

            trafficManager.SetRule(rule);

            // Verify that non-local BAN requests are rejected.  This is important because
            // this will block BAN based DOS attacks.

            using (var client = new TestHttpClient(disableConnectionReuse: true))
            {
                client.BaseAddress = new Uri($"http://{manager.PrivateAddress}:{proxyPort}/");

                // Spin up a test [vegomatic] service.

                var expireSeconds = 300;

                manager.SudoCommand($"docker service create --name vegomatic --network {network} --replicas 1 {vegomaticImage} test-server expires={expireSeconds}").EnsureSuccess();
                await WaitUntilReadyAsync(client.BaseAddress, "vegomatic");

                // Load a response into the cache so we can verify that an external BAN
                // didn't actually purge anything.

                await GetCachingStatusAsync(client, "http://vegomatic:80/foo.txt");
                Assert.True((await GetCachingStatusAsync(client, "http://vegomatic:80/foo.txt")).CacheHit);

                // Verify that a BAN request submitted from outside of the [neon-proxy-cache]
                // container doesn't actually purge anything.
                //
                // Note that we're not going to see a 403 status code here because the HAProxy
                // rule is going to add an [X-Neon-Frontend] header and we've configured the
                // Varnish VCL to actually perform BANs only when this header is not present.

                var request = new HttpRequestMessage(new HttpMethod("BAN"), $"/");

                request.Headers.Host = "vegomatic";
                request.Headers.Add("X-Ban-All", "yes");

                await client.SendAsync(request);
                
                Assert.True((await GetCachingStatusAsync(client, "http://vegomatic:80/foo.txt")).CacheHit);

                // Now we're actually going to hit Varnish directly and verify that we
                // get a 403 response.  The trick here is that we need to update the
                // [neon-proxy-public-cache] service to publish port [61223] so we can
                // hit it externally.

                int externalPort = 61223;

                try
                {
                    manager.SudoCommand($"docker service update --publish-add {externalPort}:80 neon-proxy-public-cache");

                    // We need to reload the cached item because the cache was cleared when we updated [neon-proxy-public-cache].

                    await GetCachingStatusAsync(client, "http://vegomatic:80/foo.txt");
                    Assert.True((await GetCachingStatusAsync(client, "http://vegomatic:80/foo.txt")).CacheHit);

                    // Send the BAN request and verify that it was rejected.

                    request = new HttpRequestMessage(new HttpMethod("BAN"), $"http://{manager.PrivateAddress}:{externalPort}/");

                    request.Headers.Add("X-Ban-All", "yes");

                    var response = await client.SendAsync(request);

                    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

                    // Also confirm that the item wasn't purged.

                    Assert.True((await GetCachingStatusAsync(client, "http://vegomatic:80/foo.txt")).CacheHit);
                }
                finally
                {
                    // Remove the temporary test port.

                    manager.SudoCommand($"docker service update --publish-rm {externalPort}:80 neon-proxy-public-cache");
                }
            }
        }
    }
}
