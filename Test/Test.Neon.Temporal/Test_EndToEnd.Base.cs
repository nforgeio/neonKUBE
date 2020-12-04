//-----------------------------------------------------------------------------
// FILE:        Test_EndToEnd.Activity.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
        [SlowFact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
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

            Console.WriteLine($"Transactions/sec: {tps}");
        }

        [SlowFact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
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

            Console.WriteLine($"Transactions/sec: {totalTps}");
            Console.WriteLine($"Latency (average): {1.0 / totalTps}");
        }

        [Fact(Skip = "Pending Fix")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Base_Domain()
        {
            await SyncContext.ClearAsync;

            // Exercise the Temporal domain operations.

            //-----------------------------------------------------------------
            // RegisterDomain:

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.RegisterNamespaceAsync(name: null));
            await Assert.ThrowsAsync<ArgumentException>(async () => await client.RegisterNamespaceAsync(name: "domain-0", retentionDays: -1));

            await client.RegisterNamespaceAsync("domain-0", "this is domain-0", "jeff@lilltek.com", retentionDays: 14);
            await Assert.ThrowsAsync<NamespaceAlreadyExistsException>(async () => await client.RegisterNamespaceAsync(name: "domain-0"));

            //-----------------------------------------------------------------
            // DescribeDomain:

            var domainDescribeReply = await client.DescribeNamespaceAsync("domain-0");

            Assert.False(domainDescribeReply.Configuration.EmitMetrics);
            Assert.Equal(14, domainDescribeReply.Configuration.RetentionDays);
            Assert.Equal("domain-0", domainDescribeReply.NamespaceInfo.Name);
            Assert.Equal("this is domain-0", domainDescribeReply.NamespaceInfo.Description);
            Assert.Equal("jeff@lilltek.com", domainDescribeReply.NamespaceInfo.OwnerEmail);
            Assert.Equal(NamespaceStatus.Registered, domainDescribeReply.NamespaceInfo.Status);

            await Assert.ThrowsAsync<EntityNotExistsException>(async () => await client.DescribeNamespaceAsync("does-not-exist"));

            //-----------------------------------------------------------------
            // UpdateDomain:

            var updateDomainRequest = new UpdateNamespaceRequest();

            updateDomainRequest.Options.EmitMetrics    = true;
            updateDomainRequest.Options.RetentionDays  = 77;
            updateDomainRequest.NamespaceInfo.OwnerEmail  = "foo@bar.com";
            updateDomainRequest.NamespaceInfo.Description = "new description";

            await client.UpdateNamespaceAsync("domain-0", updateDomainRequest);

            domainDescribeReply = await client.DescribeNamespaceAsync("domain-0");

            Assert.True(domainDescribeReply.Configuration.EmitMetrics);
            Assert.Equal(77, domainDescribeReply.Configuration.RetentionDays);
            Assert.Equal("domain-0", domainDescribeReply.NamespaceInfo.Name);
            Assert.Equal("new description", domainDescribeReply.NamespaceInfo.Description);
            Assert.Equal("foo@bar.com", domainDescribeReply.NamespaceInfo.OwnerEmail);
            Assert.Equal(NamespaceStatus.Registered, domainDescribeReply.NamespaceInfo.Status);

            await Assert.ThrowsAsync<EntityNotExistsException>(async () => await client.UpdateNamespaceAsync("does-not-exist", updateDomainRequest));
        }

        [Fact(Skip = "Pending Fix")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Base_ListDomains()
        {
            await SyncContext.ClearAsync;

            // Register 100 new domains and then list them in various ways 
            // to verify that works.
            //
            // NOTE: The [test-domain] created by the Temporal fixture will exist
            //       and there may be other domains left over that were created
            //       by other tests.  Temporal also creates at least one domain 
            //       for its own purposes.

            const int testDomainCount = 100;

            for (int i = 0; i < testDomainCount; i++)
            {
                var retentionDays = i + 7;

                if (retentionDays >= 30)
                {
                    retentionDays = 30;
                }

                await client.RegisterNamespaceAsync($"my-namespace-{i}", $"This is my-namespace-{i}", $"jeff-{i}@lilltek.com", retentionDays: retentionDays);
            }

            // List all of the domains in one page.

            var domainPage = await client.ListNamespacesAsync(testDomainCount * 2);

            Assert.NotNull(domainPage);
            Assert.True(domainPage.Namespaces.Count >= testDomainCount + 1);
            Assert.Null(domainPage.NextPageToken);

            // Verify that we listed the default namespace as well as the 
            // domains we just registered.

            Assert.Contains(domainPage.Namespaces, d => d.NamespaceInfo.Name == client.Settings.Namespace);

            for (int i = 0; i < testDomainCount; i++)
            {
                Assert.Contains(domainPage.Namespaces, d => d.NamespaceInfo.Name == $"my-namespace-{i}");
            }

            // Verify some of the domain fields for the domains we just registered.

            foreach (var domain in domainPage.Namespaces)
            {
                if (!domain.NamespaceInfo.Name.StartsWith("my-namespace-"))
                {
                    continue;
                }

                var p  = domain.NamespaceInfo.Name.LastIndexOf('-');
                var id = int.Parse(domain.NamespaceInfo.Name.Substring(p + 1));

                Assert.Equal($"This is my-namespace-{id}", domain.NamespaceInfo.Description);
                Assert.Equal($"jeff-{id}@lilltek.com", domain.NamespaceInfo.OwnerEmail);
                Assert.Equal(NamespaceStatus.Registered, domain.NamespaceInfo.Status);
                Assert.True(((7 + id) == domain.Configuration.RetentionDays) || (30 == domain.Configuration.RetentionDays));
            }

            // List all of the domains, one to each page of results.

            var domainCount   = domainPage.Namespaces.Count;
            var nextPageToken = (byte[])null;

            for (int i = 0; i < domainCount; i++)
            {
                domainPage    = await client.ListNamespacesAsync(1, nextPageToken);
                nextPageToken = domainPage.NextPageToken;

                Assert.NotNull(domainPage);

                // We should see a next page token for all pages except
                // for the last.

                if (i < domainCount)
                {
                    Assert.NotNull(domainPage.NextPageToken);
                    Assert.NotEmpty(domainPage.NextPageToken);
                }
                else
                {
                    Assert.Null(domainPage.NextPageToken);
                }
            }
        }

        [Fact(Skip = "Pending Fix")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Base_DescribeTaskQueue()
        {
            await SyncContext.ClearAsync;

            // Verify some information about decision tasks.

            var description = await client.DescribeQueueListAsync(TemporalTestHelper.TaskQueue, TaskQueueType.Decision);

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

        [Fact(Skip = "Pending Fix")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Base_DescribeWorkflowExecutionAsync()
        {
            await SyncContext.ClearAsync;

            var utcNow = DateTime.UtcNow;

            // Execute a workflow and then verify that we can describe it.

            const string workflowId = "my-base-workflow";

            var stub = client.NewWorkflowStub<IBaseWorkflow>(
                new WorkflowOptions() 
                {
                    WorkflowId = workflowId
                });

            await stub.RunAsync();

            var description = await client.DescribeWorkflowExecutionAsync(new WorkflowExecution(workflowId));

            Assert.NotNull(description);

            Assert.NotNull(description.ExeecutionInfo);
            Assert.Equal(workflowId, description.ExeecutionInfo.Execution.WorkflowId);
            Assert.NotNull(description.ExeecutionInfo.Execution.RunId);
            Assert.Empty(description.PendingActivities);
            Assert.Empty(description.PendingChildren);

            Assert.Equal(TemporalTestHelper.TaskQueue, description.Configuration.TaskQueue);

            // Ensure that the status properties are reasonable.

            Assert.True(description.ExeecutionInfo.HasStarted);
            Assert.False(description.ExeecutionInfo.IsRunning);
            Assert.True(description.ExeecutionInfo.IsClosed);

            Assert.True(description.ExeecutionInfo.StartTime >= utcNow);
            Assert.True(description.ExeecutionInfo.CloseTime >= utcNow);
            Assert.True(description.ExeecutionInfo.CloseTime >= description.ExeecutionInfo.StartTime);
            Assert.True(description.ExeecutionInfo.ExecutionTime <= description.ExeecutionInfo.CloseTime - description.ExeecutionInfo.StartTime);
        }

        [Fact(Skip = "Pending Fix")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
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
