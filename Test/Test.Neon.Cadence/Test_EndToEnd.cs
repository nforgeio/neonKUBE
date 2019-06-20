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
using Neon.Time;
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
        private static readonly TimeSpan allowedVariation = TimeSpan.FromMilliseconds(1000);

        //---------------------------------------------------------------------
        // Common workflow and activity classes.

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
        /// This workflow does the same thing as <see cref="HelloWorkflow"/> except that it
        /// executes the child activities by name and not type.
        /// </summary>
        private class HelloWorkflowByName : WorkflowBase
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
                    return await CallActivityAsync("hello-activity", Encoding.UTF8.GetBytes("activity: Hello World!"));
                }
                else if (arg == "local-activity")
                {
                    // NOTE: It's not possible to call local activities by name so we'll
                    // use the type here instead.

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
        /// Executes <see cref="HelloWorkflow"/> as a child to return "workflow: Hello World!".
        /// </summary>
        private class ExecuteChildWorkflow : WorkflowBase
        {
            protected async override Task<byte[]> RunAsync(byte[] args)
            {
                return await CallChildWorkflowAsync<HelloWorkflow>(args: null);
            }
        }

        /// <summary>
        /// Verifies that mutable values ARE NOT updated in the workflow history
        /// when [update=false].  We're also going to test setting two different 
        /// values to ensure that works as well.
        /// </summary>
        private class NonMutableValueWorkflow : WorkflowBase
        {
            protected async override Task<byte[]> RunAsync(byte[] args)
            {
                var value1 = await GetValueAsync("value-1", new byte[] { 1 }, update: false);

                if (value1[0] != 1)
                {
                    throw new Exception($"Test-1: value1={value1[0]}");
                }

                var value2 = await GetValueAsync("value-2", new byte[] { 2 }, update: false);

                if (value2[0] != 2)
                {
                    throw new Exception($"Test-2, value2={value2[0]}");
                }

                // Verify that we get the original values back even though
                // we're passing new values.

                value1 = await GetValueAsync("value-1", new byte[] { 3 }, update: false);

                if (value1[0] != 1)
                {
                    throw new Exception($"Test-3: value1={value1[0]}");
                }

                value2 = await GetValueAsync("value-2", new byte[] { 4 }, update: false);

                if (value2[0] != 2)
                {
                    throw new Exception($"Test-4, value2={value2[0]}");
                }

                return await Task.FromResult((byte[])null);
            }
        }

        /// <summary>
        /// Verifies that mutable values ARE updated in the workflow history
        /// when [update=true].  We're also going to test setting two different 
        /// values to ensure that works as well.
        /// </summary>
        private class MutableValueWorkflow : WorkflowBase
        {
            protected async override Task<byte[]> RunAsync(byte[] args)
            {
                var value1 = await GetValueAsync("value-1", new byte[] { 1 }, update: true);

                if (value1[0] != 1)
                {
                    throw new Exception($"Test-1: value1={value1[0]}");
                }

                var value2 = await GetValueAsync("value-2", new byte[] { 2 }, update: true);

                if (value2[0] != 2)
                {
                    throw new Exception($"Test-2, value2={value2[0]}");
                }

                // Verify that we get the new values back.

                value1 = await GetValueAsync("value-1", new byte[] { 3 }, update: true);

                if (value1[0] != 3)
                {
                    throw new Exception($"Test-3: value1={value1[0]}");
                }

                value2 = await GetValueAsync("value-2", new byte[] { 4 }, update: true);

                if (value2[0] != 4)
                {
                    throw new Exception($"Test-4, value2={value2[0]}");
                }

                // Verify that we get the new last values by passing [update = false].

                value1 = await GetValueAsync("value-1", new byte[] { 5 }, update: false);

                if (value1[0] != 3)
                {
                    throw new Exception($"Test-3: value1={value1[0]}");
                }

                value2 = await GetValueAsync("value-2", new byte[] { 6 }, update: false);

                if (value2[0] != 4)
                {
                    throw new Exception($"Test-4, value2={value2[0]}");
                }

                return await Task.FromResult((byte[])null);
            }
        }

        /// <summary>
        /// Runs two child workflows in parallel and returns "Hello World!" as the
        /// result.
        /// </summary>
        private class ParallelChildWorkflows : WorkflowBase
        {
            protected async override Task<byte[]> RunAsync(byte[] args)
            {
                var child1  = await StartChildWorkflowAsync<HelloWorkflow>(Encoding.UTF8.GetBytes("Hello"));
                var child2  = await StartChildWorkflowAsync<HelloWorkflow>(Encoding.UTF8.GetBytes("World!"));
                var result1 = await WaitForChildWorkflowAsync(child1);
                var result2 = await WaitForChildWorkflowAsync(child2);

                return Encoding.UTF8.GetBytes($"{Encoding.UTF8.GetString(result1)} {Encoding.UTF8.GetString(result2)}");
            }
        }

        /// <summary>
        /// Returns the workflow properties as an encoded dictionary with the
        /// property values converted to strings.
        /// </summary>
        private class GetPropertiesWorkflow : WorkflowBase
        {
            protected async override Task<byte[]> RunAsync(byte[] args)
            {
                var properties = new Dictionary<string, string>();

                properties.Add(nameof(Version), Version.ToString());
                properties.Add(nameof(OriginalVersion), OriginalVersion.ToString());
                properties.Add(nameof(Domain), Domain);
                properties.Add(nameof(WorkflowId), WorkflowId);
                properties.Add(nameof(RunId), RunId);
                properties.Add(nameof(WorkflowTypeName), WorkflowTypeName);
                properties.Add(nameof(TaskList), TaskList);

                return await Task.FromResult(NeonHelper.JsonSerializeToBytes(properties));
            }
        }

        /// <summary>
        /// Returns the current time (UTC) as JSON.
        /// </summary>
        private class GetUtcNowWorkflow : WorkflowBase
        {
            protected async override Task<byte[]> RunAsync(byte[] args)
            {
                return NeonHelper.JsonSerializeToBytes(await UtcNowAsync());
            }
        }

        /// <summary>
        /// Sleeps for the timespan passed as JSON.  The result is a list of two
        /// DateTime instances.  The first is the time UTC just before sleeping
        /// and the second is the time just after sleeping.
        /// </summary>
        private class SleepWorkflow : WorkflowBase
        {
            protected async override Task<byte[]> RunAsync(byte[] args)
            {
                var sleepTime  = NeonHelper.JsonDeserialize<TimeSpan>(args);
                var beforeTime = DateTime.UtcNow;

                await SleepAsync(sleepTime);

                var afterTime = DateTime.UtcNow;
                var times     = new List<DateTime>() { beforeTime, afterTime };
                
                return NeonHelper.JsonSerializeToBytes(times);
            }
        }

        /// <summary>
        /// Sleeps until the time passed as JSON.  The result is a list of two
        /// DateTime instances.  The first is the time UTC just before sleeping
        /// and the second is the time just after sleeping.
        /// </summary>
        private class SleepUntilWorkflow : WorkflowBase
        {
            protected async override Task<byte[]> RunAsync(byte[] args)
            {
                var wakeTimeUtc = NeonHelper.JsonDeserialize<DateTime>(args);
                var beforeTime  = DateTime.UtcNow;

                await SleepUntilUtcAsync(wakeTimeUtc);

                var afterTime = DateTime.UtcNow;
                var times     = new List<DateTime>() { beforeTime, afterTime };

                return NeonHelper.JsonSerializeToBytes(times);
            }
        }

        /// <summary>
        /// This workflow is never registered and is used for testing related scenarios.
        /// </summary>
        private class UnregisteredWorkflow : WorkflowBase
        {
            protected async override Task<byte[]> RunAsync(byte[] args)
            {
                return await Task.FromResult((byte[])null);
            }
        }

        /// <summary>
        /// This workflow restarts if a non-null argument is passed and also keeps track of
        /// the number of times the workflow was executed.
        /// </summary>
        private class RestartableWorkflow : WorkflowBase
        {
            public static int ExecutionCount = 0;

            protected async override Task<byte[]> RunAsync(byte[] args)
            {
                ExecutionCount++;

                if (args != null)
                {
                    // Pass [args=null] so the next run won't restart.

                    await RestartAsync(null);
                }

                return await Task.FromResult(Encoding.UTF8.GetBytes("Hello World!"));
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
                DebugPrelaunched       = false,
                Mode                   = ConnectionMode.ListenOnly,
                Debug                  = true,
                ProxyTimeout           = TimeSpan.FromSeconds(30),
                //DebugHttpTimeout     = TimeSpan.FromSeconds(5),
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
        public async Task HelloWorld_Workflow_ByType()
        {
            await client.RegisterDomainAsync("test-domain", ignoreDuplicates: true);

            using (var worker = await client.StartWorkflowWorkerAsync("test-domain"))
            {
                await client.RegisterWorkflowAsync<HelloWorkflow>();

                // Run a workflow passing NULL args.

                var workflowRun = await client.StartWorkflowAsync<HelloWorkflow>("test-domain", args: null);
                var result      = await client.GetWorkflowResultAsync(workflowRun);

                Assert.NotNull(result);
                Assert.Equal("workflow: Hello World!", Encoding.UTF8.GetString(result));

                // Run a workflow passing a string argument.

                var args = Encoding.UTF8.GetBytes("custom args");

                workflowRun = await client.StartWorkflowAsync<HelloWorkflow>("test-domain", args: args);
                result      = await client.GetWorkflowResultAsync(workflowRun);

                Assert.NotNull(result);
                Assert.Equal(args, result);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task HelloWorld_Workflow_ByName()
        {
            await client.RegisterDomainAsync("test-domain", ignoreDuplicates: true);

            using (var worker = await client.StartWorkflowWorkerAsync("test-domain"))
            {
                await client.RegisterWorkflowAsync<HelloWorkflow>("hello-workflow-by-name");

                // Run a workflow passing NULL args.

                var workflowRun = await client.StartWorkflowAsync("hello-workflow-by-name", "test-domain", args: null);
                var result      = await client.GetWorkflowResultAsync(workflowRun);

                Assert.NotNull(result);
                Assert.Equal("workflow: Hello World!", Encoding.UTF8.GetString(result));

                // Run a workflow passing a string argument.

                var args = Encoding.UTF8.GetBytes("custom args");

                workflowRun = await client.StartWorkflowAsync("hello-workflow-by-name", "test-domain", args: args);
                result      = await client.GetWorkflowResultAsync(workflowRun);

                Assert.NotNull(result);
                Assert.Equal(args, result);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task HelloWorld_Activity_ByType()
        {
            await client.RegisterDomainAsync("test-domain", ignoreDuplicates: true);

            using (var workflowWorker = await client.StartWorkflowWorkerAsync("test-domain"))
            {
                using (var activityWorker = await client.StartActivityWorkerAsync("test-domain"))
                {
                    await client.RegisterWorkflowAsync<HelloWorkflow>();
                    await client.RegisterActivityAsync<HelloActivity>();

                    // Run a workflow that invokes an activity.

                    var args        = Encoding.UTF8.GetBytes("activity");
                    var workflowRun = await client.StartWorkflowAsync<HelloWorkflow>("test-domain", args: Encoding.UTF8.GetBytes("activity"));
                    var result      = await client.GetWorkflowResultAsync(workflowRun);

                    Assert.NotNull(result);
                    Assert.Equal(Encoding.UTF8.GetBytes("activity: Hello World!"), result);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task HelloWorld_Activity_ByName()
        {
            await client.RegisterDomainAsync("test-domain", ignoreDuplicates: true);

            using (var workflowWorker = await client.StartWorkflowWorkerAsync("test-domain"))
            {
                using (var activityWorker = await client.StartActivityWorkerAsync("test-domain"))
                {
                    await client.RegisterWorkflowAsync<HelloWorkflowByName>("hello-workflow-by-name");
                    await client.RegisterActivityAsync<HelloActivity>("hello-activity");

                    // Run a workflow that invokes an activity.

                    var args        = Encoding.UTF8.GetBytes("activity");
                    var workflowRun = await client.StartWorkflowAsync("hello-workflow-by-name", "test-domain", args: Encoding.UTF8.GetBytes("activity"));
                    var result      = await client.GetWorkflowResultAsync(workflowRun);

                    Assert.NotNull(result);
                    Assert.Equal(Encoding.UTF8.GetBytes("activity: Hello World!"), result);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task HelloWorld_LocalActivity_ByType()
        {
            await client.RegisterDomainAsync("test-domain", ignoreDuplicates: true);

            using (var workflowWorker = await client.StartWorkflowWorkerAsync("test-domain"))
            {
                using (var activityWorker = await client.StartActivityWorkerAsync("test-domain"))
                {
                    await client.RegisterWorkflowAsync<HelloWorkflow>();

                    // Run a workflow that invokes an activity.

                    var args        = Encoding.UTF8.GetBytes("local-activity");
                    var workflowRun = await client.StartWorkflowAsync<HelloWorkflow>("test-domain", args: Encoding.UTF8.GetBytes("local-activity"));
                    var result      = await client.GetWorkflowResultAsync(workflowRun);

                    Assert.NotNull(result);
                    Assert.Equal(Encoding.UTF8.GetBytes("local-activity: Hello World!"), result);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task HelloWorld_ChildWorkflow_ByType()
        {
            // Move register domain up above workers
            await client.RegisterDomainAsync("test-domain", ignoreDuplicates: true);

            using (var workflowWorker = await client.StartWorkflowWorkerAsync("test-domain"))
            {
                using (var activityWorker = await client.StartActivityWorkerAsync("test-domain"))
                {
                    // Run a workflow that invokes a child workflow.

                    await client.RegisterWorkflowAsync<ExecuteChildWorkflow>();
                    await client.RegisterWorkflowAsync<HelloWorkflow>();

                    var args        = Encoding.UTF8.GetBytes("local-activity");
                    var workflowRun = await client.StartWorkflowAsync<ExecuteChildWorkflow>("test-domain", args: null);

                    var result      = await client.GetWorkflowResultAsync(workflowRun);

                    Assert.NotNull(result);
                    Assert.Equal(Encoding.UTF8.GetBytes("workflow: Hello World!"), result);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_NonMutableValue()
        {
            await client.RegisterDomainAsync("test-domain", ignoreDuplicates: true);

            using (var workflowWorker = await client.StartWorkflowWorkerAsync("test-domain"))
            {
                await client.RegisterWorkflowAsync<NonMutableValueWorkflow>();

                // Verify that non-mutable workflow values work as expected.
                // The workflow will throw an exception if there's a problem.

                var workflowRun = await client.StartWorkflowAsync<NonMutableValueWorkflow>("test-domain", args: null);

                await client.GetWorkflowResultAsync(workflowRun);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_MutableValue()
        {
            await client.RegisterDomainAsync("test-domain", ignoreDuplicates: true);

            using (var workflowWorker = await client.StartWorkflowWorkerAsync("test-domain"))
            {
                await client.RegisterWorkflowAsync<MutableValueWorkflow>();

                // Verify that mutable workflow values work as expected.
                // The workflow will throw an exception if there's a problem.

                var workflowRun = await client.StartWorkflowAsync<MutableValueWorkflow>("test-domain", args: null);

                await client.GetWorkflowResultAsync(workflowRun);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_ParallelChildWorkflows()
        {
            await client.RegisterDomainAsync("test-domain", ignoreDuplicates: true);

            using (var worker = await client.StartWorkflowWorkerAsync("test-domain"))
            {
                await client.RegisterWorkflowAsync<ParallelChildWorkflows>();
                await client.RegisterWorkflowAsync<HelloWorkflow>();

                var workflowRun = await client.StartWorkflowAsync<ParallelChildWorkflows>("test-domain", args: null);
                var result      = await client.GetWorkflowResultAsync(workflowRun);

                Assert.NotNull(result);
                Assert.Equal("Hello World!", Encoding.UTF8.GetString(result));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Properties()
        {
            await client.RegisterDomainAsync("test-domain", ignoreDuplicates: true);

            using (var worker = await client.StartWorkflowWorkerAsync("test-domain"))
            {
                await client.RegisterWorkflowAsync<GetPropertiesWorkflow>();

                var workflowRun = await client.StartWorkflowAsync<GetPropertiesWorkflow>("test-domain", args: null, options: new WorkflowOptions() { WorkflowId = "my-workflow" });
                var result      = await client.GetWorkflowResultAsync(workflowRun);
                var properties  = NeonHelper.JsonDeserialize<Dictionary<string, string>>(result);

                Assert.Equal("0.0.0", properties["Version"]);
                Assert.Equal("0.0.0", properties["OriginalVersion"]);
                Assert.Equal("test-domain", properties["Domain"]);
                Assert.Equal("my-workflow", properties["WorkflowId"]);
                Assert.NotNull(properties["RunId"]);
                Assert.NotEmpty(properties["RunId"]);
                Assert.NotEqual("my-workflow", properties["RunId"]);
                Assert.Equal(typeof(GetPropertiesWorkflow).FullName, properties["WorkflowTypeName"]);
                Assert.Equal("default", properties["TaskList"]);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_UtcNow()
        {
            await client.RegisterDomainAsync("test-domain", ignoreDuplicates: true);

            using (var worker = await client.StartWorkflowWorkerAsync("test-domain"))
            {
                await client.RegisterWorkflowAsync<GetUtcNowWorkflow>();

                var workflowRun    = await client.StartWorkflowAsync<GetUtcNowWorkflow>("test-domain", args: null);
                var nowJsonBytes   = await client.GetWorkflowResultAsync(workflowRun);
                var workflowNowUtc = NeonHelper.JsonDeserialize<DateTime>(nowJsonBytes);
                var nowUtc         = DateTime.UtcNow;

                Assert.True(nowUtc - workflowNowUtc < allowedVariation);
                Assert.True(workflowNowUtc - nowUtc < allowedVariation);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Sleep()
        {
            await client.RegisterDomainAsync("test-domain", ignoreDuplicates: true);

            using (var worker = await client.StartWorkflowWorkerAsync("test-domain"))
            {
                await client.RegisterWorkflowAsync<SleepWorkflow>();

                var sleepTime = TimeSpan.FromSeconds(5);
                var workflowRun = await client.StartWorkflowAsync<SleepWorkflow>("test-domain", args: NeonHelper.JsonSerializeToBytes(sleepTime));
                var nowJsonBytes = await client.GetWorkflowResultAsync(workflowRun);
                var times = NeonHelper.JsonDeserialize<List<DateTime>>(nowJsonBytes);

                Assert.True(times[1] > times[0]);
                Assert.True(times[1] - times[0] - sleepTime < allowedVariation);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_SleepUntil()
        {
            await client.RegisterDomainAsync("test-domain", ignoreDuplicates: true);

            using (var worker = await client.StartWorkflowWorkerAsync("test-domain"))
            {
                await client.RegisterWorkflowAsync<SleepUntilWorkflow>();

                var sleepTime    = TimeSpan.FromSeconds(5);
                var wakeTime     = DateTime.UtcNow + sleepTime;
                var workflowRun  = await client.StartWorkflowAsync<SleepUntilWorkflow>("test-domain", args: NeonHelper.JsonSerializeToBytes(wakeTime));
                var nowJsonBytes = await client.GetWorkflowResultAsync(workflowRun);
                var times        = NeonHelper.JsonDeserialize<List<DateTime>>(nowJsonBytes);

                Assert.True(times[1] > times[0]);
                Assert.True(times[1] - times[0] - sleepTime < allowedVariation);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_StartTimeout()
        {
            // Verify that we see a [CadenceTimeoutException] when we try to execute 
            // and unregistered workflow.  This als ensures that the workflow honors
            // [WorkflowOptions.ExecutionStartToCloseTimeout].

            await client.RegisterDomainAsync("test-domain", ignoreDuplicates: true);

            using (var worker = await client.StartWorkflowWorkerAsync("test-domain"))
            {
                // Warm up Cadence by registering an running another workflow.  This will
                // help make our timeout timing more accurate and repeatable.

                await client.RegisterWorkflowAsync<GetUtcNowWorkflow>();
                await client.StartWorkflowAsync<GetUtcNowWorkflow>("test-domain", args: null);

                // This is the actual test.

                var executeTimeout = TimeSpan.FromSeconds(5);
                var startTime      = DateTime.UtcNow;

                try
                {
                    await client.StartWorkflowAsync<UnregisteredWorkflow>("test-domain", options: new WorkflowOptions() { ExecutionStartToCloseTimeout = executeTimeout });
                }
                catch (CadenceTimeoutException)
                {
                    var endTime = DateTime.UtcNow;

                    // Ensure that [ExecutionStartToCloseTimeout] and that we got the exception
                    // close to 5 seconds after we attempted to execution.
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_GetResult()
        {
            // Verify that we can retrieve a workflow result after it has completed execution.

            await client.RegisterDomainAsync("test-domain", ignoreDuplicates: true);

            using (var worker = await client.StartWorkflowWorkerAsync("test-domain"))
            {
                await client.RegisterWorkflowAsync<HelloWorkflow>();

                // Run a workflow passing NULL args and verify.

                var workflowRun = await client.StartWorkflowAsync<HelloWorkflow>("test-domain", args: null);
                var result      = await client.GetWorkflowResultAsync(workflowRun);

                Assert.NotNull(result);
                Assert.Equal("workflow: Hello World!", Encoding.UTF8.GetString(result));

                // Now retrieve the result from the completed workflow and verify.

                result = await client.GetWorkflowResultAsync(workflowRun);

                Assert.NotNull(result);
                Assert.Equal("workflow: Hello World!", Encoding.UTF8.GetString(result));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_GetResultError()
        {
            // Verify that an exception is thrown when waiting on a non-existent workflow.

            await client.RegisterDomainAsync("test-domain", ignoreDuplicates: true);

            using (var worker = await client.StartWorkflowWorkerAsync("test-domain"))
            {
                var workflowRun = new WorkflowRun("not-present", "not-here");

                await Assert.ThrowsAsync<CadenceBadRequestException>(async () => await client.GetWorkflowResultAsync(workflowRun));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_Restart()
        {
            // Verify that we can a workflow can restart itself.

            await client.RegisterDomainAsync("test-domain", ignoreDuplicates: true);

            using (var worker = await client.StartWorkflowWorkerAsync("test-domain"))
            {
                await client.RegisterWorkflowAsync<RestartableWorkflow>();

                // Clear the execution count, run a restarting workflow, and then
                // verify that it executed twice.

                RestartableWorkflow.ExecutionCount = 0;

                var workflowRun = await client.StartWorkflowAsync<RestartableWorkflow>("test-domain", args: new byte[] { 1 });
                var result      = await client.GetWorkflowResultAsync(workflowRun);

                Assert.NotNull(result);
                Assert.Equal("Hello World!", Encoding.UTF8.GetString(result));
                Assert.Equal(2, RestartableWorkflow.ExecutionCount);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void CronScheduleClass()
        {
            // Verify that the [CronSchedule] class works as expected.

            //---------------------------------------------
            // Verify that it checks for invalid values.

            Assert.Throws<ArgumentException>(() => (new CronSchedule() { DayOfMonth = -1 }).ToInternal());
            Assert.Throws<ArgumentException>(() => (new CronSchedule() { DayOfMonth = 0 }).ToInternal());
            Assert.Throws<ArgumentException>(() => (new CronSchedule() { DayOfMonth = 32 }).ToInternal());

            Assert.Throws<ArgumentException>(() => (new CronSchedule() { Month = -1 }).ToInternal());
            Assert.Throws<ArgumentException>(() => (new CronSchedule() { Month = 0 }).ToInternal());
            Assert.Throws<ArgumentException>(() => (new CronSchedule() { Month = 13 }).ToInternal());

            Assert.Throws<ArgumentException>(() => (new CronSchedule() { Hour = -1 }).ToInternal());
            Assert.Throws<ArgumentException>(() => (new CronSchedule() { Hour = 24 }).ToInternal());

            Assert.Throws<ArgumentException>(() => (new CronSchedule() { Minute = -1 }).ToInternal());
            Assert.Throws<ArgumentException>(() => (new CronSchedule() { Minute = 60 }).ToInternal());

            //---------------------------------------------
            // Verify that various schedules render properly.

            Assert.Null((new CronSchedule()).ToInternal());
            Assert.Equal("1 * * * *", (new CronSchedule() { Minute = 1 } ).ToInternal());
            Assert.Equal("* 2 * * *", (new CronSchedule() { Hour = 2 } ).ToInternal());
            Assert.Equal("* * 3 * *", (new CronSchedule() { DayOfMonth = 3 } ).ToInternal());
            Assert.Equal("* * * 4 *", (new CronSchedule() { Month = 4 } ).ToInternal());
            Assert.Equal("* * * * 5", (new CronSchedule() { DayOfWeek = DayOfWeek.Friday } ).ToInternal());
            Assert.Equal("1 2 3 4 5", (new CronSchedule() { Minute = 1, Hour = 2, DayOfMonth = 3, Month = 4, DayOfWeek = DayOfWeek.Friday } ).ToInternal());

        }
    }
}
