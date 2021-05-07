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

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Data;
using Neon.IO;
using Neon.Tasks;
using Neon.Xunit;
using Neon.Xunit.Cadence;

using Newtonsoft.Json;
using Xunit;

namespace TestCadence
{
    public partial class Test_EndToEnd
    {
        [Fact(Timeout = CadenceTestHelper.TestTimeout)]
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

        [Fact(Timeout = CadenceTestHelper.TestTimeout)]
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

        [Fact(Timeout = CadenceTestHelper.TestTimeout)]
        [Trait(TestTrait.Category, TestTrait.Slow)]
        public async Task Base_Domain()
        {
            await SyncContext.ClearAsync;

            // Exercise the Cadence domain operations.

            //-----------------------------------------------------------------
            // RegisterDomain:

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.RegisterDomainAsync(name: null));
            await Assert.ThrowsAsync<ArgumentException>(async () => await client.RegisterDomainAsync(name: "domain-0", retentionDays: -1));

            await client.RegisterDomainAsync("domain-0", "this is domain-0", "jeff@lilltek.com", retentionDays: 14);
            await Assert.ThrowsAsync<DomainAlreadyExistsException>(async () => await client.RegisterDomainAsync(name: "domain-0"));

            //-----------------------------------------------------------------
            // DescribeDomain:

            var domainDescribeReply = await client.DescribeDomainAsync("domain-0");

            Assert.False(domainDescribeReply.Configuration.EmitMetrics);
            Assert.Equal(14, domainDescribeReply.Configuration.RetentionDays);
            Assert.Equal("domain-0", domainDescribeReply.DomainInfo.Name);
            Assert.Equal("this is domain-0", domainDescribeReply.DomainInfo.Description);
            Assert.Equal("jeff@lilltek.com", domainDescribeReply.DomainInfo.OwnerEmail);
            Assert.Equal(DomainStatus.Registered, domainDescribeReply.DomainInfo.Status);

            await Assert.ThrowsAsync<EntityNotExistsException>(async () => await client.DescribeDomainAsync("does-not-exist"));

            //-----------------------------------------------------------------
            // UpdateDomain:

            // NOTE: Cadence seems to require some time between creating and then updating
            //       a domain.  We're seeing a:
            //
            //          ServiceBusyException("The domain failovers too frequent.")
            //
            //       when this happens.  We'll mitigate this by adding a long delay here.

            await Task.Delay(TimeSpan.FromSeconds(60));

            var updateDomainRequest = new UpdateDomainRequest();

            updateDomainRequest.Options.EmitMetrics    = true;
            updateDomainRequest.Options.RetentionDays  = 77;
            updateDomainRequest.DomainInfo.OwnerEmail  = "foo@bar.com";
            updateDomainRequest.DomainInfo.Description = "new description";

            await client.UpdateDomainAsync("domain-0", updateDomainRequest);

            domainDescribeReply = await client.DescribeDomainAsync("domain-0");

            Assert.True(domainDescribeReply.Configuration.EmitMetrics);
            Assert.Equal(77, domainDescribeReply.Configuration.RetentionDays);
            Assert.Equal("domain-0", domainDescribeReply.DomainInfo.Name);
            Assert.Equal("new description", domainDescribeReply.DomainInfo.Description);
            Assert.Equal("foo@bar.com", domainDescribeReply.DomainInfo.OwnerEmail);
            Assert.Equal(DomainStatus.Registered, domainDescribeReply.DomainInfo.Status);

            await Assert.ThrowsAsync<EntityNotExistsException>(async () => await client.UpdateDomainAsync("does-not-exist", updateDomainRequest));
        }

