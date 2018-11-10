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
        private class Redirection
        {
            public Redirection(string fromUri, string toUri)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(fromUri));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(toUri));

                this.FromUri = new Uri(fromUri);
                this.ToUri   = new Uri(toUri);
            }

            public Uri FromUri { get; private set; }
            public Uri ToUri { get; private set; }
        }

        /// <summary>
        /// Generates a traffic director rule for each <see cref="Redirection"/> passed that
        /// will redirect from one URI to another.
        /// </summary>
        /// <param name="trafficManager">The target traffic manager.</param>
        /// <param name="testName">Used to name the traffic director rules.</param>
        /// <param name="singleRule">
        /// Pass <c>true</c> to test a single rule with all of the redirections or 
        /// <c>false</c> to test with one redirection per rule.
        /// </param>
        /// <param name="redirections">The redirections.</param>
        private async Task TestRedirect(TrafficManager trafficManager, string testName, bool singleRule, params Redirection[] redirections)
        {
            var manager = hive.GetReachableManager();

            // We need local DNS mappings for each of the URI hosts to target a hive node.

            var hosts = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var redirect in redirections)
            {
                if (!hosts.Contains(redirect.FromUri.Host))
                {
                    hosts.Add(redirect.FromUri.Host);
                }

                if (!hosts.Contains(redirect.ToUri.Host))
                {
                    hosts.Add(redirect.ToUri.Host);
                }
            }

            foreach (var host in hosts)
            {
                hiveFixture.LocalMachineHosts.AddHostAddress(host, manager.PrivateAddress.ToString(), deferCommit: true);
            }

            hiveFixture.LocalMachineHosts.Commit();

            // Generate and upload a self-signed certificate for each redirect host that
            // uses HTTPS and upload these to the hive.  Each certificate will be named
            // the same as the hostname.

            var hostToCertificate = new Dictionary<string, TlsCertificate>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var redirect in redirections.Where(r => r.FromUri.Scheme == "https"))
            {
                var host = redirect.FromUri.Host;

                if (hostToCertificate.ContainsKey(host))
                {
                    continue;
                }

                hostToCertificate[host] = TlsCertificate.CreateSelfSigned(host);
            }

            foreach (var item in hostToCertificate)
            {
                hive.Certificate.Set(item.Key, item.Value);
            }

            // Create the traffic director rule(s).

            if (singleRule)
            {
                var rule = new TrafficDirectorHttpRule()
                {
                    Name = testName,
                };

                foreach (var redirect in redirections)
                {
                    var frontend = new TrafficDirectorHttpFrontend()
                    {
                        Host       = redirect.FromUri.Host,
                        ProxyPort  = redirect.FromUri.Port,
                        RedirectTo = redirect.ToUri
                    };

                    if (redirect.FromUri.Scheme == "https")
                    {
                        frontend.CertName = redirect.FromUri.Host;
                    }

                    rule.Frontends.Add(frontend);
                }

                trafficManager.SetRule(rule);
            }
            else
            {
                var redirectIndex = 0;

                foreach (var redirect in redirections)
                {
                    var rule = new TrafficDirectorHttpRule()
                    {
                        Name = $"{testName}-{redirectIndex}",
                    };

                    var frontend = new TrafficDirectorHttpFrontend()
                    {
                        Host       = redirect.FromUri.Host,
                        ProxyPort  = redirect.FromUri.Port,
                        RedirectTo = redirect.ToUri
                    };

                    if (redirect.FromUri.Scheme == "https")
                    {
                        frontend.CertName = redirect.FromUri.Host;
                    }

                    rule.Frontends.Add(frontend);
                    trafficManager.SetRule(rule);
                    redirectIndex++;
                }
            }

            // Give the new rules some time to deploy.

            await Task.Delay(TimeSpan.FromSeconds(5));

            // Now all we need to do is hit all of the redirect [FromUri]s
            // and verify that we get redirects to the corresponding 
            // [ToUri]s.

            // Allow self-signed certificates and disable client-side automatic redirect handling
            // so we'll be able to see the redirect responses.

            var handler = new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                AllowAutoRedirect = false   // We need to see the redirects
            };

            using (var client = new TestHttpClient(disableConnectionReuse: true, handler: handler, disposeHandler: true))
            {
                foreach (var redirect in redirections)
                {
                    var response = await client.GetAsync(redirect.FromUri);

                    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
                    Assert.True(response.Headers.TryGetValues("Location", out var locations));
                    Assert.Equal(redirect.ToUri.ToString(), locations.Single());
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Redirect_Public()
        {
            await TestRedirect(hive.PublicTraffic, "test-a", singleRule: true, redirections:
                new Redirection[]
                {
                    new Redirection($"http://a.test", $"https://a.test"),
                    new Redirection($"https://b.test", $"http://b.test"),
                    new Redirection($"http://c.test:{HiveHostPorts.ProxyPublicFirstUserPort}", $"http://c.test:{HiveHostPorts.ProxyPublicFirstUserPort + 1}"),
                    new Redirection($"http://d.test:{HiveHostPorts.ProxyPublicFirstUserPort}", $"http://d.test:{HiveHostPorts.ProxyPublicFirstUserPort + 1}"),
                });
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Redirect_Private()
        {
            await TestRedirect(hive.PrivateTraffic, "test-a", singleRule: true, redirections:
                new Redirection[]
                {
                    new Redirection($"http://a.test:{HiveHostPorts.ProxyPrivateHttp}", $"https://a.test:{HiveHostPorts.ProxyPrivateHttps}"),
                    new Redirection($"https://b.test:{HiveHostPorts.ProxyPrivateHttps}", $"http://b.test:{HiveHostPorts.ProxyPrivateHttp}"),
                    new Redirection($"http://c.test:{HiveHostPorts.ProxyPrivateFirstUserPort}", $"http://c.test:{HiveHostPorts.ProxyPrivateFirstUserPort + 1}"),
                    new Redirection($"http://d.test:{HiveHostPorts.ProxyPrivateFirstUserPort}", $"http://d.test:{HiveHostPorts.ProxyPrivateFirstUserPort + 1}"),
                });
        }
    }
}
