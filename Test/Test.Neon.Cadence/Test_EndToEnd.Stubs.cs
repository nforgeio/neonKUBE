//-----------------------------------------------------------------------------
// FILE:        Test_EndToEnd.Stubs.cs
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
using System.Diagnostics.Contracts;
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
using Neon.Cryptography;
using Neon.Data;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Cadence;

using Newtonsoft.Json;
using Xunit;

namespace TestCadence
{
    public partial class Test_EndToEnd
    {
        //---------------------------------------------------------------------

        public interface IWorkflowStubUntyped_Execute : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);
        }

        [Workflow(AutoRegister = true, Name = nameof(WorkflowStubUntyped_Execute))]
        public class WorkflowStubUntyped_Execute : WorkflowBase, IWorkflowStubUntyped_Execute
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task WorkflowStub_Untyped_Execute()
        {
            // Use an untyped workflow stub to execute a workflow.

            var stub      = client.NewUntypedWorkflowStub(nameof(WorkflowStubUntyped_Execute));
            var execution = await stub.StartAsync("Jeff");

            Assert.NotNull(execution);
            Assert.Equal("Hello Jeff!", await stub.GetResultAsync<string>());

            // Verify that we're not allowed to reuse the stub.

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await stub.StartAsync("Jeff"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task WorkflowStub_Untyped_Attach()
        {
            // Use an untyped workflow stub to execute a workflow.

            var stub      = client.NewUntypedWorkflowStub(nameof(WorkflowStubUntyped_Execute));
            var execution = await stub.StartAsync("Jeff");

            Assert.NotNull(execution);
            Assert.Equal("Hello Jeff!", await stub.GetResultAsync<string>());

            // Now connect another stub to the workflow and verify that we
            // can use it to obtain the result.

            stub = client.NewUntypedWorkflowStub(execution.WorkflowId, execution.RunId, nameof(WorkflowStubUntyped_Execute));

            Assert.Equal("Hello Jeff!", await stub.GetResultAsync<string>());

            // There's one more method override for attaching to an existing workflow.

            stub = client.NewUntypedWorkflowStub(execution, nameof(WorkflowStubUntyped_Execute));

            Assert.Equal("Hello Jeff!", await stub.GetResultAsync<string>());
        }
    }
}