        [Fact(Timeout = CadenceTestHelper.TestTimeout)]
        public async Task Base_ListDomains()
        {
            await SyncContext.ClearAsync;

            // Register 100 new domains and then list them in various ways 
            // to verify that works.
            //
            // NOTE: The [test-domain] created by the Cadence fixture will exist
            //       and there may be other domains left over that were created
            //       by other tests.  Cadence also creates at least one domain 
            //       for its own purposes.

            const int testDomainCount = 100;

            for (int i = 0; i < testDomainCount; i++)
            {
                var retentionDays = i + 7;

                if (retentionDays >= 30)
                {
                    retentionDays = 30;
                }

                await client.RegisterDomainAsync($"my-domain-{i}", $"This is my-domain-{i}", $"jeff-{i}@lilltek.com", retentionDays: retentionDays);
            }

            // List all of the domains in one page.

            var domainPage = await client.ListDomainsAsync(testDomainCount * 2);

            Assert.NotNull(domainPage);
            Assert.True(domainPage.Domains.Count >= testDomainCount + 1);
            Assert.Null(domainPage.NextPageToken);

            // Verify that we listed the default domain as well as the 
            // domains we just registered.

            Assert.Contains(domainPage.Domains, domain => domain.DomainInfo.Name == client.Settings.DefaultDomain);

            for (int i = 0; i < testDomainCount; i++)
            {
                Assert.Contains(domainPage.Domains, domain => domain.DomainInfo.Name == $"my-domain-{i}");
            }

            // Verify some of the domain fields for the domains we just registered.

            foreach (var domain in domainPage.Domains)
            {
                if (!domain.DomainInfo.Name.StartsWith("my-domain-"))
                {
                    continue;
                }

                var p  = domain.DomainInfo.Name.LastIndexOf('-');
                var id = int.Parse(domain.DomainInfo.Name.Substring(p + 1));

                Assert.Equal($"This is my-domain-{id}", domain.DomainInfo.Description);
                Assert.Equal($"jeff-{id}@lilltek.com", domain.DomainInfo.OwnerEmail);
                Assert.Equal(DomainStatus.Registered, domain.DomainInfo.Status);
                Assert.True(((7 + id) == domain.Configuration.RetentionDays) || (30 == domain.Configuration.RetentionDays));
            }

            // List all of the domains, one to each page of results.

            var domainCount   = domainPage.Domains.Count;
            var nextPageToken = (byte[])null;

            for (int i = 0; i < domainCount; i++)
            {
                domainPage    = await client.ListDomainsAsync(1, nextPageToken);
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

        [Fact(Timeout = CadenceTestHelper.TestTimeout)]
        public async Task Base_DescribeTaskList()
        {
            await SyncContext.ClearAsync;

            // Verify some information about decision tasks.

            var description = await client.DescribeTaskListAsync(CadenceTestHelper.TaskList, TaskListType.Decision);

            Assert.NotNull(description);

            var poller = description.Pollers.Single(poller => poller.Identity == CadenceTestHelper.ClientIdentity);

            // We're just going to verify that the poller last access time
            // looks reasonable.  This was way off earlier due to not deserializing
            // the time relative to the Unix epoch.

            Assert.True(poller.LastAccessTime >= DateTime.UtcNow - TimeSpan.FromMinutes(30));
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
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

        [Fact(Timeout = CadenceTestHelper.TestTimeout)]
        public async Task Base_DescribeWorkflowExecutionAsync()
        {
            await SyncContext.ClearAsync;

            var utcNow = DateTime.UtcNow;

            // Adjust the time backwards by 5 seconds because we're seeing some
            // differences between the workstation clock and the WSL2 clock?

            utcNow = utcNow - TimeSpan.FromSeconds(5);

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

            Assert.NotNull(description.Status);
            Assert.Equal(workflowId, description.Status.Execution.WorkflowId);
            Assert.NotNull(description.Status.Execution.RunId);
            Assert.Empty(description.PendingActivities);
            Assert.Empty(description.PendingChildren);

            Assert.Equal(CadenceTestHelper.TaskList, description.Configuration.TaskList);

            // Ensure that the status properties are reasonable.

            Assert.True(description.Status.HasStarted);
            Assert.False(description.Status.IsRunning);
            Assert.True(description.Status.IsClosed);

            Assert.True(description.Status.StartTime >= utcNow);
            Assert.True(description.Status.CloseTime >= utcNow);
            Assert.True(description.Status.CloseTime >= description.Status.StartTime);
            Assert.True(description.Status.ExecutionTime <= description.Status.CloseTime - description.Status.StartTime);
        }

        [Fact(Timeout = CadenceTestHelper.TestTimeout)]
        public void Base_ExtractCadenceProxy()
        {
            // Verify that we can extract the [cadence-proxy] binaries.

            using (var folder = new TempFolder())
            {
                CadenceClient.ExtractCadenceProxy(folder.Path);

                var names = new string[]
                {
                    "cadence-proxy.win.exe",
                    "cadence-proxy.linux",
                    "cadence-proxy.osx"
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
