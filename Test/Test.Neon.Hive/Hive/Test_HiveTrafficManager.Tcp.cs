//-----------------------------------------------------------------------------
// FILE:	    Test_HiveTrafficManager.Tcp.cs
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
        /// Verify that we can create an TCP traffic manager rule for a 
        /// site on the public port using a specific hostname and then
        /// verify that that the traffic manager actually works by spinning
        /// up a [vegomatic] based service to accept the traffic.
        /// </summary>
        /// <param name="testName">Simple name (without spaces) used to ensure that URIs cached for different tests won't conflict.</param>
        /// <param name="proxyPort">The inbound proxy port.</param>
        /// <param name="network">The proxy network.</param>
        /// <param name="trafficManager">The traffic manager.</param>
        /// <param name="serviceName">Optionally specifies the backend service name (defaults to <b>vegomatic</b>).</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task TestTcpRule(string testName, int proxyPort, string network, TrafficManager trafficManager, string serviceName = "vegomatic")
        {
            // Append a GUID to the test name to ensure that we won't
            // conflict with what any previous test runs may have loaded
            // into the cache.

            testName += "-" + Guid.NewGuid().ToString("D");

            // Verify that we can create an TCP traffic manager rule for a 
            // site on the public port using a specific hostname and then
            // verify that that the traffic manager actually works by spinning
            // up a [vegomatic] based service to accept the traffic.

            var queryCount = 100;
            var manager    = hive.GetReachableManager();
            var hostname   = manager.PrivateAddress.ToString();

            manager.Connect();

            using (var client = new TestHttpClient(disableConnectionReuse: true))
            {
                // Setup the client to query the [vegomatic] service through the
                // proxy without needing to configure a hive DNS entry.

                client.BaseAddress                = new Uri($"http://{manager.PrivateAddress}:{proxyPort}/");
                client.DefaultRequestHeaders.Host = testHostname;

                // Configure the traffic manager rule.

                var rule = new TrafficTcpRule()
                {
                    Name         = "vegomatic",
                    CheckSeconds = 1,
                };

                rule.Frontends.Add(
                    new TrafficTcpFrontend()
                    {
                        ProxyPort = proxyPort
                    });

                rule.Backends.Add(
                    new TrafficTcpBackend()
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

                var uniqueResponses = new HashSet<string>();

                for (int i = 0; i < queryCount; i++)
                {
                    var response = await client.GetAsync($"/{testName}/pass-1/{i}?body=server-id&expires=60");

                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                    var body = await response.Content.ReadAsStringAsync();

                    if (!uniqueResponses.Contains(body))
                    {
                        uniqueResponses.Add(body);
                    }
                }

                Assert.Single(uniqueResponses);

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
                        }));
                }

                await NeonHelper.WaitAllAsync(tasks, TimeSpan.FromSeconds(30));
                Assert.Equal(2, uniqueResponses.Count);
            }
        }
    }
}
