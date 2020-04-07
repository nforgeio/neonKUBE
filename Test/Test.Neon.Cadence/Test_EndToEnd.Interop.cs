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
        }
    }
}
