//-----------------------------------------------------------------------------
// FILE:        Test_StubManager.WorkflowGenerate.cs
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
using Neon.Cryptography;
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
    public partial class Test_StubManager
    {
        //---------------------------------------------------------------------

        public interface IActivityEntryVoidNoArgs : IActivityBase
        {
            [WorkflowMethod]
            Task RunAsync();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Generate_ActivityEntryVoidNoArgs()
        {
            var stub = StubManager.CreateActivityStub<IActivityEntryVoidNoArgs>(client, new DummyWorkflow());

            Assert.NotNull(stub);
        }

        //---------------------------------------------------------------------

        public interface IActivityEntryVoidWithArgs : IActivityBase
        {
            [WorkflowMethod]
            Task RunAsync(string arg1, int arg2);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Generate_ActivityEntryVoidWithArgs()
        {
            var stub = StubManager.CreateActivityStub<IActivityEntryVoidWithArgs>(client, new DummyWorkflow());

            Assert.NotNull(stub);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Generate_ActivityEntryVoidWithOptions()
        {
            var stub = StubManager.CreateActivityStub<IActivityEntryVoidWithArgs>(client, new DummyWorkflow(), options: new ActivityOptions(), domain: "my-domain");

            Assert.NotNull(stub);
        }

        //---------------------------------------------------------------------

        public interface IActivityEntryResultWithArgs : IActivityBase
        {
            [WorkflowMethod]
            Task<int> RunAsync(string arg1, int arg2);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Generate_ActivityResultWithArgs()
        {
            var stub = StubManager.CreateActivityStub<IActivityEntryResultWithArgs>(client, new DummyWorkflow());

            Assert.NotNull(stub);
        }

        //---------------------------------------------------------------------

        public interface IActivitySignalNoArgs : IActivityBase
        {
            [WorkflowMethod]
            Task RunAsync();

            [SignalMethod("my-signal")]
            Task SignalAsync();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Generate_ActivitySignalNoArgs()
        {
            var stub = StubManager.CreateActivityStub<IActivitySignalNoArgs>(client, new DummyWorkflow());

            Assert.NotNull(stub);
        }

        //---------------------------------------------------------------------

        public interface IActivitySignalWithArgs : IActivityBase
        {
            [WorkflowMethod]
            Task RunAsync();

            [QueryMethod("my-signal")]
            Task SignalAsync(string arg1, int arg2);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Generate_ActivitySignalWithArgs()
        {
            var stub = StubManager.CreateActivityStub<IActivitySignalWithArgs>(client, new DummyWorkflow());

            Assert.NotNull(stub);
        }

        //---------------------------------------------------------------------

        public interface IActivityQueryVoidNoArgs : IActivityBase
        {
            [WorkflowMethod]
            Task RunOneAsync();

            [QueryMethod("my-query")]
            Task QueryAsync();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Generate_ActivityQueryVoidNoArgs()
        {
            var stub = StubManager.CreateActivityStub<IActivityQueryVoidNoArgs>(client, new DummyWorkflow());

            Assert.NotNull(stub);
        }

        //---------------------------------------------------------------------

        public interface IActivityQueryVoidWithArgs : IActivityBase
        {
            [WorkflowMethod]
            Task RunOneAsync();

            [QueryMethod("my-query")]
            Task QueryAsync(string arg1, bool arg2);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Generate_ActivityQueryVoidWithArgs()
        {
            var stub = StubManager.CreateActivityStub<IActivityQueryVoidWithArgs>(client, new DummyWorkflow());

            Assert.NotNull(stub);
        }

        //---------------------------------------------------------------------

        public interface IActivityQueryResultWithArgs : IActivityBase
        {
            [WorkflowMethod]
            Task RunOneAsync();

            [QueryMethod("my-query")]
            Task<string> QueryAsync(string arg1, bool arg2);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Generate_ActivityQueryResultWithArgs()
        {
            var stub = StubManager.CreateActivityStub<IActivityQueryResultWithArgs>(client, new DummyWorkflow());

            Assert.NotNull(stub);
        }

        //---------------------------------------------------------------------

        public interface IActivityMultiMethods : IActivityBase
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

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Generate_ActivityMultiMethods()
        {
            var stub = StubManager.CreateActivityStub<IActivityMultiMethods>(client, new DummyWorkflow());

            Assert.NotNull(stub);
        }
    }
}
