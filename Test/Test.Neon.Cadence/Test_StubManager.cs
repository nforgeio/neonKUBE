//-----------------------------------------------------------------------------
// FILE:        Test_StubManager.cs
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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Data;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Cadence;

using Newtonsoft.Json;
using Xunit;

using Test.Neon.Models;
using Newtonsoft.Json.Linq;

namespace TestCadence
{
    public partial class Test_StubManager : IClassFixture<CadenceFixture>, IDisposable
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
                    parent:     new WorkflowBase(),
                    client:             new CadenceClient(),
                    contextId:          1,
                    workflowTypeName:   typeof(DummyWorkflow).FullName,
                    domain:             "my-domain",
                    taskList:           "my-tasklist",
                    workflowId:         "my-workflow-id",
                    runId:              "my-run-id",
                    isReplaying:        false,
                    methodMap:          null);
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private CadenceFixture  fixture;
        private CadenceClient   client;
        private HttpClient      proxyClient;

        public Test_StubManager(CadenceFixture fixture)
        {
            var settings = new CadenceSettings()
            {
                DefaultDomain   = CadenceFixture.DefaultDomain,
                LogLevel        = CadenceTestHelper.LogLevel,
                Debug           = true,
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
    }
}
