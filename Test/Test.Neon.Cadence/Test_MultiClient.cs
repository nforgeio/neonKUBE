//-----------------------------------------------------------------------------
// FILE:        Test_MultiClient.cs
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
using System.Reflection;
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

namespace TestCadence
{
    public class Test_MultiClient : IClassFixture<CadenceFixture>, IDisposable
    {
        private CadenceFixture  fixture;

        public Test_MultiClient(CadenceFixture fixture)
        {
            var settings = new CadenceSettings()
            {
                DefaultDomain    = CadenceFixture.DefaultDomain,
                DefaultTaskList  = CadenceFixture.DefaultTaskList,
                LogLevel         = CadenceTestHelper.LogLevel,
                CreateDomain     = true,
                Debug            = true,
                DebugPrelaunched = CadenceTestHelper.DebugPrelaunched
            };

            this.fixture = fixture;

            fixture.Start(settings, keepConnection: true, keepOpen: CadenceTestHelper.KeepCadenceServerOpen, noClient: true);
        }

        public void Dispose()
        {
        }

        //---------------------------------------------------------------------

        public interface IWorkflowWithResult1: IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);
        }

        public class WorkflowWithResult1 : WorkflowBase, IWorkflowWithResult1
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"WF1 says: Hello {name}!");
            }
        }

        public interface IWorkflowWithResult2 : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);
        }

        public class WorkflowWithResult2 : WorkflowBase, IWorkflowWithResult2
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"WF2 says: Hello {name}!");
            }
        }

        public interface IWorkflowWithResult3 : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);
        }

        public class WorkflowWithResult3 : WorkflowBase, IWorkflowWithResult3
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"WF3 says: Hello {name}!");
            }
        }

        public interface IWorkflowWithResult4 : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);
        }

        public class WorkflowWithResult4 : WorkflowBase, IWorkflowWithResult4
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"WF4 says: Hello {name}!");
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Simultaneous()
        {
            // We're going to establish two simultaneous client connections, 
            // register a workflow on each, and then verify that these workflows work.

            using (var client1 = await CadenceClient.ConnectAsync(fixture.Settings))
            {
                await client1.RegisterWorkflowAsync<WorkflowWithResult1>();
                await client1.StartWorkerAsync();

                using (var client2 = await CadenceClient.ConnectAsync(fixture.Settings))
                {
                    await client2.RegisterWorkflowAsync<WorkflowWithResult2>();
                    await client2.StartWorkerAsync();

                    var stub1 = client1.NewWorkflowStub<IWorkflowWithResult1>();
                    var stub2 = client2.NewWorkflowStub<IWorkflowWithResult2>();

                    Assert.Equal("WF1 says: Hello Jeff!", await stub1.HelloAsync("Jeff"));
                    Assert.Equal("WF2 says: Hello Jeff!", await stub2.HelloAsync("Jeff"));
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Connect_Twice()
        {
            // We're going to establish two successive client connections
            // and verify that these work.

            using (var client = await CadenceClient.ConnectAsync(fixture.Settings))
            {
                await client.RegisterWorkflowAsync<WorkflowWithResult3>();
                
                using (await client.StartWorkerAsync())
                {
                    var stub1 = client.NewWorkflowStub<IWorkflowWithResult3>();

                    Assert.Equal("WF3 says: Hello Jack!", await stub1.HelloAsync("Jack"));
                }
            }

            using (var client = await CadenceClient.ConnectAsync(fixture.Settings))
            {
                await client.RegisterWorkflowAsync<WorkflowWithResult4>();

                using (await client.StartWorkerAsync())
                {
                    var stub1 = client.NewWorkflowStub<IWorkflowWithResult4>();

                    Assert.Equal("WF4 says: Hello Jack!", await stub1.HelloAsync("Jack"));
                }
            }
        }
    }
}
