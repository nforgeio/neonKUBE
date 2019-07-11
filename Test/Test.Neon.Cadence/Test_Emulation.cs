//-----------------------------------------------------------------------------
// FILE:        Test_Emulation.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Cryptography;
using Neon.Data;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Cadence;

using Xunit;

namespace TestCadence
{
    /// <summary>
    /// Tests low-level <see cref="CadenceClient"/> functionality against a
    /// partially implemented <b>cadence-proxy</b> emulation.
    /// </summary>
    public sealed class Test_Emulation : IClassFixture<CadenceFixture>, IDisposable
    {
        CadenceFixture      fixture;
        CadenceClient       client;
        HttpClient          proxyClient;

        public Test_Emulation(CadenceFixture fixture)
        {
            var settings = new CadenceSettings()
            {
                Mode                   = ConnectionMode.ListenOnly,
                Debug                  = true,
                ProxyTimeoutSeconds    = 1.0,
                Emulate                = true,
                DebugHttpTimeout       = TimeSpan.FromSeconds(1),
                DebugDisableHeartbeats = false,      // $debug(jeff.lill): COMMENT THIS OUT!
                //DebugIgnoreTimeouts    = true
            };

            fixture.Start(settings);

            this.fixture     = fixture;
            this.client      = fixture.Connection;
            this.proxyClient = new HttpClient() { BaseAddress = client.ProxyUri };
        }

        public void Dispose()
        {
            if (proxyClient != null)
            {
                proxyClient.Dispose();
                proxyClient = null;
            }
        }

