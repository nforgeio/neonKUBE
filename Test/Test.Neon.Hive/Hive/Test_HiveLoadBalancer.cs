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
using Neon.Cryptography;
using Neon.Hive;
using Neon.Xunit;
using Neon.Xunit.Hive;

using Xunit;

namespace TestHive
{
    public class Test_HiveLoadBalancer : IClassFixture<HiveFixture>
    {
        private const string            testHostname = "vegomatic.test";
        private static TlsCertificate   certificate;

        private HiveFixture     hiveFixture;
        private HiveProxy       hive;
        private string          vegomaticImage = $"nhive/vegomatic:{ThisAssembly.Git.Branch}-latest";

        public Test_HiveLoadBalancer(HiveFixture fixture)
        {
            if (!fixture.LoginAndInitialize())
            {
                fixture.Reset();
            }

            this.hiveFixture = fixture;
            this.hive        = fixture.Hive;

            // Generate a self-signed certificate we can reuse across tests if
            // we haven't already created one.

            if (certificate == null)
            {
                certificate = TlsCertificate.CreateSelfSigned(testHostname);
            }
        }

        /// <summary>
        /// Waits for the a remote proxy and origin to report being ready.
        /// </summary>
        /// <param name="baseUri">The base URI.</param>
        /// <param name="hostname">The target hostname.</param>
        /// <param name="allowSelfSignedCerts">Optionally allow self-signed certificates.</param>
        private async Task WaitUntilReadyAsync(Uri baseUri, string hostname, bool allowSelfSignedCerts = false)
        {
            TestHttpClient client;

            if (allowSelfSignedCerts)
            {
                // Allow self-signed certificates.

                var handler = new HttpClientHandler()
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                client = new TestHttpClient(disableConnectionReuse: true, handler: handler, disposeHandler: true);
            }
            else
            {
                client = new TestHttpClient(disableConnectionReuse: false);
            }

            using (client)
            {
                client.BaseAddress                = baseUri;
                client.DefaultRequestHeaders.Host = hostname;

                await NeonHelper.WaitForAsync(
                    async () =>
                    {
                        var response = await client.GetAsync("/");

                        return response.IsSuccessStatusCode;
                    },
                    timeout: TimeSpan.FromMinutes(2),
                    pollTime: TimeSpan.FromMilliseconds(100));
            }
        }

        //---------------------------------------------------------------------
        // HTTP rule verification:

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
        /// Verify that we can create an HTTP load balancer rule for a 
        /// site on the public port using a specific hostname and then
        /// verify that that the load balancer actual works by spinning
        /// up a [vegomatic] based service to accept the traffic.
        /// </summary>
        /// <param name="testName">Simple name (without spaces) used to ensure that URIs cached for different tests won't conflict.</param>
        /// <param name="proxyPort">The inbound proxy port.</param>
        /// <param name="network">The proxy network.</param>
        /// <param name="loadBalancerManager">The load balancer manager.</param>
        /// <param name="useCache">Optionally enable caching and verify.</param>
        /// <param name="useIPAddress">Optionally uses an IP addrress rather than a hostname for the rule frontend.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task TestHttpRule(string testName, int proxyPort, string network, LoadBalancerManager loadBalancerManager, bool useCache = false, bool useIPAddress = false)
        {
            // Append a GUID to the test name to ensure that we won't
            // conflict with what any previous test runs may have loaded
            // into the cache.

            testName += "-" + Guid.NewGuid().ToString("D");

            // Verify that we can create an HTTP load balancer rule for a 
            // site on the public port using a specific hostname and then
            // verify that that the load balancer actual works by spinning
            // up a [vegomatic] based service to accept the traffic.

            var queryCount = 100;
            var manager    = hive.GetReachableManager();
            var hostname   = useIPAddress ? manager.PrivateAddress.ToString() : testHostname;

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
                        Host      = useIPAddress ? null : hostname,
                        ProxyPort = proxyPort
                    });

                rule.Backends.Add(
                    new LoadBalancerHttpBackend()
                    {
                        Server = "vegomatic",
                        Port   = 80
                    });

                loadBalancerManager.SetRule(rule);

                // Spin up a single [vegomatic] service instance.

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

