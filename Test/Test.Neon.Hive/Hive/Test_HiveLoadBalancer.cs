//-----------------------------------------------------------------------------
// FILE:	    Test_HiveLoadBalancer.cs
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
using Neon.Hive;
using Neon.Xunit;
using Neon.Xunit.Hive;

using Xunit;

namespace TestHive
{
    public class Test_HiveLoadBalancer : IClassFixture<HiveFixture>
    {
        private HiveFixture hiveFixture;
        private HiveProxy hive;

        public Test_HiveLoadBalancer(HiveFixture fixture)
        {
            if (!fixture.LoginAndInitialize())
            {
                fixture.Reset();
            }

            this.hiveFixture = fixture;
            this.hive = fixture.Hive;
        }

        /// <summary>
        /// Waits for the a remote proxy and origin to report being ready.
        /// </summary>
        /// <param name="baseUri">The base URI.</param>
        /// <param name="hostname">The target hostname.</param>
        private async Task WaitUntilReadyAsync(Uri baseUri, string hostname)
        {
            using (var client = new TestHttpClient(disableConnectionReuse: false))
            {
                client.BaseAddress                = baseUri;
                client.DefaultRequestHeaders.Host = hostname;

                await NeonHelper.WaitForAsync(
                    async () =>
                    {
                        var response = await client.GetAsync("/");

                        return response.IsSuccessStatusCode;
                    },
                    timeout: TimeSpan.FromMinutes(5),
                    pollTime: TimeSpan.FromMilliseconds(100));
            }

            // Wait a few more seconds to be safe.

            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// Determines whether a response was delivered via Varnish.
        /// </summary>
        /// <param name="response">The response.</param>
        /// <returns><c>true</c> when the response was delivered via Varnish.</returns>
        private bool ViaVarnish(HttpResponseMessage response)
        {
            // The [X-Varnish] header will be present if Varnish delivered the response.

            return response.Headers.Contains("X-Varnish");
        }

        /// <summary>
        /// Determines whether a response was delivered via Varnish and was a cache hit.
        /// </summary>
        /// <param name="response">The response.</param>
        /// <returns><c>true</c> for a cache hit.</returns>
        private bool CacheHit(HttpResponseMessage response)
        {
            // The [X-Varnish] header will be present and will include two
            // space separated integer IDs when the reponse was returned 
            // from the cache.

            if (response.Headers.TryGetValues("X-Varnish", out var values))
            {
                var value  = values.Single().Trim();
                var fields = value.Split(' ');

                return fields.Length == 2 && int.TryParse(fields[0], out var v1) && int.TryParse(fields[1], out var v2);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Verify that we can create a public load balancer rule for a 
        /// site on the public port using a specific hostname and then
        /// verify that that the load balancer actual works by spinning
        /// up a [vegomatic] based service to accept the traffic.
        /// </summary>
        /// <param name="testName">Simple name (without spaces) used to ensure that URIs cached for different tests won't conflict.</param>
        /// <param name="proxyPort">The inbound proxy port.</param>
        /// <param name="network">The proxy network.</param>
        /// <param name="loadBalancerManager">The load balancer manager.</param>
        /// <param name="useCache">Optionally enable caching and verify.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task TestHttpRule(string testName, int proxyPort, string network, LoadBalancerManager loadBalancerManager, bool useCache = false)
        {
            // Append a GUID to the test name to ensure that we won't
            // conflict with what any previous test runs may have loaded
            // into the cache.

            testName += "-" + Guid.NewGuid().ToString("D");

            // Verify that we can create a public load balancer rule for a 
            // site on the public port using a specific hostname and then
            // verify that that the load balancer actual works by spinning
            // up a [vegomatic] based service to accept the traffic.

            var vegomaticImage = $"nhive/vegomatic:{ThisAssembly.Git.Branch}-latest";
            var hostname       = "vegomatic.test";
            var queryCount     = 100;
            var manager        = hive.GetReachableManager();

            manager.Connect();

            using (var client = new TestHttpClient(disableConnectionReuse: true))
            {
                // Setup the client to query the [vegomatic] service through the
                // proxy without needing to configure a hive DNS entry.

                client.BaseAddress                = new Uri($"http://{manager.PrivateAddress}:{proxyPort}/");
                client.DefaultRequestHeaders.Host = hostname;

                // The test should start without any non-system rules.

                Assert.Empty(loadBalancerManager.ListRules(r => !r.System));

                // Configure the load balancer rule.

                var rule = new LoadBalancerHttpRule()
                {
                    Name         = "vegomatic",
                    CheckExpect  = "status 200",
                    CheckSeconds = 1,
                };

                if (useCache)
                {
                    rule.Cache = new LoadBalancerHttpCache() { Enabled = true };
                }

                rule.Frontends.Add(
                    new LoadBalancerHttpFrontend()
                    {
                        Host      = hostname,
                        ProxyPort = proxyPort
                    });

                rule.Backends.Add(
                    new LoadBalancerHttpBackend()
                    {
                        Server = "vegomatic",
                        Port   = 80
                    });

                loadBalancerManager.SetRule(rule);

                // Spin up a single [vegomatic] service with a single instance that will
                // return the instance UUID.  We're also going to configure this to set the
                // [Expires] header to a date 60 seconds in the future so we can verify that
                // caching is working.

                manager.SudoCommand($"docker service create --name vegomatic --network {network} --replicas 1 {vegomaticImage} test-server").EnsureSuccess();
                await WaitUntilReadyAsync(client.BaseAddress, hostname);

                // Query the service several times to verify that we get a response and 
                // also that all of the responses are the same (because we have only
                // a single [vegomatic] instance returning its UUID).
                //
                // We're going to use a different URL for each request so that we 
                // won't see any cache hits.

                var uniqueResponses = new HashSet<string>();
                var viaVarnish      = false;
                var cacheHit        = false;

                for (int i = 0; i < queryCount; i++)
                {
                    var response = await client.GetAsync($"/{testName}/pass-1/{i}?body=server-id&expires=60");

                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                    if (ViaVarnish(response))
                    {
                        viaVarnish = true;
                    }

                    if (CacheHit(response))
                    {
                        cacheHit = true;
                    }

                    var body = await response.Content.ReadAsStringAsync();

                    if (!uniqueResponses.Contains(body))
                    {
                        uniqueResponses.Add(body);
                    }
                }

                Assert.Single(uniqueResponses);

                if (useCache)
                {
                    // [viaVarnish] should be TRUE because we're routing through the cache.

                    Assert.True(viaVarnish);

                    // [cacheHit] should be FALSE because we used a unique URI for each request.

                    Assert.False(cacheHit);
                }
                else
                {
                    // [viaVarnish] and [cacheHit] should both be FALSE because we're not caching.

                    Assert.False(viaVarnish);
                    Assert.False(cacheHit);
                }

                if (useCache)
                {
                    // Repeat the test with the same URLs as last time and verify that
                    // we see cache hits this time.

                    viaVarnish = false;
                    cacheHit   = false;

                    for (int i = 0; i < queryCount; i++)
                    {
                        var response = await client.GetAsync($"/{testName}/pass-1/{i}?body=server-id&expires=60");

                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                        if (ViaVarnish(response))
                        {
                            viaVarnish = true;
                        }

                        if (CacheHit(response))
                        {
                            cacheHit = true;
                        }

                        var body = await response.Content.ReadAsStringAsync();

                        if (!uniqueResponses.Contains(body))
                        {
                            uniqueResponses.Add(body);
                        }
                    }

                    Assert.True(viaVarnish);
                    Assert.True(cacheHit);
                }

                // Spinup a second replica and repeat the query test to verify 
                // that we see two unique responses.
                //
                // Note that we're going to pass a new set of URLs to avoid having 
                // any responses cached so we'll end up seeing all of the IDs.
                //
                // Note also that we need to perform these requests in parallel
                // to try to force Varnish to establish more than one connection
                // to the [vegomatic] service.  If we don't do this, Varnish will
                // establish a single connection to one of the service instances 
                // and keep sending traffic there resulting in us seeing only
                // one response UUID.

                manager.SudoCommand($"docker service update --replicas 2 vegomatic").EnsureSuccess();
                await WaitUntilReadyAsync(client.BaseAddress, hostname);

                // Reset the response info and do the requests.

                uniqueResponses.Clear();
                viaVarnish = false;
                cacheHit   = false;

                var tasks = new List<Task>();
                var uris  = new List<string>();

                for (int i = 0; i < queryCount; i++)
                {
                    uris.Add($"/{testName}/pass-2/{i}?body=server-id&expires=60&delay=0.250");
                }

                foreach (var uri in uris)
                {
                    tasks.Add(Task.Run(
                        async () =>
                        {
                            var response = await client.GetAsync(uri);
                            var body     = await response.Content.ReadAsStringAsync();

                            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                            if (ViaVarnish(response))
                            {
                                viaVarnish = true;
                            }

                            if (CacheHit(response))
                            {
                                cacheHit = true;
                            }

                            lock (uniqueResponses)
                            {
                                if (!uniqueResponses.Contains(body))
                                {
                                    uniqueResponses.Add(body);
                                }
                            }
                        }));
                }

                await NeonHelper.WaitAllAsync(tasks);

                if (useCache)
                {
                    // [viaVarnish] should be TRUE because we're routing through the cache.

                    Assert.True(viaVarnish);

                    // [cacheHit] should be FALSE because we used a unique URI for each request.

                    Assert.False(cacheHit);
                }
                else
                {
                    // [viaVarnish] and [cacheHit] should both be FALSE because we're not caching.

                    Assert.False(viaVarnish);
                    Assert.False(cacheHit);
                }

                Assert.Equal(2, uniqueResponses.Count);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task HttpRule_Public()
        {
            await TestHttpRule("http-public", HiveHostPorts.ProxyPublicHttp, HiveConst.PublicNetwork, hive.PublicLoadBalancer);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task HttpRule_Private()
        {
            await TestHttpRule("http-public", HiveHostPorts.ProxyPrivateHttp, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task HttpRule_PublicCached()
        {
            await TestHttpRule("http-public-cached", HiveHostPorts.ProxyPublicHttp, HiveConst.PublicNetwork, hive.PublicLoadBalancer, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task HttpRule_PrivateCached()
        {
            await TestHttpRule("http-public-cached", HiveHostPorts.ProxyPrivateHttp, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer, useCache: true);
        }
    }
}
