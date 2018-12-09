//-----------------------------------------------------------------------------
// FILE:	    Test_HiveTrafficManager.Http.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        /// <summary>
        /// Verify that we can create an HTTP traffic manager rule for a 
        /// site on the proxy port using a specific hostname and then
        /// verify that that the traffic manager actually works by spinning
        /// up a [vegomatic] based service to accept the traffic.
        /// </summary>
        /// <param name="testName">Simple name (without spaces) used to ensure that URIs cached for different tests won't conflict.</param>
        /// <param name="proxyPort">The inbound proxy port.</param>
        /// <param name="network">The proxy network.</param>
        /// <param name="trafficManager">The traffic manager.</param>
        /// <param name="useCache">Optionally enable caching and verify.</param>
        /// <param name="serviceName">Optionally specifies the backend service name (defaults to <b>vegomatic</b>).</param>
        /// <param name="useIPAddress">Optionally uses an IP addrress rather than a hostname for the rule frontend.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task TestHttpRule(string testName, int proxyPort, string network, TrafficManager trafficManager, bool useCache = false, string serviceName = "vegomatic", bool useIPAddress = false)
        {
            // Append a GUID to the test name to ensure that we won't
            // conflict with what any previous test runs may have loaded
            // into the cache.

            testName += "-" + Guid.NewGuid().ToString("D");

            // Verify that we can create an HTTP traffic manager rule for a 
            // site on the proxy port using a specific hostname and then
            // verify that that the traffic manager actually works by spinning
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
                client.DefaultRequestHeaders.Host = testHostname;

                // Configure the traffic manager rule.

                var rule = new TrafficHttpRule()
                {
                    Name         = "vegomatic",
                    CheckExpect  = "status 200",
                    CheckSeconds = 1,
                };

                if (useCache)
                {
                    rule.Cache = new TrafficHttpCache() { Enabled = true };
                }

                rule.Frontends.Add(
                    new TrafficHttpFrontend()
                    {
                        Host      = useIPAddress ? null : hostname,
                        ProxyPort = proxyPort
                    });

                rule.Backends.Add(
                    new TrafficHttpBackend()
                    {
                        Server = serviceName,
                        Port   = 80
                    });

                trafficManager.SetRule(rule);

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

                // Repeat the test if caching is enabled with the same URLs as last time and verify that
                // we see cache hits this time.

                if (useCache)
                {
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

                // Spin up a second replica and repeat the query test to verify 
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

                await NeonHelper.WaitAllAsync(tasks, TimeSpan.FromSeconds(30));

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
        /// Verify that we can create an HTTP traffic manager rule for a 
        /// site on the proxy port using a specific hostname and then
        /// verify that that the traffic manager actually works by spinning
        /// up a [vegomatic] based service to accept the traffic.
        /// </summary>
        /// <param name="testName">Simple name (without spaces) used to ensure that URIs cached for different tests won't conflict.</param>
        /// <param name="hostnames">The hostnames to be used for .</param>
        /// <param name="proxyPort">The inbound proxy port.</param>
        /// <param name="network">The proxy network.</param>
        /// <param name="trafficManager">The traffic manager.</param>
        /// <param name="useCache">Optionally enable caching and verify.</param>
        /// <param name="serviceName">Optionally specifies the backend service name (defaults to <b>vegomatic</b>).</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task TestHttpMultipleFrontends(string testName, string[] hostnames, int proxyPort, string network, TrafficManager trafficManager, bool useCache = false, string serviceName = "vegomatic")
        {
            Covenant.Requires<ArgumentNullException>(hostnames != null && hostnames.Length > 0);

            // Append a GUID to the test name to ensure that we won't
            // conflict with what any previous test runs may have loaded
            // into the cache.

            testName += "-" + Guid.NewGuid().ToString("D");

            // Verify that we can create an HTTP traffic manager rule for a 
            // site on the proxy port using a specific hostname and then
            // verify that that the traffic manager actually works by spinning
            // up a [vegomatic] based service to accept the traffic.

            var queryCount = 100;
            var manager    = hive.GetReachableManager();
            var proxyUri   = new Uri($"http://{manager.PrivateAddress}:{proxyPort}/");

            manager.Connect();

            using (var client = new TestHttpClient(disableConnectionReuse: true))
            {
                // Configure the traffic manager rule.

                var rule = new TrafficHttpRule()
                {
                    Name         = "vegomatic",
                    CheckExpect  = "status 200",
                    CheckSeconds = 1,
                };

                if (useCache)
                {
                    rule.Cache = new TrafficHttpCache() { Enabled = true };
                }

                foreach (var hostname in hostnames)
                {
                    rule.Frontends.Add(
                        new TrafficHttpFrontend()
                        {
                            Host      = hostname,
                            ProxyPort = proxyPort
                        });
                }

                rule.Backends.Add(
                    new TrafficHttpBackend()
                    {
                        Server = serviceName,
                        Port   = 80
                    });

                trafficManager.SetRule(rule);

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

                    // Repeat the test if caching is enabled with the same URLs as last time and verify that
                    // we see cache hits this time.

                    if (useCache)
                    {
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

                // Spin up a second replica and repeat the query test for each hostname
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

                    await NeonHelper.WaitAllAsync(tasks, TimeSpan.FromSeconds(30));

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

        /// <summary>
        /// Verify that we can create HTTP traffic manager rules for a 
        /// site on the proxy port using a specific hostname and various
        /// path prefixes and then verify that that the traffic manager
        /// actually works  by spinning up a [vegomatic] based service to
        /// accept the traffic.
        /// </summary>
        /// <param name="testName">Simple name (without spaces) used to ensure that URIs cached for different tests won't conflict.</param>
        /// <param name="proxyPort">The inbound proxy port.</param>
        /// <param name="network">The proxy network.</param>
        /// <param name="trafficManager">The traffic manager.</param>
        /// <param name="useCache">Optionally enable caching and verify.</param>
        /// <param name="serviceName">Optionally specifies the backend service name prefix (defaults to <b>vegomatic</b>).</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task TestHttpPrefix(string testName, int proxyPort, string network, TrafficManager trafficManager, bool useCache = false, string serviceName = "vegomatic")
        {
            // Append a GUID to the test name to ensure that we won't
            // conflict with what any previous test runs may have loaded
            // into the cache.

            testName += "-" + Guid.NewGuid().ToString("D");

            // Verify that we can create an HTTP traffic manager rule for a 
            // site on the proxy port using a specific hostname and then
            // verify that that the traffic manager actually works by spinning
            // up a [vegomatic] based service to accept the traffic.

            var manager  = hive.GetReachableManager();
            var hostname = testHostname;

            manager.Connect();

            using (var client = new TestHttpClient(disableConnectionReuse: true))
            {
                // Setup the client to query the [vegomatic] service through the
                // proxy without needing to configure a hive DNS entry.

                client.BaseAddress                = new Uri($"http://{manager.PrivateAddress}:{proxyPort}/");
                client.DefaultRequestHeaders.Host = hostname;

                // Create the traffic manager rules, one without a path prefix and
                // some others, some with intersecting prefixes so we can verify
                // that the longest prefixes are matched first.
                //
                // Each rule's backend will be routed to a service whose name
                // will be constructed from [testName] plus the prefix with the
                // slashes replaced with dashes.  Each service will be configured
                // to return its name.

                var prefixes = new PrefixInfo[]
                {
                    new PrefixInfo("/",         $"{serviceName}"),
                    new PrefixInfo("/foo/",     $"{serviceName}-foo"),
                    new PrefixInfo("/foo/bar/", $"{serviceName}-foo-bar"),
                    new PrefixInfo("/foobar/",  $"{serviceName}-foobar"),
                    new PrefixInfo("/bar/",     $"{serviceName}-bar")
                };

                // Spin the services up first in parallel (for speed).  Each of
                // these service will respond to requests with its service name.

                var tasks = new List<Task>();

                foreach (var prefix in prefixes)
                {
                    tasks.Add(Task.Run(
                        () =>
                        {
                            manager.SudoCommand($"docker service create --name {prefix.ServiceName} --network {network} --replicas 1 {vegomaticImage} test-server server-id={prefix.ServiceName}").EnsureSuccess();
                        }));
                }

                await NeonHelper.WaitAllAsync(tasks, TimeSpan.FromSeconds(30));

                // Create the traffic manager rules.

                foreach (var prefix in prefixes)
                {
                    var rule = new TrafficHttpRule()
                    {
                        Name         = prefix.ServiceName,
                        CheckExpect  = "status 200",
                        CheckSeconds = 1,
                    };

                    if (useCache)
                    {
                        rule.Cache = new TrafficHttpCache() { Enabled = true };
                    }

                    var frontend = new TrafficHttpFrontend()
                        {
                            Host      = hostname,
                            ProxyPort = proxyPort
                        };

                    if (!string.IsNullOrEmpty(prefix.Path))
                    {
                        frontend.PathPrefix = prefix.Path;
                    }

                    rule.Frontends.Add(frontend);

                    rule.Backends.Add(
                        new TrafficHttpBackend()
                        {
                            Server = prefix.ServiceName,
                            Port   = 80
                        });

                    trafficManager.SetRule(rule, deferUpdate: true);
                }

                trafficManager.Update();

                // Wait for all of the services to report being ready.

                await NeonHelper.WaitForAsync(
                    async () =>
                    {
                        foreach (var prefix in prefixes)
                        {
                            try
                            {
                                var response = await client.GetAsync(prefix.Path);

                                response.EnsureSuccessStatusCode();
                            }
                            catch
                            {
                                return false;
                            }
                        }

                        return true;
                    },
                    timeout: TimeSpan.FromSeconds(60),
                    pollTime: TimeSpan.FromSeconds(1));

                // Give everything a chance to stablize.

                await Task.Delay(TimeSpan.FromSeconds(5));

                // Now verify that prefix rules route to the correct backend service.

                foreach (var prefix in prefixes)
                {
                    var response = await client.GetAsync($"{prefix.Path}{testName}?expires=60");

                    response.EnsureSuccessStatusCode();

                    var body = await response.Content.ReadAsStringAsync();

                    Assert.Equal(prefix.ServiceName, body.Trim());

                    if (useCache)
                    {
                        // Verify that the request routed through Varnish.

                        Assert.True(ViaVarnish(response));

                        // This is the first request using the globally unique [testName]
                        // so it should not be a cache hit.

                        Assert.False(CacheHit(response));
                    }
                }

                // If caching is enabled, perform the requests again to ensure that
                // we see cache hits.

                if (useCache)
                {
                    foreach (var prefix in prefixes)
                    {
                        // Request the item again and verify that it was a cache hit.

                        var response = await client.GetAsync($"{prefix.Path}{testName}?expires=60");

                        response.EnsureSuccessStatusCode();

                        var body = await response.Content.ReadAsStringAsync();

                        Assert.Equal(prefix.ServiceName, body.Trim());
                        Assert.True(CacheHit(response));
                    }
                }
            }
        }

        /// <summary>
        /// Verify that we can create an HTTP traffic manager rule that pre-warms items.
        /// </summary>
        /// <param name="testName">Simple name (without spaces) used to ensure that URIs cached for different tests won't conflict.</param>
        /// <param name="proxyPort">The inbound proxy port.</param>
        /// <param name="network">The proxy network.</param>
        /// <param name="trafficManager">The traffic manager.</param>
        /// <param name="serviceName">Optionally specifies the backend service name (defaults to <b>vegomatic</b>).</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task TestHttpCacheWarming(string testName, int proxyPort, string network, TrafficManager trafficManager, string serviceName = "vegomatic")
        {
            // Append a GUID to the test name to ensure that we won't
            // conflict with what any previous test runs may have loaded
            // into the cache.

            testName += "-" + Guid.NewGuid().ToString("D");

            // Verify that we can create an HTTP traffic manager rule for a 
            // site on the proxy port using a specific hostname and then
            // verify that warming actually works by spinning up a [vegomatic] 
            // based service to accept the traffic.
            //
            // We'll do this by specifying warm and cold URIs that both enable
            // caching.  We'll specify the warm URI as a warm target but not
            // the cold URI.  Then we'll publish the rule and wait for a bit
            // to allow it to stablize and for the [neon-proxy-cache] to
            // load the warm URI.
            //
            // Finally, we'll verify that this worked by fetching both URIs.
            // The warm URI should indicate that it came from the cache and
            // the cold URI should not be cached.

            var manager       = hive.GetReachableManager();
            var guid          = Guid.NewGuid().ToString("D");  // Avoid conflicts with previous test runs
            var expireSeconds = 60;
            var warmUri       = new Uri($"http://{testHostname}:{proxyPort}/{guid}/warm?body=text:warm&Expires={expireSeconds}");
            var coldUri       = new Uri($"http://{testHostname}:{proxyPort}/{guid}/cold?body=text:cold&Expires={expireSeconds}");

            manager.Connect();

            using (var client = new TestHttpClient(disableConnectionReuse: true))
            {
                // Setup the client to query the [vegomatic] service through the
                // proxy without needing to configure a hive DNS entry.

                client.BaseAddress                = new Uri($"http://{manager.PrivateAddress}:{proxyPort}/");
                client.DefaultRequestHeaders.Host = testHostname;

                // Configure the traffic manager rule.

                var rule = new TrafficHttpRule()
                {
                    Name         = "vegomatic",
                    CheckExpect  = "status 200",
                    CheckSeconds = 1,
                };

                rule.Cache = new TrafficHttpCache() { Enabled = true };
                rule.Cache.WarmTargets.Add(
                    new TrafficWarmTarget()
                    {
                         UpdateSeconds = 1.0,
                         Uri           = warmUri.ToString()
                    });

                rule.Frontends.Add(
                    new TrafficHttpFrontend()
                    {
                        Host      = testHostname,
                        ProxyPort = proxyPort
                    });

                rule.Backends.Add(
                    new TrafficHttpBackend()
                    {
                        Server = serviceName,
                        Port   = 80
                    });

                trafficManager.SetRule(rule);

                // Spin up a [vegomatic] service instance.

                manager.SudoCommand($"docker service create --name vegomatic --network {network} --replicas 1 {vegomaticImage} test-server").EnsureSuccess();
                await WaitUntilReadyAsync(client.BaseAddress, testHostname);

                // Wait a bit longer to ensure that the cache has had a chance to 
                // warm the URI.

                await Task.Delay(TimeSpan.FromSeconds(5));

                // Query for the warm and cold URIs and verify that the warm item was a
                // cache hit and the cold item was not.

                var warmResponse = await client.GetAsync(warmUri.PathAndQuery);
                var warmBody     = (await warmResponse.Content.ReadAsStringAsync()).Trim();
                var coldResponse = await client.GetAsync(coldUri.PathAndQuery);
                var coldBody     = (await coldResponse.Content.ReadAsStringAsync()).Trim();

                Assert.Equal(HttpStatusCode.OK, warmResponse.StatusCode);
                Assert.Equal("warm", warmBody);
                Assert.True(CacheHit(warmResponse));

                Assert.Equal(HttpStatusCode.OK, coldResponse.StatusCode);
                Assert.Equal("cold", coldBody);
                Assert.False(CacheHit(coldResponse));
            }
        }
    }
}
