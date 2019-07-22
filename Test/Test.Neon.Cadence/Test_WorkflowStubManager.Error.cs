//-----------------------------------------------------------------------------
// FILE:        Test_WorkflowStubManager.Error.cs
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

        public interface IErrorGenericWorkflow<T> : IWorkflow
        {
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_GenericsNotAllowed()
        {
            // We don't support workflow interfaces with generic parameters.

            Assert.Throws<WorkflowDefinitionException>(() => WorkflowStubManager.Create<IErrorGenericWorkflow<int>>(client));
        }

        //---------------------------------------------------------------------

        public interface IErrorNoEntryPointWorkflow : IWorkflow
        {
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_NoEntryPoint()
        {
            // Workflows need to have at least one entry point.

            Assert.Throws<WorkflowDefinitionException>(() => WorkflowStubManager.Create<IErrorNoEntryPointWorkflow>(client));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_NullClient()
        {
            // A non-NULL client is required.

            Assert.Throws<ArgumentNullException>(() => WorkflowStubManager.Create<IErrorNoEntryPointWorkflow>(null));
        }

        //---------------------------------------------------------------------

        public class ErrorNotInterface : Workflow
        {
            [WorkflowMethod]
            public async Task EntryPoint()
            {
                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_NotInterface()
        {
            // Only workflow interfaces are allowed.

            Assert.Throws<WorkflowDefinitionException>(() => WorkflowStubManager.Create<ErrorNotInterface>(client));
        }

        //---------------------------------------------------------------------

        internal class ErrorNotPublic : Workflow
        {
            [WorkflowMethod]
            public async Task EntryPoint()
            {
                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_NotPublic()
        {
            // Workflow interfaces must be public.

            Assert.Throws<WorkflowDefinitionException>(() => WorkflowStubManager.Create<ErrorNotPublic>(client));
        }

        //---------------------------------------------------------------------

        public interface IErrorNonTaskEntryPoint1 : IWorkflow
        {
            [WorkflowMethod]
            void EntryPoint();
        }

        public interface IErrorNonTaskEntryPoint2 : IWorkflow
        {
            [WorkflowMethod]
            List<int> EntryPoint();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_NonTaskEntryPoint()
        {
            // Workflow entry points methods need to return a Task.

            Assert.Throws<WorkflowDefinitionException>(() => WorkflowStubManager.Create<IErrorNonTaskEntryPoint1>(client));
            Assert.Throws<WorkflowDefinitionException>(() => WorkflowStubManager.Create<IErrorNonTaskEntryPoint2>(client));
        }

        //---------------------------------------------------------------------

        public interface IErrorNonTaskSignal : IWorkflow
        {
            [WorkflowMethod]
            Task EntryPoint();

            [SignalMethod("my-signal")]
            void Signal();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_NonTaskSignal()
        {
            // Workflow signal methods need to return a Task.

            Assert.Throws<WorkflowDefinitionException>(() => WorkflowStubManager.Create<IErrorNonTaskSignal>(client));
        }

        //---------------------------------------------------------------------

        public interface IErrorDuplicateSignals : IWorkflow
        {
            [WorkflowMethod]
            Task EntryPoint();

            [SignalMethod("my-signal")]
            Task Signal1();

            [SignalMethod("my-signal")]
            Task Signal2();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_DuplicateSignals()
        {
            // Verify that we detect duplicate signal names.

            Assert.Throws<WorkflowDefinitionException>(() => WorkflowStubManager.Create<IErrorDuplicateSignals>(client));
        }

        //---------------------------------------------------------------------

        public interface IErrorNonTaskQuery : IWorkflow
        {
            [WorkflowMethod]
            Task EntryPoint();

            [QueryMethod("my-query")]
            void Query();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_NonTaskQuery()
        {
            // Workflow query methods need to return a Task.

            Assert.Throws<WorkflowDefinitionException>(() => WorkflowStubManager.Create<IErrorNonTaskQuery>(client));
        }

        //---------------------------------------------------------------------

        public interface IErrorDuplicateQueries : IWorkflow
        {
            [WorkflowMethod]
            Task EntryPoint();

            [QueryMethod("my-query")]
            Task Query1();

            [QueryMethod("my-query")]
            Task Query2();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_DuplicateQueries()
        {
            // Verify that we detect duplicate query names.

            Assert.Throws<WorkflowDefinitionException>(() => WorkflowStubManager.Create<IErrorDuplicateQueries>(client));
        }
    }
}
