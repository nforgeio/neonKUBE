//-----------------------------------------------------------------------------
// FILE:        Test_EndToEnd.Activity.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Data;
using Neon.IO;
using Neon.Tasks;
using Neon.Temporal;
using Neon.Temporal.Internal;
using Neon.Xunit;
using Neon.Xunit.Temporal;

using Newtonsoft.Json;
using Xunit;

namespace TestTemporal
{
    public partial class Test_EndToEnd
    {
        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public async Task Base_Ping()
        {
            await SyncContext.ClearAsync;

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

            testWriter.WriteLine($"Transactions/sec: {tps}");
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Base_PingAttack()
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

            testWriter.WriteLine($"Transactions/sec: {totalTps}");
            testWriter.WriteLine($"Latency (average): {1.0 / totalTps}");
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        [Trait(TestTrait.Slow, "true")]
        public async Task Base_Namespace()
        {
            await SyncContext.ClearAsync;

            // Exercise the Temporal namespace operations.

            //-----------------------------------------------------------------
            // RegisterNamespace:

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.RegisterNamespaceAsync(name: null));
            await Assert.ThrowsAsync<ArgumentException>(async () => await client.RegisterNamespaceAsync(name: "namespace-0", retentionDays: -1));

            await client.RegisterNamespaceAsync("namespace-0", "this is namespace-0", "jeff@lilltek.com", retentionDays: 14);
            
            // TODO -- JACK REMOVE THIS
            //await Assert.ThrowsAsync<NamespaceAlreadyExistsException>(async () => await client.RegisterNamespaceAsync(name: "namespace-0"));

            //-----------------------------------------------------------------
            // DescribeNamespace:

            var namespaceDescribeReply = await client.DescribeNamespaceAsync("namespace-0");

            Assert.Equal(ArchivalState.Disabled, namespaceDescribeReply.Config.HistoryArchivalState);
            Assert.Equal(TimeSpan.FromDays(14), namespaceDescribeReply.Config.WorkflowExecutionRetentionTtl);
            Assert.Equal("namespace-0", namespaceDescribeReply.NamespaceInfo.Name);
            Assert.Equal("this is namespace-0", namespaceDescribeReply.NamespaceInfo.Description);
            Assert.Equal("jeff@lilltek.com", namespaceDescribeReply.NamespaceInfo.OwnerEmail);
            Assert.Equal(NamespaceState.Registered, namespaceDescribeReply.NamespaceInfo.State);

            // TODO -- JACK REMOVE THIS
            //await Assert.ThrowsAsync<EntityNotExistsException>(async () => await client.DescribeNamespaceAsync("does-not-exist"));

            //-----------------------------------------------------------------
            // UpdateNamespace:

            // NOTE: Temporal seems to require some time between creating and then updating
            //       a domain.  We're seeing a:
            //
            //          ServiceBusyException("The domain failovers too frequent.")
            //
            //       when this happens.  We'll mitigate this by adding a long delay here.

            await Task.Delay(TimeSpan.FromSeconds(60));

            var updateNamespaceRequest = new UpdateNamespaceRequest();

            updateNamespaceRequest.Config.HistoryArchivalState = ArchivalState.Enabled;
            updateNamespaceRequest.Config.WorkflowExecutionRetentionTtl = TimeSpan.FromDays(88);
            updateNamespaceRequest.UpdateInfo.OwnerEmail = "foo@bar.com";
            updateNamespaceRequest.UpdateInfo.Description = "new description";

            await client.UpdateNamespaceAsync("namespace-0", updateNamespaceRequest);

            namespaceDescribeReply = await client.DescribeNamespaceAsync("namespace-0");

            Assert.Equal(ArchivalState.Enabled, namespaceDescribeReply.Config.HistoryArchivalState);
            Assert.Equal(TimeSpan.FromDays(88), namespaceDescribeReply.Config.WorkflowExecutionRetentionTtl);
            Assert.Equal("namespace-0", namespaceDescribeReply.NamespaceInfo.Name);
            Assert.Equal("new description", namespaceDescribeReply.NamespaceInfo.Description);
            Assert.Equal("foo@bar.com", namespaceDescribeReply.NamespaceInfo.OwnerEmail);
            Assert.Equal(NamespaceState.Registered, namespaceDescribeReply.NamespaceInfo.State);

            // TODO -- JACK REMOVE THIS
            //await Assert.ThrowsAsync<EntityNotExistsException>(async () => await client.UpdateNamespaceAsync("does-not-exist", updateNamespaceRequest));
        }

