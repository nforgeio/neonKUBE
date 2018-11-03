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

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task HttpRule_Public()
        {
            // Verify that we can create a public load balancer rule for a 
            // site on the public port using a specific hostname and then
            // verify that that the load balancer actual works by spinning
            // up a [neon-vegomatic] based service to accept the traffic.

            var vegomaticImage = $"nhive/neon-vegomatic:{ThisAssembly.Git.Branch}-latest";
            var hostname       = "vegomatic.test";
            var proxyPort      = HiveHostPorts.ProxyPublicHttp;
            var network        = "neon-public";
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

                Assert.Empty(hive.PublicLoadBalancer.ListRules(r => !r.System));

                // Configure the load balancer rule.

                var rule = new LoadBalancerHttpRule()
                {
                    Name         = "vegomatic",
                    CheckExpect  = "status 200",
                    CheckSeconds = 1,
                };

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

                hive.PublicLoadBalancer.SetRule(rule);

                // Spin up a single [neon-vegomatic] service with a single instance 
                // that will return the instance UUID.

                manager.SudoCommand($"docker service create --name vegomatic --network {network} --replicas 1 {vegomaticImage} instanceid-server").EnsureSuccess();
                await WaitForHealthyAsync();

                // Query the service several times to verify that we get a response and 
                // also that all of the responses are the same (because we have only
                // a single [neon-vegomatic] instance returning its UUID).

                var uniqueResponses = new HashSet<string>();

                for (int i = 0; i < queryCount; i++)
                {
                    var response = await client.GetStringAsync("/");

                    if (!uniqueResponses.Contains(response))
                    {
                        uniqueResponses.Add(response);
                    }
                }

                Assert.Single(uniqueResponses);

                // Spinup a second replica and repeat the query test to verify that we
                // see two unique responses.

                manager.SudoCommand($"docker service update --replicas 2 vegomatic");

                // $hack(jeff.lill): Give the second replica a chance to be reported as healthy.

                await Task.Delay(TimeSpan.FromSeconds(10));

                uniqueResponses.Clear();

                for (int i = 0; i < queryCount; i++)
                {
                    var response = await client.GetStringAsync("/");

                    if (!uniqueResponses.Contains(response))
                    {
                        uniqueResponses.Add(response);
                    }
                }

                Assert.Equal(2, uniqueResponses.Count);
            }
        }
    }
}
