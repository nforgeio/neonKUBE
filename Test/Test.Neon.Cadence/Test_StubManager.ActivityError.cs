//-----------------------------------------------------------------------------
// FILE:        Test_StubManager.ActivityError.cs
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

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Data;
using Neon.IO;
using Neon.Tasks;
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

        public interface IErrorGenericActivity<T>
        {
            [ActivityMethod]
            Task DoIt();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_ActivityGenericsNotAllowed()
        {
            // We don't support activity interfaces with generic parameters.

            Assert.Throws<ActivityTypeException>(() => StubManager.NewActivityStub<IErrorGenericActivity<int>>(client, new DummyWorkflow().Workflow));
        }

        //---------------------------------------------------------------------

        public interface IErrorNoEntryPointActivity
        {
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_ActivityNoEntryPoint()
        {
            // Activities need to have at least one entry point.

            Assert.Throws<ActivityTypeException>(() => StubManager.NewActivityStub<IErrorNoEntryPointActivity>(client, new DummyWorkflow().Workflow));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_ActivityNullClient()
        {
            // A non-NULL client is required.

            Assert.Throws<ArgumentNullException>(() => StubManager.NewActivityStub<IErrorNoEntryPointActivity>(null, new DummyWorkflow().Workflow));
        }

        //---------------------------------------------------------------------

        public class IErrorNotInterfaceActivity
        {
            [ActivityMethod]
            public async Task EntryPoint()
            {
                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_ActivityNotInterface()
        {
            // Only activity interfaces are allowed.

            Assert.Throws<ActivityTypeException>(() => StubManager.NewActivityStub<IErrorNotInterfaceActivity>(client, new DummyWorkflow().Workflow));
        }

        //---------------------------------------------------------------------

        internal class IErrorNotPublicActivity
        {
            [ActivityMethod]
            public async Task EntryPoint()
            {
                await Task.CompletedTask;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_ActivityNotPublic()
        {
            // Activity interfaces must be public.

            Assert.Throws<ActivityTypeException>(() => StubManager.NewActivityStub<IErrorNotPublicActivity>(client, new DummyWorkflow().Workflow));
        }

        //---------------------------------------------------------------------

        public interface IErrorNonTaskEntryPoint1Activity
        {
            [ActivityMethod]
            void EntryPoint();
        }

        public interface IErrorNonTaskEntryPoint2Activity
        {
            [ActivityMethod]
            List<int> EntryPoint();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_ActivityNonTaskEntryPoint()
        {
            // Activity entry points methods need to return a Task.

            Assert.Throws<ActivityTypeException>(() => StubManager.NewActivityStub<IErrorNonTaskEntryPoint1Activity>(client, new DummyWorkflow().Workflow));
        }

        //---------------------------------------------------------------------

        public interface IDuplicateDefaultEntryPointsActivity
        {
            [ActivityMethod]
            Task EntryPoint1();

            [ActivityMethod]
            Task EntryPoint2();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_ActivityDuplicateDuplicateDefaultEntryPoints()
        {
            // Verify that we detect duplicate entrypoint methods
            // with the default name.

            Assert.Throws<ActivityTypeException>(() => StubManager.NewActivityStub<IDuplicateDefaultEntryPointsActivity>(client, new DummyWorkflow().Workflow));
        }

        //---------------------------------------------------------------------

        public interface IDuplicateEntryPointsActivity
        {
            [ActivityMethod(Name = "duplicate")]
            Task EntryPoint1();

            [ActivityMethod(Name = "duplicate")]
            Task EntryPoint2();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Error_ActivityDuplicateDuplicateEntryPoints()
        {
            // Verify that we detect duplicate entrypoint methods
            // with explicit names.

            Assert.Throws<ActivityTypeException>(() => StubManager.NewActivityStub<IDuplicateEntryPointsActivity>(client, new DummyWorkflow().Workflow));
        }
    }
}
