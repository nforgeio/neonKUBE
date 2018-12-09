//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleTrafficManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Couchbase;
using Couchbase.Core;
using Couchbase.Linq;
using Couchbase.Linq.Extensions;
using Couchbase.N1QL;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Data;
using Neon.Hive;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Couchbase;
using Neon.Xunit.Hive;

using Xunit;

namespace TestHive
{
    public class Test_AnsibleTrafficManager : IClassFixture<HiveFixture>
    {
        private HiveFixture     hiveFixture;
        private HiveProxy       hive;
        private string          vegomaticImage = $"nhive/vegomatic:{ThisAssembly.Git.Branch}-latest";

        public Test_AnsibleTrafficManager(HiveFixture fixture)
        {
            if (!fixture.LoginAndInitialize())
            {
                fixture.ClearTrafficManagers();
            }

            this.hiveFixture = fixture;
            this.hive = fixture.Hive;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Public()
        {
            //-----------------------------------------------------------------
            // Verify that we can add a simple public traffic manager rule.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: public
        state: present
        rule_name: test
        rule:
          mode: http
          frontends:
            - host: test.com
          backends:
            - server: www.google.com
              port: 80
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            var rule = (TrafficHttpRule)hive.PublicTraffic.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(TrafficMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(HiveHostPorts.ProxyPublicHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("www.google.com", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);

            //-----------------------------------------------------------------
            // Add the same rule and verify that no change was detected this time.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: public
        state: present
        rule_name: test
        rule:
          mode: http
          frontends:
            - host: test.com
          backends:
            - server: www.google.com
              port: 80
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            rule = (TrafficHttpRule)hive.PublicTraffic.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(TrafficMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(HiveHostPorts.ProxyPublicHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("www.google.com", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);

            //-----------------------------------------------------------------
            // Add the same rule and use [state: update] to force an update.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: public
        state: present
        rule_name: test
        rule:
          mode: http
          frontends:
            - host: test.com
          backends:
            - server: www.google.com
              port: 80
    - name: update
      neon_traffic_manager:
        name: public
        state: update
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            rule = (TrafficHttpRule)hive.PublicTraffic.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(TrafficMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(HiveHostPorts.ProxyPublicHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("www.google.com", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);

            taskResult = results.GetTaskResult("update");
            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            //-----------------------------------------------------------------
            // Modify the rule and verify that a change was detected.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: public
        state: present
        rule_name: test
        rule:
          mode: http
          frontends:
            - host: test.com
          backends:
            - server: bing.com
              port: 80
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (TrafficHttpRule)hive.PublicTraffic.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(TrafficMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(HiveHostPorts.ProxyPublicHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("bing.com", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);

            //-----------------------------------------------------------------
            // Delete the rule and verify.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: public
        state: absent
        rule_name: test
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (TrafficHttpRule)hive.PublicTraffic.GetRule("test");

            Assert.Null(rule);

            //-----------------------------------------------------------------
            // Delete the rule again and verify that there was no change.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: public
        state: absent
        rule_name: test
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            rule = (TrafficHttpRule)hive.PublicTraffic.GetRule("test");

            Assert.Null(rule);

            //-----------------------------------------------------------------
            // Delete the rule again and then use [state: update] and verify the change.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: public
        state: absent
        rule_name: test
    - name: update
      neon_traffic_manager:
        name: public
        state: update
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            taskResult = results.GetTaskResult("update");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (TrafficHttpRule)hive.PublicTraffic.GetRule("test");

            Assert.Null(rule);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Private()
        {
            //-----------------------------------------------------------------
            // Verify that we can add a simple public traffic manager rule.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: private
        state: present
        rule_name: test
        rule:
          mode: http
          frontends:
            - host: test.com
          backends:
            - server: www.google.com
              port: 80
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            var rule = (TrafficHttpRule)hive.PrivateTraffic.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(TrafficMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(HiveHostPorts.ProxyPrivateHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("www.google.com", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);

            //-----------------------------------------------------------------
            // Add the same rule and verify that no change was detected this time.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: private
        state: present
        rule_name: test
        rule:
          mode: http
          frontends:
            - host: test.com
          backends:
            - server: www.google.com
              port: 80
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            rule = (TrafficHttpRule)hive.PrivateTraffic.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(TrafficMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(HiveHostPorts.ProxyPrivateHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("www.google.com", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);

            //-----------------------------------------------------------------
            // Add the same rule and then use [state: update] to force an update.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: private
        state: present
        rule_name: test
        rule:
          mode: http
          frontends:
            - host: test.com
          backends:
            - server: www.google.com
              port: 80
    - name: update
      neon_traffic_manager:
        name: private
        state: update
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            taskResult = results.GetTaskResult("update");
            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (TrafficHttpRule)hive.PrivateTraffic.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(TrafficMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(HiveHostPorts.ProxyPrivateHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("www.google.com", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);

            //-----------------------------------------------------------------
            // Modify the rule and verify that a change was detected.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: private
        state: present
        rule_name: test
        rule:
          mode: http
          frontends:
            - host: test.com
          backends:
            - server: bing.com
              port: 80
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (TrafficHttpRule)hive.PrivateTraffic.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(TrafficMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("test.com", rule.Frontends.First().Host);
            Assert.Equal(HiveHostPorts.ProxyPrivateHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("bing.com", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);

            //-----------------------------------------------------------------
            // Delete the rule and verify.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: private
        state: absent
        rule_name: test
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (TrafficHttpRule)hive.PrivateTraffic.GetRule("test");

            Assert.Null(rule);

            //-----------------------------------------------------------------
            // Delete the rule again and verify that there was no change.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: private
        state: absent
        rule_name: test
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            rule = (TrafficHttpRule)hive.PrivateTraffic.GetRule("test");

            Assert.Null(rule);

            //-----------------------------------------------------------------
            // Delete the rule again and then use [state: update] for force an update.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: private
        state: absent
        rule_name: test
    - name: update
      neon_traffic_manager:
        name: private
        state: update
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            taskResult = results.GetTaskResult("update");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (TrafficHttpRule)hive.PrivateTraffic.GetRule("test");

            Assert.Null(rule);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task Cache()
        {
            var manager = hive.GetReachableManager();

            //-----------------------------------------------------------------
            // Verify that we can add a cached public traffic manager rule and
            // then that PURGE works.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: public
        state: present
        rule_name: test
        rule:
          mode: http
          checkexpect: status 200
          cache:
            enabled: true
          frontends:
            - host: vegomatic.test
          backends:
            - server: vegomatic
              port: 80
";
            var results = AnsiblePlayer.PlayNoGather(playbook);
            var taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            var rule = (TrafficHttpRule)hive.PublicTraffic.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(TrafficMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("vegomatic.test", rule.Frontends.First().Host);
            Assert.Equal(HiveHostPorts.ProxyPublicHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("vegomatic", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);
            Assert.True(rule.Cache.Enabled);

            //-----------------------------------------------------------------
            // Add the same rule and verify that no change was detected this time.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: public
        state: present
        rule_name: test
        rule:
          mode: http
          checkexpect: status 200
          cache:
            enabled: true
          frontends:
            - host: vegomatic.test
          backends:
            - server: vegomatic
              port: 80
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);

            rule = (TrafficHttpRule)hive.PublicTraffic.GetRule("test");

            Assert.NotNull(rule);
            Assert.Equal("test", rule.Name);
            Assert.Equal(TrafficMode.Http, rule.Mode);
            Assert.Single(rule.Frontends);
            Assert.Equal("vegomatic.test", rule.Frontends.First().Host);
            Assert.Equal(HiveHostPorts.ProxyPublicHttp, rule.Frontends.First().ProxyPort);
            Assert.Single(rule.Backends);
            Assert.Equal("vegomatic", rule.Backends.First().Server);
            Assert.Equal(80, rule.Backends.First().Port);
            Assert.True(rule.Cache.Enabled);

            //-----------------------------------------------------------------
            // Crank up Vegomatic as the backing service and perform some tests
            // to verify that caching is actually working.

            var baseAddress = new Uri($"http://{manager.PrivateAddress}:80/");
            var uuid        = Guid.NewGuid().ToString("D");

            manager.SudoCommand($"docker service create --name vegomatic --network {HiveConst.PublicNetwork} --replicas 1 {vegomaticImage} test-server expires=300").EnsureSuccess();
            await WaitUntilReadyAsync(baseAddress, "vegomatic.test");

            using (var client = new TestHttpClient(disableConnectionReuse: true))
            {
                client.BaseAddress                = baseAddress;
                client.DefaultRequestHeaders.Host = "vegomatic.test";

                // Cache responses for [/{uuid}/test1.txt] and [/{uuid}/test2.txt]

                var response = await client.GetAsync($"/{uuid}/test1.txt");

                Assert.True(ViaVarnish(response));      // Verify that the request was routed thru Varnish
                Assert.False(CacheHit(response));       // The first request shouldn't be cached

                response = await client.GetAsync($"/{uuid}/test1.txt");

                Assert.True(ViaVarnish(response));
                Assert.True(CacheHit(response));        // The second request should be cached

                response = await client.GetAsync($"/{uuid}/test2.txt");

                Assert.True(ViaVarnish(response));      // Verify that the request was routed thru Varnish
                Assert.False(CacheHit(response));       // The first request shouldn't be cached

                response = await client.GetAsync($"/{uuid}/test2.txt");

                Assert.True(ViaVarnish(response));
                Assert.True(CacheHit(response));        // The second request should be cached

                // Purge [test1.txt] and verify that it's no longer cached and that [test2.txt]
                // is still cached.

                playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: public
        state: purge
        purge_list:
           - http://vegomatic.test/*/test1.txt
";
                results = AnsiblePlayer.PlayNoGather(playbook);
                taskResult = results.GetTaskResult("rule");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);        // PURGE is always considered to be a change

                response = await client.GetAsync($"/{uuid}/test1.txt");

                Assert.True(ViaVarnish(response));
                Assert.False(CacheHit(response));       // This should have been purged.

                response = await client.GetAsync($"/{uuid}/test2.txt");

                Assert.True(ViaVarnish(response));
                Assert.True(CacheHit(response));        // This should still be cached.

                // Both items should both be loaded back into the cache now.  We're going
                // to try purging both of them this time.

                playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: public
        state: purge
        purge_list:
           - http://vegomatic.test/**/*
";
                results = AnsiblePlayer.PlayNoGather(playbook);
                taskResult = results.GetTaskResult("rule");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);
            
                response = await client.GetAsync($"/{uuid}/test1.txt");

                Assert.True(ViaVarnish(response));
                Assert.False(CacheHit(response));       // This should have been purged.

                response = await client.GetAsync($"/{uuid}/test2.txt");

                Assert.True(ViaVarnish(response));
                Assert.False(CacheHit(response));       // This should be purged too.

                // Both items should both be loaded back into the cache now.  We're going
                // to use ALL to both of them this time.

                playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: public
        state: purge
        purge_list:
           - ALL
";
                results = AnsiblePlayer.PlayNoGather(playbook);
                taskResult = results.GetTaskResult("rule");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);
            
                response = await client.GetAsync($"/{uuid}/test1.txt");

                Assert.True(ViaVarnish(response));
                Assert.False(CacheHit(response));       // This should have been purged.

                response = await client.GetAsync($"/{uuid}/test2.txt");

                Assert.True(ViaVarnish(response));
                Assert.False(CacheHit(response));       // This should be purged too.

                // Verify that we can do case sensitive purging.

                await client.GetAsync($"/{uuid}/test1.txt");
                await client.GetAsync($"/{uuid}/test2.txt");
                await client.GetAsync($"/{uuid}/TEST1.TXT");
                await client.GetAsync($"/{uuid}/TEST2.TXT");

                playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: public
        state: purge
        purge_list:
           - http://vegomatic.test/*/test*.txt
        purge_case_sensitive: yes
";
                results = AnsiblePlayer.PlayNoGather(playbook);
                taskResult = results.GetTaskResult("rule");

                Assert.True(taskResult.Success);
                Assert.True(taskResult.Changed);

                response = await client.GetAsync($"/{uuid}/test1.txt");

                Assert.True(ViaVarnish(response));
                Assert.False(CacheHit(response));       // This should have been purged.

                response = await client.GetAsync($"/{uuid}/test2.txt");

                Assert.True(ViaVarnish(response));
                Assert.False(CacheHit(response));       // This should be purged too.

                response = await client.GetAsync($"/{uuid}/TEST1.TXT");

                Assert.True(ViaVarnish(response));
                Assert.True(CacheHit(response));        // This should still be cached.

                response = await client.GetAsync($"/{uuid}/TEST2.TXT");

                Assert.True(ViaVarnish(response));
                Assert.True(CacheHit(response));        // This should still be cached too.
            }

            //-----------------------------------------------------------------
            // Delete the rule and verify.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: rule
      neon_traffic_manager:
        name: public
        state: absent
        rule_name: test
";
            results = AnsiblePlayer.PlayNoGather(playbook);
            taskResult = results.GetTaskResult("rule");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            rule = (TrafficHttpRule)hive.PublicTraffic.GetRule("test");

            Assert.Null(rule);
        }

        /// <summary>
        /// Waits for the a remote proxy and origin to report being ready.
        /// </summary>
        /// <param name="baseUri">The base URI.</param>
        /// <param name="hostname">The target hostname.</param>
        private async Task WaitUntilReadyAsync(Uri baseUri, string hostname)
        {
            // Delay for 10 seconds so that any DNS entries cached by HAProxy will
            // have a chance to expire.  By default, we configure the DNS hold time
            // to be 5 seconds, so waiting for 10 seconds should be more than enough.

            await Task.Delay(TimeSpan.FromSeconds(10));

            // Allow self-signed certificates for HTTPS tests.

            var handler = new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            using (var client = new TestHttpClient(disableConnectionReuse: true, handler: handler, disposeHandler: true))
            {
                client.BaseAddress                = baseUri;
                client.DefaultRequestHeaders.Host = hostname;

                await NeonHelper.WaitForAsync(
                    async () =>
                    {
                        try
                        {
                            var response = await client.GetAsync("/");

                            return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Redirect;
                        }
                        catch (HttpRequestException)
                        {
                            // We're going to ignore these because this probably 
                            // indicates that HAProxy hasn't started a listener
                            // on the port yet.

                            return false;
                        }
                    },
                    timeout: TimeSpan.FromSeconds(60),
                    pollTime: TimeSpan.FromMilliseconds(100));
            }
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
    }
}