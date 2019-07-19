//-----------------------------------------------------------------------------
// FILE:        Test_WorkflowStubGen.cs
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
    /// <summary>
    /// Tests dynamically generated workflow stub generation.
    /// </summary>
    public partial class Test_WorkflowStubGen : IClassFixture<CadenceFixture>, IDisposable
    {
        //---------------------------------------------------------------------

        public interface ErrorGenericWorkflow<T> : IWorkflow
        {
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_GenericsNotAllowed()
        {
            // We don't support workflow interfaces with generic parameters.

            Assert.Throws<WorkflowDefinitionException>(() => WorkflowStubManager.Create<ErrorGenericWorkflow<int>>(client));
        }

        //---------------------------------------------------------------------

        public interface ErrorNoEntryPointWorkflow : IWorkflow
        {
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_NoEntryPoint()
        {
            // Workflows need to have at least one entry point.

            Assert.Throws<WorkflowDefinitionException>(() => WorkflowStubManager.Create<ErrorNoEntryPointWorkflow>(client));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_NullClient()
        {
            // A non-NULL client is required.

            Assert.Throws<ArgumentNullException>(() => WorkflowStubManager.Create<ErrorNoEntryPointWorkflow>(null));
        }

        //---------------------------------------------------------------------

        public class ErrorNotInterface : Workflow
        {
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_NotInterface()
        {
            // Only workflow interfaces are allowed.

            Assert.Throws<WorkflowDefinitionException>(() => WorkflowStubManager.Create<ErrorNotInterface>(client));
        }

        //---------------------------------------------------------------------

        public interface ErrorNonTaskEntryPoint1 : IWorkflow
        {
            [WorkflowMethod]
            void EntryPoint();
        }

        public interface ErrorNonTaskEntryPoint2 : IWorkflow
        {
            [WorkflowMethod]
            List<int> EntryPoint();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_NonTaskEntryPoint()
        {
            // Workflow entry points methods need to return a Task.

            Assert.Throws<WorkflowDefinitionException>(() => WorkflowStubManager.Create<ErrorNonTaskEntryPoint1>(client));
            Assert.Throws<WorkflowDefinitionException>(() => WorkflowStubManager.Create<ErrorNonTaskEntryPoint2>(client));
        }

        //---------------------------------------------------------------------

        public interface ErrorNonTaskSignal : IWorkflow
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

            Assert.Throws<WorkflowDefinitionException>(() => WorkflowStubManager.Create<ErrorNonTaskSignal>(client));
        }

        //---------------------------------------------------------------------

        public interface ErrorDuplicateSignals : IWorkflow
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

            Assert.Throws<WorkflowDefinitionException>(() => WorkflowStubManager.Create<ErrorDuplicateSignals>(client));
        }

        //---------------------------------------------------------------------

        public interface ErrorNonTaskQuery : IWorkflow
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

            Assert.Throws<WorkflowDefinitionException>(() => WorkflowStubManager.Create<ErrorNonTaskQuery>(client));
        }

        //---------------------------------------------------------------------

        public interface ErrorDuplicateQueries : IWorkflow
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

            Assert.Throws<WorkflowDefinitionException>(() => WorkflowStubManager.Create<ErrorDuplicateQueries>(client));
        }
    }
}
