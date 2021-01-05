//-----------------------------------------------------------------------------
// FILE:        Test_EndToEnd.Error.cs
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
    public partial class Test_EndToEnd
    {
        //---------------------------------------------------------------------

        public interface IWorkflowNotRegistered
        {
            [WorkflowMethod]
            Task RunAsync();
        }

        /// <summary>
        /// Note that we're explicitly not registering this implementation 
        /// for this test.
        /// </summary>
        public class WorkflowWithNotRegistered: WorkflowBase, IWorkflowNotRegistered
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;
            }
        }

        //---------------------------------------------------------------------

        [ActivityInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IActivityNotRegistered : IActivity
        {
            [ActivityMethod]
            Task<string> HelloAsync(string name);
        }

        /// <summary>
        /// Note that we're explicitly not registering this implementation 
        /// for this test.
        /// </summary>
        public class ActivityNotRegistered : ActivityBase, IActivityNotRegistered
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }
        }

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IWorkflowCallsUnregisteredActivity : IWorkflow
        {
            [WorkflowMethod]
            Task<Exception> RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowCallsUnregisteredActivity : WorkflowBase, IWorkflowCallsUnregisteredActivity
        {
            public async Task<Exception> RunAsync()
            {
                var stub = Workflow.NewActivityStub<IActivityNotRegistered>();

                try
                {
                    await stub.HelloAsync("Jeff");

                    return await Task.FromResult<Exception>(null);
                }
                catch (Exception e)
                {
                    return await Task.FromResult(e);
                }
            }
        }
    }
}
