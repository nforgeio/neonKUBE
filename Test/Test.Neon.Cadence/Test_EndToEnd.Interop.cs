//-----------------------------------------------------------------------------
// FILE:        Test_EndToEnd.Interop.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
    public partial class Test_EndToEnd
    {
        // IMPLEMENTATION NOTE:
        // --------------------
        //
        // These tests launch the [wf-args.exe] GOLANG test executable
        // which implements three workflows and activities that accept
        // varying numbers of arguments:
        // 
        //      NoArgsWorkflow  - (0 args)          returns "Hello there!"
        //      OneArgWorkflow  - (1 string arg)    returns "Hello " + arg + "!"
        //      TwoArgsWorkflow - (2 string args)   returns "Hello " + arg1 + " & " + arg2 + "!"
        // 
        //      NoArgsActivity  - (0 args)          returns "Hello there!"
        //      OneArgActivity  - (1 string arg)    returns "Hello " + arg + "!"
        //      TwoArgsActivity - (2 string args)   returns "Hello " + arg1 + " & " + arg2 + "!"
        // 
        // [wf-args.exe] is built by the solution and will be located at:
        //
        //      %NF_ROOT%\Build\go-test\wf-args.exe
        //
        // We need to verify that the .NET client can interop with a workflow
        // written in GOLANG and presumably with workflows written in other 
        // languages such as Java.
        //
        // This necessary because we originally coded argument
        // serialization in an incompatiable way as described here:
        //
        //      https://github.com/nforgeio/neonKUBE/issues/793


        /// <summary>
        /// Handles the launching and termination of the GOLANG [wf-args.exe]
        /// workflow/activity worker.
        /// </summary>
        private class WfArgsWorker : IDisposable
        {
            private Process workerProcess;

            /// <summary>
            /// Starts the worker application, configuring it to run for 5 seconds
            /// (by default) which is a bit fragile but should be long enough to 
            /// complete a typical test case.
            /// </summary>
            public WfArgsWorker(int runSeconds = 100000)
            {
                var workerExePath = Path.Combine(Environment.GetEnvironmentVariable("NF_BUILD"), "go-test", "wf-args.exe");

                workerProcess = Process.Start(workerExePath, $"-domain {CadenceFixture.DefaultDomain} -tasklist {CadenceTestHelper.TaskList} -wait {runSeconds}");

                // Give the worker a chance to register its workflow and activity implementations.

                Thread.Sleep(TimeSpan.FromSeconds(5));
            }

            /// <summary>
            /// Waits for the worker process to exit.
            /// </summary>
            public void Dispose()
            {
                if (workerProcess == null)
                {
                    throw new ObjectDisposedException(nameof(WfArgsWorker));
                }

                while (!workerProcess.HasExited)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(100));
                }

                workerProcess = null;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Interop_Workflow_Untyped()
        {
            await SyncContext.ClearAsync;

            // verify that we can execute a GOLANG workflows using 
            // untyped stubs.

            using (new WfArgsWorker())
            {
                //-----------------------------------------
                // Zero args:

                var options = new WorkflowOptions()
                {
                    WorkflowId = "no-args-" + Guid.NewGuid().ToString("d"),
                    TaskList   = CadenceTestHelper.TaskList
                };

                var stub      = client.NewUntypedWorkflowStub("NoArgsWorkflow", options);
                var execution = await stub.StartAsync();

                Assert.Equal("Hello there!", await stub.GetResultAsync<string>());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Interop_Activity_Untyped()
        {
            await SyncContext.ClearAsync;
        }

        //---------------------------------------------------------------------
        // Verify that Neon.Cadence v2+ clients can transparently support the
        // incorrect v1.x encoded arguments.
        //
        //      https://github.com/nforgeio/neonKUBE/issues/793

        [WorkflowInterface(TaskList = CadenceTestHelper.TaskList)]
        public interface IInteropArgsBackwardsCompat : IWorkflow
        {
            [WorkflowMethod(Name = "ZeroArgs")]
            Task<string> ZeroArgsAsync();

            [WorkflowMethod(Name = "OneArg")]
            Task<string> OneArgAsync(string arg);

            [WorkflowMethod(Name = "TwoArgs")]
            Task<string> TwoArgsAsync(string arg1, string arg2);
        }

        [Workflow(AutoRegister = true)]
        public class InteropArgsBackwardsCompat : WorkflowBase, IInteropArgsBackwardsCompat
        {
            public async Task<string> ZeroArgsAsync()
            {
                return await Task.FromResult("no-args");
            }

            public async Task<string> OneArgAsync(string arg)
            {
                return await Task.FromResult(arg);
            }

            public async Task<string> TwoArgsAsync(string arg1, string arg2)
            {
                return await Task.FromResult(arg1 + arg2);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Interop_ArgsBackwardsCompat()
        {
            await SyncContext.ClearAsync;

            // We're going to explicitly encode parameters using the incorrect 
            // Neon.Cadence v1.x style to verify that the v2+ code supports
            // it correctly for backwards compatability.

            var options = new WorkflowOptions()
            {
                TaskList = CadenceTestHelper.TaskList
            };

            //---------------------------------------------
            // Zero arguments:

            var stub      = client.NewUntypedWorkflowStub(CadenceHelper.GetWorkflowTypeName<IInteropArgsBackwardsCompat>("ZeroArgs"), options);
            var argBytes  = Encoding.UTF8.GetBytes("[]");    // v1.x encoded zero args as an empty array whereas v2+ encodes this as NULL.
            var execution = await stub.StartAsync(argBytes);

            Assert.Equal("no-args", await stub.GetResultAsync<string>());

            //---------------------------------------------
            // One argument:

            stub      = client.NewUntypedWorkflowStub(CadenceHelper.GetWorkflowTypeName<IInteropArgsBackwardsCompat>("OneArg"), options);
            argBytes  = Encoding.UTF8.GetBytes("[\"one-arg\"]");    // v1.x encoded one arg as an array with yhe the are whereas v2+ encodes this as just the arg value.
            execution = await stub.StartAsync(argBytes);

            Assert.Equal("one-arg", await stub.GetResultAsync<string>());

            //---------------------------------------------
            // Two arguments:

            stub      = client.NewUntypedWorkflowStub(CadenceHelper.GetWorkflowTypeName<IInteropArgsBackwardsCompat>("TwoArgs"), options);
            argBytes  = Encoding.UTF8.GetBytes("[\"arg-one:\",\"arg-two\"]");    // v1.x and v2+ both encode this as an array with the two args.
            execution = await stub.StartAsync(argBytes);

            Assert.Equal("arg-one:arg-two", await stub.GetResultAsync<string>());
        }
    }
}
