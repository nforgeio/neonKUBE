//-----------------------------------------------------------------------------
// FILE:        Test_StubManager.WorkflowGenerate.cs
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
    public partial class Test_StubManager
    {
        //---------------------------------------------------------------------

        [WorkflowInterface(TaskQueue = TemporalTestHelper.TaskQueue)]
        public interface IWorkflowEntryVoidNoArgs : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Project, TestProject.NeonTemporal)]
        public void Generate_WorkflowEntryVoidNoArgs()
        {
            var stub = StubManager.NewWorkflowStub<IWorkflowEntryVoidNoArgs>(client);

            Assert.NotNull(stub);
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskQueue = TemporalTestHelper.TaskQueue)]
        public interface IWorkflowEntryVoidWithArgs : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync(string arg1, int arg2);
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Project, TestProject.NeonTemporal)]
        public void Generate_WorkflowEntryVoidWithArgs()
        {
            var stub = StubManager.NewWorkflowStub<IWorkflowEntryVoidWithArgs>(client);

            Assert.NotNull(stub);
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskQueue = TemporalTestHelper.TaskQueue)]
        public interface IWorkflowEntryResultWithArgs : IWorkflow
        {
            [WorkflowMethod]
            Task<int> RunAsync(string arg1, int arg2);
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Project, TestProject.NeonTemporal)]
        public void Generate_WorkflowResultWithArgs()
        {
            var stub = StubManager.NewWorkflowStub<IWorkflowEntryResultWithArgs>(client);

            Assert.NotNull(stub);
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Project, TestProject.NeonTemporal)]
        public void Generate_WorkflowResultWithOptions()
        {
            var stub = StubManager.NewWorkflowStub<IWorkflowEntryResultWithArgs>(client, options: new StartWorkflowOptions() { TaskQueue = "my-taskqueue", Namespace = "my-namespace" });

            Assert.NotNull(stub);
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskQueue = TemporalTestHelper.TaskQueue)]
        public interface IWorkflowSignalNoArgs : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();

            [SignalMethod("my-signal")]
            Task SignalAsync();
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Project, TestProject.NeonTemporal)]
        public void Generate_WorkflowSignalNoArgs()
        {
            var stub = StubManager.NewWorkflowStub<IWorkflowSignalNoArgs>(client);

            Assert.NotNull(stub);
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskQueue = TemporalTestHelper.TaskQueue)]
        public interface IWorkflowSignalWithArgs : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();

            [QueryMethod("my-signal")]
            Task SignalAsync(string arg1, int arg2);
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Project, TestProject.NeonTemporal)]
        public void Generate_WorkflowSignalWithArgs()
        {
            var stub = StubManager.NewWorkflowStub<IWorkflowSignalWithArgs>(client);

            Assert.NotNull(stub);
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskQueue = TemporalTestHelper.TaskQueue)]
        public interface IWorkflowQueryVoidNoArgs : IWorkflow
        {
            [WorkflowMethod]
            Task RunOneAsync();

            [QueryMethod("my-query")]
            Task QueryAsync();
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Project, TestProject.NeonTemporal)]
        public void Generate_WorkflowQueryVoidNoArgs()
        {
            var stub = StubManager.NewWorkflowStub<IWorkflowQueryVoidNoArgs>(client);

            Assert.NotNull(stub);
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskQueue = TemporalTestHelper.TaskQueue)]
        public interface IWorkflowQueryVoidWithArgs : IWorkflow
        {
            [WorkflowMethod]
            Task RunOneAsync();

            [QueryMethod("my-query")]
            Task QueryAsync(string arg1, bool arg2);
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Project, TestProject.NeonTemporal)]
        public void Generate_WorkflowQueryVoidWithArgs()
        {
            var stub = StubManager.NewWorkflowStub<IWorkflowQueryVoidWithArgs>(client);

            Assert.NotNull(stub);
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskQueue = TemporalTestHelper.TaskQueue)]
        public interface IWorkflowQueryResultWithArgs : IWorkflow
        {
            [WorkflowMethod]
            Task RunOneAsync();

            [QueryMethod("my-query")]
            Task<string> QueryAsync(string arg1, bool arg2);
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Project, TestProject.NeonTemporal)]
        public void Generate_WorkflowQueryResultWithArgs()
        {
            var stub = StubManager.NewWorkflowStub<IWorkflowQueryResultWithArgs>(client);

            Assert.NotNull(stub);
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskQueue = TemporalTestHelper.TaskQueue)]
        public interface IWorkflowMultiMethods : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();

            [WorkflowMethod(Name = "one")]
            Task<int> RunAsync(string arg1);

            [WorkflowMethod(Name = "two")]
            Task<int> RunAsync(string arg1, string arg2);

            [QueryMethod("my-query1")]
            Task<string> QueryAsync();

            [QueryMethod("my-query2")]
            Task<string> QueryAsync(string arg1);

            [QueryMethod("my-query3")]
            Task<string> QueryAsync(string arg1, string arg2);

            [QueryMethod("my-signal1")]
            Task<string> SignalAsync();

            [QueryMethod("my-signal2")]
            Task<string> SignalAsync(string arg1);

            [QueryMethod("my-signal3")]
            Task<string> SignalAsync(string arg1, string arg2);
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Project, TestProject.NeonTemporal)]
        public void Generate_WorkflowMultiMethods()
        {
            var stub = StubManager.NewWorkflowStub<IWorkflowMultiMethods>(client);

            Assert.NotNull(stub);
        }
    }
}
