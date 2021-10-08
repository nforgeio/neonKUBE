//-----------------------------------------------------------------------------
// FILE:        Test_StubBuilders.cs
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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.Data;
using Neon.IO;
using Neon.Temporal;
using Neon.Temporal.Internal;
using Neon.Xunit;
using Neon.Xunit.Temporal;

using Newtonsoft.Json;
using Xunit;

namespace TestTemporal
{
    [Trait(TestTrait.Category, TestTrait.Buggy)]
    [Trait(TestTrait.Category, TestTrait.Incomplete)]
    [Trait(TestTrait.Category, TestArea.NeonTemporal)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public partial class Test_StubBuilders : IClassFixture<TemporalFixture>, IDisposable
    {
        private const int maxWaitSeconds = 5;

        private static readonly TimeSpan allowedVariation = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan workflowTimeout  = TimeSpan.FromSeconds(20);

        private TemporalFixture     fixture;
        private TemporalClient      client;
        private HttpClient          proxyClient;

        [ActivityInterface]
        public interface ITestActivity : IActivity
        {
            [ActivityMethod]
            Task Test();
        }

        [WorkflowInterface]
        public interface ITestWorkflow : IWorkflow
        {
            [WorkflowMethod]
            Task Test();
        }

        public Test_StubBuilders(TemporalFixture fixture)
        {
            TestHelper.ResetDocker(this.GetType());

            // Initialize the Cadence fixture.

            var settings = new TemporalSettings()
            {
                Namespace              = TemporalFixture.Namespace,
                ProxyLogLevel          = TemporalTestHelper.ProxyLogLevel,
                CreateNamespace        = true,
                Debug                  = TemporalTestHelper.Debug,
                DebugPrelaunched       = TemporalTestHelper.DebugPrelaunched,
                DebugDisableHeartbeats = TemporalTestHelper.DebugDisableHeartbeats,
                ClientIdentity         = TemporalTestHelper.ClientIdentity,
                TaskQueue       = TemporalTestHelper.TaskQueue,
            };

            if (fixture.Start(settings, composeFile: TemporalTestHelper.TemporalStackDefinition, reconnect: true, keepRunning: TemporalTestHelper.KeepTemporalServerOpen) == TestFixtureStatus.Started)
            {
                this.fixture     = fixture;
                this.client      = fixture.Client;
                this.proxyClient = new HttpClient() { BaseAddress = client.ProxyUri };

                // Create a worker and register the workflow and activity 
                // implementations to let Temporal know we're open for business.

                var worker = client.NewWorkerAsync().Result;

                worker.RegisterAssemblyAsync(Assembly.GetExecutingAssembly()).Wait();
                worker.StartAsync().Wait();
            }
            else
            {
                this.fixture     = fixture;
                this.client      = fixture.Client;
                this.proxyClient = new HttpClient() { BaseAddress = client.ProxyUri };
            }
        }

        public void Dispose()
        {
            if (proxyClient != null)
            {
                proxyClient.Dispose();
                proxyClient = null;
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Category, TestArea.NeonTemporal)]
        public void Prebuild_Stubs()
        {
            // Verify that the stub builder methods don't barf.

            TemporalClient.BuildActivityStub<ITestActivity>();
            TemporalClient.BuildWorkflowStub<ITestWorkflow>();
            TemporalClient.BuildAssemblyStubs(Assembly.GetExecutingAssembly());
        }
    }
}
