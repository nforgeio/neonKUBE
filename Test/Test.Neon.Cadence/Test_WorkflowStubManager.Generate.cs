//-----------------------------------------------------------------------------
// FILE:        Test_WorkflowStubManager.Generate.cs
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
    public partial class Test_WorkflowStubManager
    {
        //---------------------------------------------------------------------

        public interface IWorkflowVoidNoArgs : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Generate_WorkflowVoidNoArgs()
        {
            var stub = WorkflowStubManager.Create<IWorkflowVoidNoArgs>(client);

            Assert.NotNull(stub);
        }

        //---------------------------------------------------------------------

        public interface IWorkflowVoidWithArgs : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync(string arg1, int arg2);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Generate_WorkflowVoidWithArgs()
        {
            var stub = WorkflowStubManager.Create<IWorkflowVoidWithArgs>(client);

            Assert.NotNull(stub);
        }

        //---------------------------------------------------------------------

        public interface IWorkflowResultWithArgs : IWorkflow
        {
            [WorkflowMethod]
            Task<int> RunAsync(string arg1, int arg2);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Generate_WorkflowResultWithArgs()
        {
            var stub = WorkflowStubManager.Create<IWorkflowResultWithArgs>(client);

            Assert.NotNull(stub);
        }

        //---------------------------------------------------------------------

        public interface WorkflowMultipleEntrypoint : IWorkflow
        {
            [WorkflowMethod]
            Task<int> RunOneAsync(string arg1, int arg2);

            [WorkflowMethod]
            Task<string> RunTwoAsync(Person arg1, List<string> arg2);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Generate_WorkflowMultipleEntrypoint()
        {
            var stub = WorkflowStubManager.Create<WorkflowMultipleEntrypoint>(client);

            Assert.NotNull(stub);
        }
    }
}