        /// <summary>
        /// Verify that we can create an HTTP load balancer rule for a 
        /// site on the public port using a specific hostname and then
        /// verify that that the load balancer actual works by spinning
        /// up a [vegomatic] based service to accept the traffic.
        /// </summary>
        /// <param name="testName">Simple name (without spaces) used to ensure that URIs cached for different tests won't conflict.</param>
        /// <param name="hostnames">The hostnames to be used for .</param>
        /// <param name="proxyPort">The inbound proxy port.</param>
        /// <param name="network">The proxy network.</param>
        /// <param name="loadBalancerManager">The load balancer manager.</param>
        /// <param name="useCache">Optionally enable caching and verify.</param>
        /// <param name="useIPAddress">Optionally uses an IP addrress rather than a hostname for the rule frontend.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task TestHttpMultipleHosts(string testName, string[] hostnames, int proxyPort, string network, LoadBalancerManager loadBalancerManager, bool useCache = false)
        {
            Covenant.Requires<ArgumentNullException>(hostnames != null && hostnames.Length > 0);

            // Append a GUID to the test name to ensure that we won't
            // conflict with what any previous test runs may have loaded
            // into the cache.

            testName += "-" + Guid.NewGuid().ToString("D");

            // Verify that we can create an HTTP load balancer rule for a 
            // site on the public port using a specific hostname and then
            // verify that that the load balancer actual works by spinning
            // up a [vegomatic] based service to accept the traffic.

            var queryCount = 100;
            var manager    = hive.GetReachableManager();
            var proxyUri   = new Uri($"http://{manager.PrivateAddress}:{proxyPort}/");

            manager.Connect();

            using (var client = new TestHttpClient(disableConnectionReuse: true))
            {
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

                foreach (var hostname in hostnames)
                {
                    rule.Frontends.Add(
                        new LoadBalancerHttpFrontend()
                        {
                            Host      = hostname,
                            ProxyPort = proxyPort
                        });
                }

                rule.Backends.Add(
                    new LoadBalancerHttpBackend()
                    {
                        Server = "vegomatic",
                        Port   = 80
                    });

                loadBalancerManager.SetRule(rule);

                // Spin up a single [vegomatic] service instance.

                manager.SudoCommand($"docker service create --name vegomatic --network {network} --replicas 1 {vegomaticImage} test-server").EnsureSuccess();
                await WaitUntilReadyAsync(proxyUri, hostnames.First());

                // Query the service several times for each hostname to verify that we
                // get a response and  also that all of the responses are the same
                // (because we have only a single [vegomatic] instance returning its UUID).
                //
                // We're going to use a different URL for each request so that we 
                // won't see any cache hits.

                foreach (var hostname in hostnames)
                {
                    var uniqueResponses = new HashSet<string>();
                    var viaVarnish      = false;
                    var cacheHit        = false;

                    client.BaseAddress                = proxyUri;
                    client.DefaultRequestHeaders.Host = hostname;

                    for (int i = 0; i < queryCount; i++)
                    {
                        var response = await client.GetAsync($"/{testName}/{hostname}/pass-1/{i}?body=server-id&expires=60");

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
                            var response = await client.GetAsync($"/{testName}/{hostname}/pass-1/{i}?body=server-id&expires=60");

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
                }

                // Spinup a second replica and repeat the query test for each hostname
                // to verify that we see two unique responses.
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
                await WaitUntilReadyAsync(proxyUri, hostnames.First());

                foreach (var hostname in hostnames)
                {
                    var uniqueResponses = new HashSet<string>();
                    var viaVarnish      = false;
                    var cacheHit        = false;
                    var tasks           = new List<Task>();
                    var uris            = new List<string>();

                    client.BaseAddress                = proxyUri;
                    client.DefaultRequestHeaders.Host = hostname;

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
                                var body = await response.Content.ReadAsStringAsync();

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
        }

        //---------------------------------------
        // HTTP: PUBLIC load balancer tests:

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_DefaultPort()
        {
            await TestHttpRule("http-public-defaultport", HiveHostPorts.ProxyPublicHttp, HiveConst.PublicNetwork, hive.PublicLoadBalancer);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_NonDefaultPort()
        {
            await TestHttpRule("http-public-nondefaultport", HiveHostPorts.ProxyPublicLastUserPort, HiveConst.PublicNetwork, hive.PublicLoadBalancer);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_NoHostname()
        {
            await TestHttpRule("http-public-nohostname", HiveHostPorts.ProxyPublicLastUserPort, HiveConst.PublicNetwork, hive.PublicLoadBalancer, useIPAddress: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_Cached_DefaultPort()
        {
            await TestHttpRule("http-public-cached-defaultport", HiveHostPorts.ProxyPublicHttp, HiveConst.PublicNetwork, hive.PublicLoadBalancer, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_Cached_NonDefaultPort()
        {
            await TestHttpRule("http-public-cached-nondefaultport", HiveHostPorts.ProxyPublicLastUserPort, HiveConst.PublicNetwork, hive.PublicLoadBalancer, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_Cached_NoHostname()
        {
            await TestHttpRule("http-public-cached-nohostname", HiveHostPorts.ProxyPublicLastUserPort, HiveConst.PublicNetwork, hive.PublicLoadBalancer, useCache: true, useIPAddress: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_MultiHostnames_DefaultPort()
        {
            await TestHttpMultipleHosts("http-public-multihostnames-defaultport", new string[] { "vegomatic1.test", "vegomatic2.test" }, HiveHostPorts.ProxyPublicHttp, HiveConst.PublicNetwork, hive.PublicLoadBalancer);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_MultiHostnames_NondefaultPort()
        {
            await TestHttpMultipleHosts("http-public-multihostnames-nondefaultport", new string[] { "vegomatic1.test", "vegomatic2.test" }, HiveHostPorts.ProxyPublicLastUserPort, HiveConst.PublicNetwork, hive.PublicLoadBalancer);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_MultiHostnames_DefaultPort_Cached()
        {
            await TestHttpMultipleHosts("http-public-multihostnames-defaultport", new string[] { "vegomatic1.test", "vegomatic2.test" }, HiveHostPorts.ProxyPublicHttp, HiveConst.PublicNetwork, hive.PublicLoadBalancer, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_MultiHostnames_NondefaultPort_Cached()
        {
            await TestHttpMultipleHosts("http-public-multihostnames-nondefaultport", new string[] { "vegomatic1.test", "vegomatic2.test" }, HiveHostPorts.ProxyPublicLastUserPort, HiveConst.PublicNetwork, hive.PublicLoadBalancer, useCache: true);
        }

        //---------------------------------------
        // HTTP: PRIVATE load balancer tests:

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_DefaultPort()
        {
            await TestHttpRule("http-private-defaultport", HiveHostPorts.ProxyPrivateHttp, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_NonDefaultPort()
        {
            await TestHttpRule("http-private-nondefaultport", HiveHostPorts.ProxyPrivateLastUserPort, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_NoHostname()
        {
            await TestHttpRule("http-private-nohostname", HiveHostPorts.ProxyPrivateLastUserPort, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer, useIPAddress: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Cached_DefaultPort()
        {
            await TestHttpRule("http-private-cached-defaultport", HiveHostPorts.ProxyPrivateHttp, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Cached_NonDefaultPort()
        {
            await TestHttpRule("http-private-cached-nondefaultport", HiveHostPorts.ProxyPrivateLastUserPort, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Cached_NoHostname()
        {
            await TestHttpRule("http-private-cached-nohostname", HiveHostPorts.ProxyPrivateLastUserPort, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer, useCache: true, useIPAddress: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_MultiHostnames_DefaultPort()
        {
            await TestHttpMultipleHosts("http-private-multihostnames-defaultport", new string[] { "vegomatic1.test", "vegomatic2.test" }, HiveHostPorts.ProxyPrivateHttp, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_MultiHostnames_NondefaultPort()
        {
            await TestHttpMultipleHosts("http-private-multihostnames-nondefaultport", new string[] { "vegomatic1.test", "vegomatic2.test" }, HiveHostPorts.ProxyPrivateLastUserPort, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_MultiHostnames_DefaultPort_Cached()
        {
            await TestHttpMultipleHosts("http-private-multihostnames-defaultport", new string[] { "vegomatic1.test", "vegomatic2.test" }, HiveHostPorts.ProxyPrivateHttp, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_MultiHostnames_NondefaultPort_Cached()
        {
            await TestHttpMultipleHosts("http-private-multihostnames-nondefaultport", new string[] { "vegomatic1.test", "vegomatic2.test" }, HiveHostPorts.ProxyPrivateLastUserPort, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer, useCache: true);
        }

        //---------------------------------------------------------------------
        // HTTPS rule verification:

        /// <summary>
        /// Verify that we can create an HTTPS load balancer rule for a 
        /// site on the public port using a specific hostname and then
        /// verify that that the load balancer actual works by spinning
        /// up a [vegomatic] based service to accept the traffic.
        /// </summary>
        /// <param name="testName">Simple name (without spaces).</param>
        /// <param name="proxyPort">The inbound proxy port.</param>
        /// <param name="network">The proxy network.</param>
        /// <param name="loadBalancerManager">The load balancer manager.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task TestHttpsRule(string testName, int proxyPort, string network, LoadBalancerManager loadBalancerManager)
        {
            // Verify that we can create an HTTPS load balancer rule for a 
            // site on the public port using a specific hostname and then
            // verify that that the load balancer actual works by spinning
            // up a [vegomatic] based service to accept the traffic.

            var queryCount = 100;
            var manager    = hive.GetReachableManager();

            manager.Connect();

            // We need the test hostname to point to the manager's private address
            // so we can submit HTTPS requests there.

            hiveFixture.LocalMachineHosts.AddHostAddress(testHostname, manager.PrivateAddress.ToString());

            // Allow self-signed certificates.

            var handler = new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            using (var client = new TestHttpClient(disableConnectionReuse: true, handler: handler, disposeHandler: true))
            {
                client.BaseAddress                = new Uri($"https://{testHostname}:{proxyPort}/");
                client.DefaultRequestHeaders.Host = testHostname;

                // The test should start without any non-system rules and certificates.

                Assert.Empty(loadBalancerManager.ListRules(r => !r.System));
                Assert.Empty(hive.Certificate.List());

                // Add the test certificate.

                hive.Certificate.Set("test-load-balancer", certificate);

                // Configure the load balancer rule.

                var rule = new LoadBalancerHttpRule()
                {
                    Name         = "vegomatic",
                    CheckExpect  = "status 200",
                    CheckSeconds = 1
                };

                rule.Frontends.Add(
                    new LoadBalancerHttpFrontend()
                    {
                        Host      = testHostname,
                        ProxyPort = proxyPort,
                        CertName  = "test-load-balancer"
                    });

                rule.Backends.Add(
                    new LoadBalancerHttpBackend()
                    {
                        Server = "vegomatic",
                        Port   = 80
                    });

                loadBalancerManager.SetRule(rule);

                // Spin up a single [vegomatic] service instance.

                manager.SudoCommand($"docker service create --name vegomatic --network {network} --replicas 1 {vegomaticImage} test-server").EnsureSuccess();
                await WaitUntilReadyAsync(client.BaseAddress, testHostname, allowSelfSignedCerts: true);

                // Query the service several times to verify that we get a response and 
                // also that all of the responses are the same (because we have only
                // a single [vegomatic] instance returning its UUID).

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

                // [viaVarnish] and [cacheHit] should both be FALSE because caching is not supported for TLS.

                Assert.False(viaVarnish);
                Assert.False(cacheHit);

                // Spinup a second replica and repeat the query test to verify 
                // that we see two unique responses.
                //
                // Note also that we need to perform these requests in parallel
                // to try to force HAProxy to establish more than one connection
                // to the [vegomatic] service.  If we don't do this, HAProxy may
                // establish a single connection to one of the service instances 
                // and keep sending traffic there resulting in us seeing only
                // one response UUID.

                manager.SudoCommand($"docker service update --replicas 2 vegomatic").EnsureSuccess();
                await WaitUntilReadyAsync(client.BaseAddress, testHostname, allowSelfSignedCerts: true);

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
                            var body = await response.Content.ReadAsStringAsync();

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

                // [viaVarnish] and [cacheHit] should both be FALSE because we're not caching.

                Assert.False(viaVarnish);
                Assert.False(cacheHit);

                Assert.Equal(2, uniqueResponses.Count);
            }
        }

        //---------------------------------------
        // HTTPS: PUBLIC load balancer tests:

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Public_DefaultPort()
        {
            await TestHttpsRule("https-public-defaultport", HiveHostPorts.ProxyPublicHttps, HiveConst.PublicNetwork, hive.PublicLoadBalancer);
        }

        //---------------------------------------
        // HTTPS: PRIVATE load balancer tests:

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Private_DefaultPort()
        {
            await TestHttpsRule("https-private-defaultport", HiveHostPorts.ProxyPrivateHttps, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer);
        }
    }
}