        [Fact_NotImplemented]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public async Task Base_ListNamespaces()
        {
            await SyncContext.ClearAsync;

            // Register 100 new namespaces and then list them in various ways 
            // to verify that works.
            //
            // NOTE: The [test-namespace] created by the Temporal fixture will exist
            //       and there may be other namespaces left over that were created
            //       by other tests.  Temporal also creates at least one namespace 
            //       for its own purposes.

            const int testNamespaceCount = 100;

            for (int i = 0; i < testNamespaceCount; i++)
            {
                var retentionDays = i + 7;

                if (retentionDays >= 30)
                {
                    retentionDays = 30;
                }

                await client.RegisterNamespaceAsync($"my-namespace-{i}", $"This is my-namespace-{i}", $"jeff-{i}@lilltek.com", retentionDays: retentionDays);
            }

            // List all of the namespaces in one page.

            var namespacePage = await client.ListNamespacesAsync(testNamespaceCount * 2);

            Assert.NotNull(namespacePage);
            Assert.True(namespacePage.Namespaces.Count >= testNamespaceCount + 1);
            Assert.Null(namespacePage.NextPageToken);

            // Verify that we listed the default namespace as well as the 
            // namespaces we just registered.

            Assert.Contains(namespacePage.Namespaces, d => d.NamespaceInfo.Name == client.Settings.Namespace);

            for (int i = 0; i < testNamespaceCount; i++)
            {
                Assert.Contains(namespacePage.Namespaces, d => d.NamespaceInfo.Name == $"my-namespace-{i}");
            }

            // Verify some of the namespace fields for the namespaces we just registered.

            foreach (var @namespace in namespacePage.Namespaces)
            {
                if (!@namespace.NamespaceInfo.Name.StartsWith("my-namespace-"))
                {
                    continue;
                }

                var p  = @namespace.NamespaceInfo.Name.LastIndexOf('-');
                var id = int.Parse(@namespace.NamespaceInfo.Name.Substring(p + 1));

                Assert.Equal($"This is my-namespace-{id}", @namespace.NamespaceInfo.Description);
                Assert.Equal($"jeff-{id}@lilltek.com", @namespace.NamespaceInfo.OwnerEmail);
                Assert.Equal(NamespaceState.Registered, @namespace.NamespaceInfo.State);
                Assert.True((TimeSpan.FromSeconds(7 + id) == @namespace.Config.WorkflowExecutionRetentionTtl) || (TimeSpan.FromSeconds(30) == @namespace.Config.WorkflowExecutionRetentionTtl));
            }

            // List all of the namespaces, one to each page of results.

            var namespceCount = namespacePage.Namespaces.Count;
            var nextPageToken = (byte[])null;

            for (int i = 0; i < namespceCount; i++)
            {
                namespacePage    = await client.ListNamespacesAsync(1, nextPageToken);
                nextPageToken = namespacePage.NextPageToken;

                Assert.NotNull(namespacePage);

                // We should see a next page token for all pages except
                // for the last.

                if (i < namespceCount)
                {
                    Assert.NotNull(namespacePage.NextPageToken);
                    Assert.NotEmpty(namespacePage.NextPageToken);
                }
                else
                {
                    Assert.Null(namespacePage.NextPageToken);
                }
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public async Task Base_DescribeTaskQueue()
        {
            await SyncContext.ClearAsync;

            // Verify some information about decision tasks.

            var description = await client.DescribeQueueListAsync(TemporalTestHelper.TaskQueue, TaskQueueType.Workflow);

            Assert.NotNull(description);

            var poller = description.Pollers.Single(p => p.Identity == TemporalTestHelper.ClientIdentity);

            // We're just going to verify that the poller last access time
            // looks reasonable.  This was way off earlier due to not deserializing
            // the time relative to the Unix epoch.

            Assert.True(poller.LastAccessTime >= DateTime.UtcNow - TimeSpan.FromMinutes(30));
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskQueue = TemporalTestHelper.TaskQueue)]
        public interface IBaseWorkflow : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class BaseWorkflow : WorkflowBase, IBaseWorkflow
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public async Task Base_DescribeWorkflowExecutionAsync()
        {
            await SyncContext.ClearAsync;

            var utcNow = DateTime.UtcNow;

            // Execute a workflow and then verify that we can describe it.

            const string workflowId = "my-base-workflow";

            var stub = client.NewWorkflowStub<IBaseWorkflow>(
                new StartWorkflowOptions() 
                {
                    Id = workflowId
                });

            await stub.RunAsync();

            var description = await client.DescribeWorkflowExecutionAsync(new WorkflowExecution(workflowId));

            Assert.NotNull(description);

            Assert.NotNull(description.WorkflowExecutionInfo);
            Assert.Equal(workflowId, description.WorkflowExecutionInfo.Execution.WorkflowId);
            Assert.NotNull(description.WorkflowExecutionInfo.Execution.RunId);
            Assert.Empty(description.PendingActivities);
            Assert.Empty(description.PendingChildren);

            Assert.Equal(TemporalTestHelper.TaskQueue, description.ExecutionConfig.TaskQueue.Name);

            // Ensure that the status properties are reasonable.

            Assert.True(description.WorkflowExecutionInfo.HasStarted);
            Assert.False(description.WorkflowExecutionInfo.IsRunning);
            Assert.True(description.WorkflowExecutionInfo.IsClosed);

            Assert.True(description.WorkflowExecutionInfo.StartTime >= utcNow);
            Assert.True(description.WorkflowExecutionInfo.CloseTime >= utcNow);
            Assert.True(description.WorkflowExecutionInfo.CloseTime >= description.WorkflowExecutionInfo.StartTime);
            Assert.True(description.WorkflowExecutionInfo.ExecutionTime.Value.Ticks >= (description.WorkflowExecutionInfo.CloseTime - description.WorkflowExecutionInfo.StartTime).Value.Ticks);
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Base_ExtractTemporalProxy()
        {
            // Verify that we can extract the [temporal-proxy] binaries.

            using (var folder = new TempFolder())
            {
                TemporalClient.ExtractTemporalProxy(folder.Path);

                var names = new string[]
                {
                    "temporal-proxy.win.exe",
                    "temporal-proxy.linux",
                    "temporal-proxy.osx"
                };

                foreach (var name in names)
                {
                    var fullPath = Path.Combine(folder.Path, name);

                    Assert.True(File.Exists(fullPath));
                    Assert.True(new FileInfo(fullPath).Length > 0);
                }
            }
        }
    }
}