        /// <summary>
        /// Transmits a message to the connection's associated <b>cadence-proxy</b> 
        /// and then verifies that the response matches.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="message">The message to be checked.</param>
        /// <returns>The received echo message.</returns>
        private TMessage EchoToProxy<TMessage>(TMessage message)
            where TMessage : ProxyMessage, new()
        {
            var bytes   = message.SerializeAsBytes();
            var content = new ByteArrayContent(bytes);

            content.Headers.ContentType = new MediaTypeHeaderValue(ProxyMessage.ContentType);

            var request = new HttpRequestMessage(HttpMethod.Put, "/echo")
            {
                Content = content
            };

            var response = proxyClient.SendAsync(request).Result;

            response.EnsureSuccessStatusCode();

            bytes = response.Content.ReadAsByteArrayAsync().Result;

            return ProxyMessage.Deserialize<TMessage>(response.Content.ReadAsStreamAsync().Result);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Echo()
        {
            // Verify that the emulated [/echo] answers (for completeness).

            InitializeRequest message;

            using (var stream = new MemoryStream())
            {
                // Empty message.

                message = new InitializeRequest();

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<InitializeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.LibraryAddress);
                Assert.Equal(0, message.LibraryPort);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.LibraryAddress = "1.2.3.4";
                Assert.Equal("1.2.3.4", message.LibraryAddress);
                message.LibraryPort = 666;
                Assert.Equal(666, message.LibraryPort);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<InitializeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.2.3.4", message.LibraryAddress);
                Assert.Equal(666, message.LibraryPort);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.2.3.4", message.LibraryAddress);
                Assert.Equal(666, message.LibraryPort);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task NoHeartbeat()
        {
            try
            {
                var connectionClosed    = false;
                var connectionException = (Exception)null;

                client.ConnectionClosed +=
                    (sender, args) =>
                    {
                        connectionClosed    = true;
                        connectionException = args.Exception;
                    };

                // Wait 5 seconds to verify that the [cadence-proxy] is happily
                // responding to heartbeats.

                await Task.Delay(TimeSpan.FromSeconds(5));
                Assert.False(connectionClosed);

                // Disable heartbeat responses so we can verify that
                // the connection is closed gracefully.

                client.Settings.DebugIgnoreHeartbeats = true;

                await Task.Delay(TimeSpan.FromSeconds(5));
                Assert.True(connectionClosed);
                Assert.IsType<CadenceTimeoutException>(connectionException);
            }
            finally
            {
                client.Settings.DebugIgnoreHeartbeats = false;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task ConnectionFailed()
        {
            // Verify that we see a [CadenceConnectException] when connecting
            // with no server URIs.

            var settings = new CadenceSettings()
            {
                Servers = new List<string>()
            };

            await TestHelper.AssertThrowsAsync<CadenceConnectException>(async () => await CadenceClient.ConnectAsync(settings));

            // Verify that we see a [CadenceConnectException] when connecting
            // with a relative server URI.

            settings.Servers.Clear();
            settings.Servers.Add("/relativeuri");

            await TestHelper.AssertThrowsAsync<CadenceConnectException>(async () => await CadenceClient.ConnectAsync(settings));

#if TODO
            // Verify that we see a [CadenceConnectException] when attempting
            // to connect to a Cadence cluster that doesn't exist.

            // $todo(jeff.lill):
            //
            // The emulator won't throw an exception for this because it doesn't
            // actually attempt to connect to a Cadence cluster.  We should include
            // this when testing against the real [cadence-proxy].

            // $hack(jeff.lill): I'm betting that there will never be a Cadence server 
            // at the URI specified.

            settings.Servers.Clear();
            settings.Servers.Add(new Uri("http://127.1.2.3:23444"));

            await TestHelper.AssertThrowsAsync<CadenceConnectException>(async () => await CadenceClient.ConnectAsync(settings));
#endif
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void ConnectionClosed()
        {
            try
            {
                // Verify that the [ConnectionClosed] event when a
                // connection is disposed.

                fixture.Restart();

                this.client  = fixture.Connection;
                this.proxyClient = new HttpClient() { BaseAddress = client.ProxyUri };

                var connectionClosed    = false;
                var connectionException = (Exception)null;

                client.ConnectionClosed +=
                    (sender, args) =>
                    {
                        connectionClosed    = true;
                        connectionException = args.Exception;
                    };

                client.Dispose();

                Assert.True(connectionClosed);
                Assert.Null(connectionException);
            }
            finally
            {
                // Restart the cadence fixture for the next test.

                fixture.Restart();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Domain()
        {
            // Exercise the Cadence global domain operations.

            //-----------------------------------------------------------------
            // RegisterDomain:

            await client.RegisterDomainAsync("domain-0", "this is domain-0", "jeff@lilltek.com", retentionDays: 14);
            await Assert.ThrowsAsync<CadenceDomainAlreadyExistsException>(async () => await client.RegisterDomainAsync(name: "domain-0"));
            await Assert.ThrowsAsync<CadenceBadRequestException>(async () => await client.RegisterDomainAsync(name: null));

            //-----------------------------------------------------------------
            // DescribeDomain:

            var domainDescribeReply = await client.DescribeDomainAsync("domain-0");

            Assert.False(domainDescribeReply.Configuration.EmitMetrics);
            Assert.Equal(14, domainDescribeReply.Configuration.RetentionDays);
            Assert.Equal("domain-0", domainDescribeReply.DomainInfo.Name);
            Assert.Equal("this is domain-0", domainDescribeReply.DomainInfo.Description);
            Assert.Equal("jeff@lilltek.com", domainDescribeReply.DomainInfo.OwnerEmail);
            Assert.Equal(DomainStatus.Registered, domainDescribeReply.DomainInfo.Status);

            await Assert.ThrowsAsync<CadenceEntityNotExistsException>(async () => await client.DescribeDomainAsync("does-not-exist"));

            //-----------------------------------------------------------------
            // UpdateDomain:

            var updateDomainRequest = new DomainUpdateArgs();

            updateDomainRequest.Options.EmitMetrics   = true;
            updateDomainRequest.Options.RetentionDays = 77;
            updateDomainRequest.DomainInfo.OwnerEmail       = "foo@bar.com";
            updateDomainRequest.DomainInfo.Description      = "new description";

            await client.UpdateDomainAsync("domain-0", updateDomainRequest);

            domainDescribeReply = await client.DescribeDomainAsync("domain-0");

            Assert.True(domainDescribeReply.Configuration.EmitMetrics);
            Assert.Equal(77, domainDescribeReply.Configuration.RetentionDays);
            Assert.Equal("domain-0", domainDescribeReply.DomainInfo.Name);
            Assert.Equal("new description", domainDescribeReply.DomainInfo.Description);
            Assert.Equal("foo@bar.com", domainDescribeReply.DomainInfo.OwnerEmail);
            Assert.Equal(DomainStatus.Registered, domainDescribeReply.DomainInfo.Status);

            await Assert.ThrowsAsync<CadenceEntityNotExistsException>(async () => await client.UpdateDomainAsync("does-not-exist", updateDomainRequest));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Worker()
        {
            // Verify that emulated start/stop worker operations work.

            // Generate unique a domain and task list to avoid conflicts with
            // other tests on this connection.

            var domain = Guid.NewGuid().ToString("D");
            var taskList = Guid.NewGuid().ToString("D");

            // Verify parameter checks.

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.StartWorkerAsync(null, taskList));
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.StartWorkerAsync("", taskList));
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.StartWorkerAsync(domain, null));
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.StartWorkerAsync(domain, ""));

            // This operation should fail because the domain has not yet been registered.

            await Assert.ThrowsAsync<CadenceEntityNotExistsException>(async () => await client.StartWorkerAsync(domain, "test"));

            // Register the domain and then start workflow and activity workers.

            await client.RegisterDomainAsync(domain);

            var workflowWorker = await client.StartWorkerAsync(domain, taskList, new WorkerOptions() { DisableActivityWorker = true });
            var activityWorker = await client.StartWorkerAsync(domain, taskList, new WorkerOptions() { DisableWorkflowWorker = true });

            // Stop the workers.

            workflowWorker.Dispose();
            activityWorker.Dispose();

            // Stop the workers again to verify that we don't see any errors.

            workflowWorker.Dispose();
            activityWorker.Dispose();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Ping()
        {
            // Verify that Ping works and optionally measure simple transaction throughput.

            await client.PingAsync();

            var stopwatch  = new Stopwatch();
            var iterations = 5000;

            stopwatch.Start();

            for (int i = 0; i < iterations; i++)
            {
                await client.PingAsync();
            }

            stopwatch.Stop();

            var tps = iterations * (1.0 / stopwatch.Elapsed.TotalSeconds);

            Console.WriteLine($"Transactions/sec: {tps}");
            Console.WriteLine($"Latency (average): {1.0 / tps}");
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void PingAttack()
        {
            // Measure througput with 4 threads hammering the proxy with pings.

            var syncLock   = new object();
            var totalTps   = 0.0;
            var threads    = new Thread[4];
            var iterations = 5000;

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(
                    new ThreadStart(
                        () =>
                        {
                            var stopwatch = new Stopwatch();

                            stopwatch.Start();

                            for (int j = 0; j < iterations; j++)
                            {
                                client.PingAsync().Wait();
                            }

                            stopwatch.Stop();

                            var tps = iterations * (1.0 / stopwatch.Elapsed.TotalSeconds);

                            lock (syncLock)
                            {
                                totalTps += tps;
                            }
                        }));

                threads[i].Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            Console.WriteLine($"Transactions/sec: {totalTps}");
            Console.WriteLine($"Latency (average): {1.0 / totalTps}");
        }
    }
}
