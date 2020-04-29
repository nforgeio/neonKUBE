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
        // These tests launch the [cwf-args.exe] GOLANG test executable
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
        // [cwf-args.exe] is built by the solution and will be located at:
        //
        //      %NF_ROOT%\Build\go-test\cadence\cwf-args.exe
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
        /// Handles the launching and termination of the GOLANG [cwf-args.exe]
        /// workflow/activity worker.
        /// </summary>
        private class CwfArgsWorker : IDisposable
        {
            private Task<ExecuteResponse>   workerTask;
            private string                  readyFile;
            private string                  stopFile;

            /// <summary>
            /// Starts the worker application.
            /// </summary>
            public CwfArgsWorker()
            {
                var goTestDir     = Path.Combine(Environment.GetEnvironmentVariable("NF_BUILD"), "go-test", "cadence");
                var workerExePath = Path.Combine(goTestDir, "cwf-args.exe");

                // The worker app polls for the existance of a temporary stop file and
                // exits when one is created.

                var fileId   = Guid.NewGuid().ToString("d");
                var tempPath = Path.GetTempPath();

                tempPath = "C:\\temp";  // debug(jefflil): DELETE THIS!

                readyFile = Path.Combine(tempPath, fileId + ".ready");
                stopFile  = Path.Combine(tempPath, fileId + ".stop");

                workerTask = NeonHelper.ExecuteCaptureAsync(workerExePath,
                    new object[]
                    {
                        $"-config={Path.Combine(goTestDir, "config.yaml")}",
                        $"-domain={CadenceFixture.DefaultDomain}", 
                        $"-tasklist={CadenceTestHelper.TaskList_CwfArgs}", 
                        $"-readyfile={readyFile}",
                        $"-stopfile={stopFile}"
                    });

                // Wait for the worker app to signal that it's ready.

                NeonHelper.WaitFor(() => File.Exists(readyFile), TimeSpan.FromSeconds(60));
            }

            /// <summary>
            /// Signals the worker application to stop and then waits for the
            /// process to exit.
            /// </summary>
            public void Dispose()
            {
                if (workerTask == null)
                {
                    throw new ObjectDisposedException(nameof(CwfArgsWorker));
                }

                File.WriteAllText(stopFile, "STOP");

                var result = workerTask.Result;

                 //$debug(jefflill): DELETE THIS!
                 File.WriteAllText(@"C:\Temp\test.log", result.AllText);

                workerTask = null;

                File.Delete(readyFile);
                File.Delete(stopFile);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Interop_Workflow_Untyped()
        {
            await SyncContext.ClearAsync;

            // verify that we can execute a GOLANG workflows using 
            // untyped stubs.

            using (new CwfArgsWorker())
            {
                //-----------------------------------------
                // Zero args:

                var options = new WorkflowOptions()
                {
                    WorkflowId = "NoArgs-" + Guid.NewGuid().ToString("d"),
                    TaskList = CadenceTestHelper.TaskList_CwfArgs
                };

                var stub      = client.NewUntypedWorkflowStub("main.NoArgsWorkflow", options);
                var execution = await stub.StartAsync();

                Assert.Equal("Hello there!", await stub.GetResultAsync<string>());

                //-----------------------------------------
                // One arg:

                options = new WorkflowOptions()
                {
                    WorkflowId = "OneArg-" + Guid.NewGuid().ToString("d"),
                    TaskList   = CadenceTestHelper.TaskList_CwfArgs
                };

                stub      = client.NewUntypedWorkflowStub("main.OneArgWorkflow", options);
                execution = await stub.StartAsync("JACK");

                Assert.Equal("Hello JACK!", await stub.GetResultAsync<string>());

                //-----------------------------------------
                // Two Args:

                options = new WorkflowOptions()
                {
                    WorkflowId = "TwoArgs-" + Guid.NewGuid().ToString("d"),
                    TaskList   = CadenceTestHelper.TaskList_CwfArgs
                };

                stub      = client.NewUntypedWorkflowStub("main.TwoArgsWorkflow", options);
                execution = await stub.StartAsync("JACK", "JILL");

                Assert.Equal("Hello JACK & JILL!", await stub.GetResultAsync<string>());

                //-----------------------------------------
                // One Array Arg:

                options = new WorkflowOptions()
                {
                    WorkflowId = "OneArrayArg-" + Guid.NewGuid().ToString("d"),
                    TaskList   = CadenceTestHelper.TaskList_CwfArgs
                };

                stub      = client.NewUntypedWorkflowStub("main.ArrayArgWorkflow", options);
                execution = await stub.StartAsync(new int[] { 0, 1, 2, 3, 4 });

                var arrayResult = await stub.GetResultAsync<int[]>();

                Assert.Equal(new int[] { 0, 1, 2, 3, 4 }, arrayResult);

                //-----------------------------------------
                // One Array and a String Arg 

                options = new WorkflowOptions()
                {
                    WorkflowId = "OneArrayArgs-" + Guid.NewGuid().ToString("d"),
                    TaskList = CadenceTestHelper.TaskList_CwfArgs
                };

                stub      = client.NewUntypedWorkflowStub("main.ArrayArgsWorkflow", options);
                execution = await stub.StartAsync(new int[] { 0, 1, 2, 3, 4 }, "test");

                arrayResult = await stub.GetResultAsync<int[]>();

                Assert.Equal(new int[] { 0, 1, 2, 3, 4 }, arrayResult);

                //-----------------------------------------
                // Workflow that returns just an error:

                // Verify that things work when the workflow DOESN'T return an error.

                options = new WorkflowOptions()
                {
                    WorkflowId = "ErrorWorkflow-NOERROR-" + Guid.NewGuid().ToString("d"),
                    TaskList   = CadenceTestHelper.TaskList_CwfArgs
                };

                stub      = client.NewUntypedWorkflowStub("main.ErrorWorkflow", options);
                execution = await stub.StartAsync("");

                await stub.GetResultAsync();

                // Verify that things work when the workflow DOES return an error.

                options = new WorkflowOptions()
                {
                    WorkflowId = "ErrorWorkflow-ERROR-" + Guid.NewGuid().ToString("d"),
                    TaskList   = CadenceTestHelper.TaskList_CwfArgs
                };

                stub      = client.NewUntypedWorkflowStub("main.ErrorWorkflow", options);
                execution = await stub.StartAsync("error message");

                try
                {
                    await stub.GetResultAsync();
                }
                catch (Exception e)
                {
                    Assert.Equal("error message", e.Message);
                }

                //-----------------------------------------
                // Workflow that returns a string or an error:

                // Verify that things work when the workflow DOESN'T return an error.

                options = new WorkflowOptions()
                {
                    WorkflowId = "StringErrorWorkflow-NOERROR-" + Guid.NewGuid().ToString("d"),
                    TaskList   = CadenceTestHelper.TaskList_CwfArgs
                };

                stub      = client.NewUntypedWorkflowStub("main.StringErrorWorkflow", options);
                execution = await stub.StartAsync("JEFF", "");

                Assert.Equal("Hello JEFF!", await stub.GetResultAsync<string>());

                // Verify that things work when the workflow DOES return an error.

                options = new WorkflowOptions()
                {
                    WorkflowId = "StringErrorWorkflow-ERROR-" + Guid.NewGuid().ToString("d"),
                    TaskList   = CadenceTestHelper.TaskList_CwfArgs
                };

                stub      = client.NewUntypedWorkflowStub("main.ErrorWorkflow", options);
                execution = await stub.StartAsync("", "error message");

                try
                {
                    await stub.GetResultAsync<string>();
                }
                catch (Exception e)
                {
                    Assert.Equal("error message", e.Message);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Interop_Activity_Untyped()
        {
            await SyncContext.ClearAsync;

            throw new NotImplementedException();
        }
    }
}
