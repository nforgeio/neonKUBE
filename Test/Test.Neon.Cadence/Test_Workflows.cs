//-----------------------------------------------------------------------------
// FILE:        Test_Workflows.cs
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

using Newtonsoft.Json;

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
    /// Tests basic workflow functionality.
    /// </summary>
    public class Test_Workflows : IClassFixture<CadenceFixture>, IDisposable
    {
        CadenceFixture  fixture;
        CadenceClient   client;
        HttpClient      proxyClient;

        public Test_Workflows(CadenceFixture fixture)
        {
            var settings = new CadenceSettings()
            {
                Mode  = ConnectionMode.ListenOnly,
                Debug = true,

                //--------------------------------
                // $debug(jeff.lill): DELETE THIS!
                Emulate                = false,
                DebugPrelaunched       = false,
                DebugDisableHandshakes = false,
                DebugDisableHeartbeats = false,
                //--------------------------------
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

        //---------------------------------------------------------------------
        // HelloWorld

        private class HelloWorkflow : Workflow
        {
            protected async override Task<byte[]> RunAsync(byte[] args)
            {
                return await Task.FromResult(Encoding.UTF8.GetBytes("Hello World!"));
            }
        }
        
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task HelloWorld()
        {
            Worker worker = null;

            try
            {
                await client.RegisterDomainAsync("test-domain");

                worker = await client.StartWorkflowWorkerAsync("test-domain");

                await client.RegisterWorkflow<HelloWorkflow>();

                var workflowRun = await client.StartWorkflowAsync<HelloWorkflow>("test-domain");
            }
            finally
            {
                if (worker != null)
                {
                    await client.StopWorkerAsync(worker);
                }
            }
        }
    }
}
