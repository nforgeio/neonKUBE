//-----------------------------------------------------------------------------
// FILE:	    Test_HiveLoadBalancer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        /// Waits for the a remote proxy and server to report being ready.
        /// </summary>
        private async Task WaitForHealthyAsync()
        {
            // $hack(jeff.lill): 
            //
            // I'm just hardcoding this to 7 seconds assuming that health checks
            // will be performed on 1 second intervals and that the origin server
            // will be declared health after 5 or less successful probes.  I'm
            // adding additional 2 seconds for safety.

            await Task.Delay(TimeSpan.FromSeconds(7));
        }

        /// <summary>
        /// Verify that we can create a public load balancer rule for a 
        /// site on the public port using a specific hostname and then
        /// verify that that the load balancer actual works by spinning
        /// up a [neon-vegomatic] based service to accept the traffic.
        /// </summary>
        /// <param name="proxyPort">The inbound proxy port.</param>
        /// <param name="network">The proxy network.</param>
        /// <param name="loadBalancerManager">The load balancer manager.</param>
        /// <param name="testCaching">Optionally enable caching and verify.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task TestHttpRule(int proxyPort, string network, LoadBalancerManager loadBalancerManager, bool testCaching = false)
        {
            // Verify that we can create a public load balancer rule for a 
            // site on the public port using a specific hostname and then
            // verify that that the load balancer actual works by spinning
            // up a [neon-vegomatic] based service to accept the traffic.

            var vegomaticImage = $"nhive/neon-vegomatic:{ThisAssembly.Git.Branch}-latest";
            var hostname       = "vegomatic.test";
            var queryCount     = 100;
            var manager        = hive.GetReachableManager();

            manager.Connect();

            using (var client = new TestHttpClient(disableConnectionReuse: true))
            {
                // Setup the client to query the [vegomatic] service through the
                // proxy without needing to configure a hive DNS entry.

                client.BaseAddress = new Uri($"http://{manager.PrivateAddress}:{proxyPort}/");
                client.DefaultRequestHeaders.Host = hostname;

                // The test should start out with no non-system rules.

                Assert.Empty(loadBalancerManager.ListRules(r => !r.System));

                // Configure the load balancer rule.

                var rule = new LoadBalancerHttpRule()
                {
                    Name         = "vegomatic",
                    CheckExpect  = "status 200",
                    CheckSeconds = 1,
                };

                if (testCaching)
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

                // Spin up a single [neon-vegomatic] service with a single instance that will
                // return the instance UUID.  We're also going to configure this to set the
                // [Expires] header to a date 60 seconds in the future so we can verify that
                // caching is working.

                manager.SudoCommand($"docker service create --name vegomatic --network {network} --replicas 1 {vegomaticImage} instanceid-server expire-seconds=60").EnsureSuccess();
                await WaitForHealthyAsync();

                // Query the service several times to verify that we get a response and 
                // also that all of the responses are the same (because we have only
                // a single [neon-vegomatic] instance returning its UUID).
                //
                // We'll determine whether any responses were cached by looking for the
                // [X-Varnish] header which will be added for cache hits.

                var uniqueResponses = new HashSet<string>();
                var cached          = false;

                for (int i = 0; i < queryCount; i++)
                {
                    var response = await client.GetAsync("/");
                    var body     = await response.Content.ReadAsStringAsync();

                    if (response.Headers.Contains("X-Varnish"))
                    {
                        cached = true;
                    }

                    if (!uniqueResponses.Contains(body))
                    {
                        uniqueResponses.Add(body);
                    }
                }

                Assert.Single(uniqueResponses);

                if (testCaching)
                {
                    Assert.True(cached);
                }

                // Spinup a second replica and repeat the query test to verify that we
                // see two unique responses.

                manager.SudoCommand($"docker service update --replicas 2 vegomatic");

                // $hack(jeff.lill): Give the second replica a chance to be reported as healthy.

                await Task.Delay(TimeSpan.FromSeconds(10));

                // Reset response info.

                uniqueResponses.Clear();
                cached = false;

                for (int i = 0; i < queryCount; i++)
                {
                    var response = await client.GetAsync("/");
                    var body     = await response.Content.ReadAsStringAsync();

                    if (response.Headers.Contains("X-Varnish"))
                    {
                        cached = true;
                    }

                    if (!uniqueResponses.Contains(body))
                    {
                        uniqueResponses.Add(body);
                    }
                }

                Assert.Equal(2, uniqueResponses.Count);

                if (testCaching)
                {
                    Assert.True(cached);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task HttpRule_Public()
        {
            await TestHttpRule(HiveHostPorts.ProxyPublicHttp, HiveConst.PublicNetwork, hive.PublicLoadBalancer);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task HttpRule_Private()
        {
            await TestHttpRule(HiveHostPorts.ProxyPrivateHttp, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task HttpRule_PublicCached()
        {
            await TestHttpRule(HiveHostPorts.ProxyPublicHttp, HiveConst.PublicNetwork, hive.PublicLoadBalancer, testCaching: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task HttpRule_PrivateCached()
        {
            await TestHttpRule(HiveHostPorts.ProxyPrivateHttp, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer, testCaching: true);
        }
    }
}
