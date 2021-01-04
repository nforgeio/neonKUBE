//-----------------------------------------------------------------------------
// FILE:        Test_RegistrationError.Workflow.cs
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
    public partial class Test_RegistrationError
    {
        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowDuplicateBlankEntrypoint : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);

            [WorkflowMethod]
            Task<string> GoodbyeAsync(string name);
        }

        [Workflow(AutoRegister = false)]
        public class WorkflowDuplicateBlankEntrypoint : WorkflowBase, IWorkflowDuplicateBlankEntrypoint
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }

            public async Task<string> GoodbyeAsync(string name)
            {
                return await Task.FromResult($"Goodbye {name}!");
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_DuplicateBlankEntrypoint()
        {
            // Verify that the client detects workflows that have multiple
            // entrypoints that conflict because they have the same (blank)
            // name.

            await Assert.ThrowsAsync<WorkflowTypeException>(async() => await client.RegisterWorkflowAsync<WorkflowDuplicateBlankEntrypoint>());
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowDuplicateEntrypoint : IWorkflow
        {
            [WorkflowMethod(Name ="same")]
            Task<string> HelloAsync(string name);

            [WorkflowMethod(Name ="same")]
            Task<string> GoodbyeAsync(string name);
        }

        [Workflow(AutoRegister = false)]
        public class WorkflowDuplicateEntrypoint : WorkflowBase, IWorkflowDuplicateEntrypoint
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }

            public async Task<string> GoodbyeAsync(string name)
            {
                return await Task.FromResult($"Goodbye {name}!");
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_DuplicateEntrypoint()
        {
            // Verify that the client detects workflows that have multiple
            // entrypoints that conflict because they have the same name.

            await Assert.ThrowsAsync<WorkflowTypeException>(async () => await client.RegisterWorkflowAsync<WorkflowDuplicateEntrypoint>());
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowNoEntrypoint : IWorkflow
        {
        }

        [Workflow(AutoRegister = false)]
        public class WorkflowNoEntrypoint : WorkflowBase, IWorkflowNoEntrypoint
        {
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_NoEntrypoint()
        {
            // Verify that the client detects workflows that have no
            // entry point methods.

            await Assert.ThrowsAsync<WorkflowTypeException>(async () => await client.RegisterWorkflowAsync<WorkflowDuplicateEntrypoint>());
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowDuplicateSignal : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();

            [SignalMethod(name: "same")]
            Task Signal1();

            [SignalMethod(name: "same")]
            Task Signal2();
        }

        [Workflow(AutoRegister = false)]
        public class WorkflowDuplicateSignal : WorkflowBase, IWorkflowDuplicateSignal
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;
            }

            public async Task Signal1()
            {
                await Task.CompletedTask;
            }

            public async Task Signal2()
            {
                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_DuplicateSignal()
        {
            // Verify that the client detects workflows with two signals that
            // have the same signal name.

            await Assert.ThrowsAsync<WorkflowTypeException>(async () => await client.RegisterWorkflowAsync<WorkflowDuplicateSignal>());
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowDuplicateQuery : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();

            [QueryMethod(name: "same")]
            Task Query1();

            [QueryMethod(name: "same")]
            Task Query2();
        }

        [Workflow(AutoRegister = false)]
        public class WorkflowDuplicateQuery : WorkflowBase, IWorkflowDuplicateQuery
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;
            }

            public async Task Query1()
            {
                await Task.CompletedTask;
            }

            public async Task Query2()
            {
                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_DuplicateQuery()
        {
            // Verify that the client detects workflows with two signals that
            // have the same signal name.

            await Assert.ThrowsAsync<WorkflowTypeException>(async () => await client.RegisterWorkflowAsync<WorkflowDuplicateQuery>());
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowMultiInterface1 : IWorkflow
        {
            [WorkflowMethod]
            Task Run1Async();
        }

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowMultiInterface2 : IWorkflow
        {
            [WorkflowMethod]
            Task Run2Async();
        }

        [Workflow(AutoRegister = false)]
        public class WorkflowMultiInterface : WorkflowBase, IWorkflowMultiInterface1, IWorkflowMultiInterface2
        {
            public async Task Run1Async()
            {
                await Task.CompletedTask;
            }

            public async Task Run2Async()
            {
                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Workflow_MultipleInterfaces()
        {
            // Verify that the client detects workflow implementations
            // that implement more than one IWorkflow interface.

            await Assert.ThrowsAsync<WorkflowTypeException>(async () => await client.RegisterWorkflowAsync<WorkflowMultiInterface>());
        }
    }
}
