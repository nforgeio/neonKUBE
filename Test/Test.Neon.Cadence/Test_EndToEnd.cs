//-----------------------------------------------------------------------------
// FILE:        Test_EndToEnd.cs
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
    /// Tests low-level <see cref="CadenceClient"/> functionality against the <b>cadence-proxy</b>.
    /// </summary>
    public sealed class Test_EndToEnd : IClassFixture<CadenceFixture>, IDisposable
    {
        //---------------------------------------------------------------------
        // Workflow and Activity classes.

        /// <summary>
        /// <para>
        /// A very simple workflow that accepts an optional UTF-8 encoded argument
        /// string that controls what the workflow does:
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>null</b></term>
        ///     <description>
        ///     Returns <b>workflow: Hello World!</b> encoded as UTF-8 directly from the workflow.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>"activity"</b></term>
        ///     <description>
        ///     Returns <b>activity: Hello World!</b> encoded as UTF-8 from a child activity.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>"local-activity"</b></term>
        ///     <description>
        ///     Returns <b>local-activity: Hello World!</b> encoded as UTF-8 from a <b>local</b> child activity.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>anything else</b></term>
        ///     <description>
        ///     Returns the string passed directly from the workflow.
        ///     </description>
        /// </item>
        /// </list>
        /// </summary>
        private class HelloWorkflow : WorkflowBase
        {
            protected async override Task<byte[]> RunAsync(byte[] args)
            {
                string arg = null;

                if (args != null)
                {
                    arg = Encoding.UTF8.GetString(args);
                }

                if (arg == null)
                {
                    return await Task.FromResult(Encoding.UTF8.GetBytes("workflow: Hello World!"));
                }
                else if (arg == "activity")
                {
                    return await CallActivityAsync<HelloActivity>(Encoding.UTF8.GetBytes("activity: Hello World!"));
                }
                else if (arg == "local-activity")
                {
                    return await CallLocalActivityAsync<HelloActivity>(Encoding.UTF8.GetBytes("local-activity: Hello World!"));
                }
                else
                {
                    return await Task.FromResult(Encoding.UTF8.GetBytes(arg));
                }
            }
        }

        /// <summary>
        /// Test activity that returns the argument passed.
        /// </summary>
        private class HelloActivity : ActivityBase
        {
            protected async override Task<byte[]> RunAsync(byte[] args)
            {
                return await Task.FromResult(args);
            }
        }

        /// <summary>
        /// Verifies that only single copies of mutable values are persisted to
        /// the workflow history.  We're also going to test setting two different 
        /// values to ensure that works as well.
        /// </summary>
        private class MutableValueWorkflow : WorkflowBase
        {
            protected async override Task<byte[]> RunAsync(byte[] args)
            {
                var value1 = await GetValueAsync("value-1", new byte[] { 1 });

                if (value1[0] != 1)
                {
                    throw new Exception($"Test-1: value1={value1[0]}");
                }

                var value2 = await GetValueAsync("value-2", new byte[] { 2 });

                if (value2[0] != 1)
                {
                    throw new Exception($"Test-2, value2={value2[0]}");
                }

                // Verify that we we get the original values back even though
                // we're passing new values.

                value1 = await GetValueAsync("value-1", new byte[] { 3 });

                if (value1[0] != 0)
                {
                    throw new Exception($"Test-3: value1={value1[0]}");
                }

                value2 = await GetValueAsync("value-2", new byte[] { 4 });

                if (value2[0] != 1)
                {
                    throw new Exception($"Test-4, value2={value2[0]}");
                }

                return await Task.FromResult((byte[])null);
            }
        }

        //---------------------------------------------------------------------
        // Test implementations:

        CadenceFixture      fixture;
        CadenceClient       client;
        HttpClient          proxyClient;

        public Test_EndToEnd(CadenceFixture fixture)
        {
            var settings = new CadenceSettings()
            {
                DebugPrelaunched       = true,
                Mode                   = ConnectionMode.ListenOnly,
                Debug                  = true,
                ProxyTimeout           = TimeSpan.FromSeconds(100),
                //DebugHttpTimeout       = TimeSpan.FromSeconds(5),
                DebugDisableHeartbeats = true,
                DebugIgnoreTimeouts    = false
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

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task HelloWorld_Workflow()
        {
            Worker worker = null;

            try
            {
                await client.RegisterDomainAsync("test-domain");

                worker = await client.StartWorkflowWorkerAsync("test-domain");

                await client.RegisterWorkflowAsync<HelloWorkflow>();

                // Run a workflow passing NULL args.

                var workflowRun = await client.StartWorkflowAsync<HelloWorkflow>("test-domain", args: null);
                var result      = await client.GetWorkflowResultAsync(workflowRun);

                Assert.NotNull(result);
                Assert.Equal("workflow: Hello World!", Encoding.UTF8.GetString(result));

                // $debug(jack.burns): DELETE THIS!
                //// Run a workflow passing a string argument.

                //var args = Encoding.UTF8.GetBytes("custom args");

                //workflowRun = await client.StartWorkflowAsync<HelloWorkflow>("test-domain", args: args);
                //result      = await client.GetWorkflowResultAsync(workflowRun);

                //Assert.NotNull(result);
                //Assert.Equal(args, result);
            }
            finally
            {
                if (worker != null)
                {
                    await client.StopWorkerAsync(worker);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task HelloWorld_Activity()
        {
            Worker workflowWorker = null;
            Worker activityWorker = null;

            try
            {
                await client.RegisterDomainAsync("test-domain");

                workflowWorker = await client.StartWorkflowWorkerAsync("test-domain");
                activityWorker = await client.StartActivityWorkerAsync("test-domain");

                await client.RegisterWorkflowAsync<HelloWorkflow>();

                // Run a workflow that invokes an activity.

                var args        = Encoding.UTF8.GetBytes("activity");
                var workflowRun = await client.StartWorkflowAsync<HelloWorkflow>("test-domain", args: Encoding.UTF8.GetBytes("activity"));
                var result      = await client.GetWorkflowResultAsync(workflowRun);

                Assert.NotNull(result);
                Assert.Equal(Encoding.UTF8.GetBytes("activity: Hello World!"), result);
            }
            finally
            {
                if (workflowWorker != null)
                {
                    await client.StopWorkerAsync(workflowWorker);
                }

                if (activityWorker != null)
                {
                    await client.StopWorkerAsync(activityWorker);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task HelloWorld_LocalActivity()
        {
            Worker workflowWorker = null;
            Worker activityWorker = null;

            try
            {
                await client.RegisterDomainAsync("test-domain");

                workflowWorker = await client.StartWorkflowWorkerAsync("test-domain");
                activityWorker = await client.StartWorkflowWorkerAsync("test-domain");

                await client.RegisterWorkflowAsync<HelloWorkflow>();

                // Run a workflow that invokes an activity.

                var args        = Encoding.UTF8.GetBytes("local-activity");
                var workflowRun = await client.StartWorkflowAsync<HelloWorkflow>("test-domain", args: Encoding.UTF8.GetBytes("local-activity"));
                var result      = await client.GetWorkflowResultAsync(workflowRun);

                Assert.NotNull(result);
                Assert.Equal(Encoding.UTF8.GetBytes("local-activity: Hello World!"), result);
            }
            finally
            {
                if (workflowWorker != null)
                {
                    await client.StopWorkerAsync(workflowWorker);
                }

                if (activityWorker != null)
                {
                    await client.StopWorkerAsync(activityWorker);
                }
            }
        }
    }
}
