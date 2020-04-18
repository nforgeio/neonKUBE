//-----------------------------------------------------------------------------
// FILE:        Test_Worker.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
    public class Test_Worker : IClassFixture<TemporalFixture>, IDisposable
    {
        private TemporalFixture  fixture;
        private TemporalClient   client;
        private HttpClient      proxyClient;

        public Test_Worker(TemporalFixture fixture)
        {
            var settings = new TemporalSettings()
            {
                DefaultDomain          = TemporalFixture.DefaultDomain,
                LogLevel               = TemporalTestHelper.LogLevel,
                Debug                  = TemporalTestHelper.Debug,
                DebugPrelaunched       = TemporalTestHelper.DebugPrelaunched,
                DebugDisableHeartbeats = TemporalTestHelper.DebugDisableHeartbeats
            };

            fixture.Start(settings, keepConnection: true);

            this.fixture     = fixture;
            this.client      = fixture.Client;
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Worker()
        {
            await SyncContext.ClearAsync;

            await client.RegisterDomainAsync("test-domain", ignoreDuplicates: true);

            // Verify that creating workers with the same attributes actually
            // return the pre-existing instance with an incremented reference
            // count.

            var activityWorker1 = await client.StartWorkerAsync("tasks1", new WorkerOptions() { DisableWorkflowWorker = true });

            Assert.Equal(0, activityWorker1.RefCount);

            var activityWorker2 = await client.StartWorkerAsync("tasks1", new WorkerOptions() { DisableWorkflowWorker = true });

            Assert.Same(activityWorker1, activityWorker2);
            Assert.Equal(1, activityWorker2.RefCount);

            var workflowWorker1 = await client.StartWorkerAsync("tasks1", new WorkerOptions() { DisableActivityWorker = true });

            Assert.Equal(0, workflowWorker1.RefCount);

            var workflowWorker2 = await client.StartWorkerAsync("tasks1", new WorkerOptions() { DisableActivityWorker = true });

            Assert.Same(workflowWorker1, workflowWorker2);
            Assert.Equal(1, workflowWorker2.RefCount);

            Assert.Same(workflowWorker1, workflowWorker2);
            Assert.Equal(1, workflowWorker2.RefCount);

            var worker1 = await client.StartWorkerAsync("tasks2");

            Assert.Equal(0, worker1.RefCount);

            var worker2 = await client.StartWorkerAsync("tasks2");

            Assert.Same(worker1, worker2);
            Assert.Equal(1, worker2.RefCount);

            // Verify the dispose/refcount behavior.

            activityWorker2.Dispose();
            Assert.False(activityWorker2.IsDisposed);
            Assert.Equal(0, activityWorker2.RefCount);

            activityWorker2.Dispose();
            Assert.True(activityWorker2.IsDisposed);
            Assert.Equal(-1, activityWorker2.RefCount);

            workflowWorker2.Dispose();
            Assert.False(workflowWorker2.IsDisposed);
            Assert.Equal(0, workflowWorker2.RefCount);

            workflowWorker2.Dispose();
            Assert.True(workflowWorker2.IsDisposed);
            Assert.Equal(-1, workflowWorker2.RefCount);

            // Verify that we're not allowed to restart workers.

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await client.StartWorkerAsync("tasks1", new WorkerOptions() { DisableWorkflowWorker = true }));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await client.StartWorkerAsync("tasks1", new WorkerOptions() { DisableActivityWorker = true }));
        }
    }
}
