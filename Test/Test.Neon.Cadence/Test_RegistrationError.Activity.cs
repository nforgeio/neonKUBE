//-----------------------------------------------------------------------------
// FILE:        Test_RegistrationError.Activity.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using Neon.Tasks;
using Neon.Xunit;
using Neon.Xunit.Cadence;

using Newtonsoft.Json;
using Xunit;

namespace TestCadence
{
    public partial class Test_RegistrationError
    {
        //---------------------------------------------------------------------

        [ActivityInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IActivityDuplicateBlankEntrypoint : IActivity
        {
            [ActivityMethod]
            Task<string> HelloAsync(string name);

            [ActivityMethod]
            Task<string> GoodbyeAsync(string name);
        }

        [Activity(AutoRegister = false)]
        public class ActivityDuplicateBlankEntrypoint : ActivityBase, IActivityDuplicateBlankEntrypoint
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
        public async Task Activity_DuplicateBlankEntrypoint()
        {
            // Verify that the client detects workflows that have multiple
            // entrypoints that conflict because they have the same (blank)
            // name.

            await Assert.ThrowsAsync<ActivityTypeException>(async () => await client.RegisterActivityAsync<ActivityDuplicateBlankEntrypoint>());
        }

        //---------------------------------------------------------------------

        [ActivityInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IActivityDuplicateEntrypoint : IActivity
        {
            [ActivityMethod(Name = "same")]
            Task<string> HelloAsync(string name);

            [ActivityMethod(Name = "same")]
            Task<string> GoodbyeAsync(string name);
        }

        [Activity(AutoRegister = false)]
        public class ActivityDuplicateEntrypoint : ActivityBase, IActivityDuplicateEntrypoint
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
        public async Task Activity_DuplicateEntrypoint()
        {
            // Verify that the client detects workflows that have multiple
            // entrypoints that conflict because they have the same name.

            await Assert.ThrowsAsync<ActivityTypeException>(async () => await client.RegisterActivityAsync<ActivityDuplicateEntrypoint>());
        }

        //---------------------------------------------------------------------

        [ActivityInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IActivityNoEntrypoint : IActivity
        {
        }

        [Activity(AutoRegister = false)]
        public class ActivityNoEntrypoint : ActivityBase, IActivityNoEntrypoint
        {
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_NoEntrypoint()
        {
            // Verify that the client detects activities that don't
            // an entry point method.

            await Assert.ThrowsAsync<ActivityTypeException>(async () => await client.RegisterActivityAsync<ActivityNoEntrypoint>());
        }

        //---------------------------------------------------------------------

        [ActivityInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IActivityMultiInterface1 : IActivity
        {
            [ActivityMethod]
            Task Run1Async();
        }

        [ActivityInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IActivityMultiInterface2 : IActivity
        {
            [ActivityMethod]
            Task Run2Async();
        }

        [Activity(AutoRegister = false)]
        public class ActivityMultiInterface : ActivityBase, IActivityMultiInterface1, IActivityMultiInterface2
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
        public async Task Activity_MultipleInterfaces()
        {
            // Verify that the client detects activity implementations
            // that implement more than one IActivity interface.

            await Assert.ThrowsAsync<ActivityTypeException>(async () => await client.RegisterActivityAsync<ActivityMultiInterface>());
        }
    }
}
