//-----------------------------------------------------------------------------
// FILE:        Test_StubManager.cs
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
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Data;
using Neon.IO;
using Neon.Temporal;
using Neon.Temporal.Internal;
using Neon.Xunit;
using Neon.Xunit.Temporal;

using Newtonsoft.Json;
using Xunit;

using Test.Neon.Models;
using Newtonsoft.Json.Linq;

namespace TestTemporal
{
    [Trait(TestTrait.Category, TestTrait.Incomplete)]
    [Trait(TestTrait.Category, TestArea.NeonTemporal)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    [Trait(TestTrait.Category, TestTrait.Investigate)]  // https://github.com/nforgeio/neonKUBE/issues/1200
    public partial class Test_StubManager : IClassFixture<TemporalFixture>, IDisposable
    {
        //---------------------------------------------------------------------
        // Local types

        /// <summary>
        /// Used when testing activity stub code generation.  This fakes up just
        /// enough of a workflow so that stubs can be generated.
        /// </summary>
        public class DummyWorkflow : WorkflowBase
        {
            public DummyWorkflow()
            {
                this.Workflow = new Workflow(
                    parent:             new WorkflowBase(),
                    worker:             new Worker(new TemporalClient(), 1, new WorkerOptions()),
                    contextId:          1,
                    workflowTypeName:   typeof(DummyWorkflow).FullName,
                    @namespace:         "my-namespace",
                    taskQueue:          "my-taskqueue",
                    workflowId:         "my-workflow-id",
                    runId:              "my-run-id",
                    isReplaying:        false,
                    methodMap:          null);
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private TemporalFixture     fixture;
        private TemporalClient      client;
        private HttpClient          proxyClient;

        public Test_StubManager(TemporalFixture fixture)
        {
            TestHelper.ResetDocker(this.GetType());

            var settings = new TemporalSettings()
            {
                Namespace      = TemporalFixture.Namespace,
                ProxyLogLevel  = TemporalTestHelper.ProxyLogLevel,
                Debug          = TemporalTestHelper.Debug,
                ClientIdentity = TemporalTestHelper.ClientIdentity
            };

            fixture.Start(settings, reconnect: true);

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
    }
}
