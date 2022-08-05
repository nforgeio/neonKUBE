//-----------------------------------------------------------------------------
// FILE:        Test_EndToEnd.Interop.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

using Neon.Common;
using Neon.Data;
using Neon.IO;
using Neon.Tasks;
using Neon.Temporal;
using Neon.Temporal.Internal;
using Neon.Xunit;
using Neon.Xunit.Temporal;

using Newtonsoft.Json;
using Xunit;

namespace TestTemporal
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
        //      %NK_ROOT%\Build\go-test\temporal\twf-args.exe
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
        private class TwfArgsWorker : IDisposable
        {
            private Task<ExecuteResponse>   workerTask;
            private string                  readyFile;
            private string                  stopFile;

            /// <summary>
            /// Starts the worker application.
            /// </summary>
            public TwfArgsWorker()
            {
                // Kill any running [twf-args.exe] processes.

                foreach (var twfProcess in Process.GetProcesses()
                    .Where(p => p.ProcessName.Equals("twf-args", StringComparison.InvariantCultureIgnoreCase)))
                {
                    twfProcess.Kill();
                }

                var goTestDir     = Path.Combine(Environment.GetEnvironmentVariable("NK_BUILD"), "go-test", "temporal");
                var workerExePath = Path.Combine(goTestDir, "twf-args.exe");

                // The worker app polls for the existence of a temporary stop file and
                // exits when one is created.

                var fileId   = Guid.NewGuid().ToString("d");
                var tempPath = Path.GetTempPath();

                readyFile = Path.Combine(tempPath, fileId + ".ready");
                stopFile  = Path.Combine(tempPath, fileId + ".stop");

                workerTask = NeonHelper.ExecuteCaptureAsync(workerExePath,
                    new object[]
                    {
                        $"-config={Path.Combine(goTestDir, "config.yaml")}",
                        $"-namespace={TemporalFixture.Namespace}", 
                        $"-taskqueue={TemporalTestHelper.TaskQueue_TwfArgs}", 
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
                    throw new ObjectDisposedException(nameof(TwfArgsWorker));
                }

                File.WriteAllText(stopFile, "STOP");

                var result = workerTask.Result;

                workerTask = null;

                File.Delete(readyFile);
                File.Delete(stopFile);
            }
        }

        /// <summary>
        /// Partial definition of for a GOLANG workflow.
        /// </summary>
        [WorkflowInterface(TaskQueue = TemporalTestHelper.TaskQueue_TwfArgs)]
        public interface IGoWorkflow : IWorkflow
        {
            [WorkflowMethod(Name = "NoArgsWorkflow", IsFullName = true)]
            Task<string> NoArgsAsync();

            [WorkflowMethod(Name = "OneArgWorkflow", IsFullName = true)]
            Task<string> OneArgAsync(string name);

            [WorkflowMethod(Name = "TwoArgsWorkflow", IsFullName = true)]
            Task<string> TwoArgsAsync(string name1, string name2);
        }

        /// <summary>
        /// Partial definition of for a GOLANG activity.
        /// </summary>
        [ActivityInterface(TaskQueue = TemporalTestHelper.TaskQueue_TwfArgs)]
        public interface IGoActivity : IActivity
        {
            [ActivityMethod(Name = "NoArgsActivity", IsFullName = true)]
            Task<string> NoArgsAsync();

            [ActivityMethod(Name = "OneArgActivity", IsFullName = true)]
            Task<string> OneArgAsync(string name);

            [ActivityMethod(Name = "TwoArgsActivity", IsFullName = true)]
            Task<string> TwoArgsAsync(string name1, string name2);
        }

        //---------------------------------------------------------------------

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        public async Task Interop_Workflow_Untyped()
        {
            await SyncContext.Clear;

            // verify that we can execute a GOLANG workflows using 
            // untyped stubs.

            using (new TwfArgsWorker())
            {
                //-----------------------------------------
                // Zero args:

                var options = new StartWorkflowOptions()
                {
                    Id          = "NoArgs-" + Guid.NewGuid().ToString("d"),
                    TaskQueue   = TemporalTestHelper.TaskQueue_TwfArgs
                };

                var stub      = client.NewUntypedWorkflowStub("NoArgsWorkflow", options);
                var execution = await stub.StartAsync();

                Assert.Equal("Hello there!", await stub.GetResultAsync<string>());

                //-----------------------------------------
                // One arg:

                options = new StartWorkflowOptions()
                {
                    Id          = "OneArg-" + Guid.NewGuid().ToString("d"),
                    TaskQueue   = TemporalTestHelper.TaskQueue_TwfArgs
                };

                stub      = client.NewUntypedWorkflowStub("OneArgWorkflow", options);
                execution = await stub.StartAsync("JACK");

                Assert.Equal("Hello JACK!", await stub.GetResultAsync<string>());

                //-----------------------------------------
                // Two Args:

                options = new StartWorkflowOptions()
                {
                    Id          = "TwoArgs-" + Guid.NewGuid().ToString("d"),
                    TaskQueue   = TemporalTestHelper.TaskQueue_TwfArgs
                };

                stub      = client.NewUntypedWorkflowStub("TwoArgsWorkflow", options);
                execution = await stub.StartAsync("JACK", "JILL");

                Assert.Equal("Hello JACK & JILL!", await stub.GetResultAsync<string>());

                //-----------------------------------------
                // One Array Arg:

                options = new StartWorkflowOptions()
                {
                    Id          = "OneArrayArg-" + Guid.NewGuid().ToString("d"),
                    TaskQueue   = TemporalTestHelper.TaskQueue_TwfArgs
                };

                stub      = client.NewUntypedWorkflowStub("ArrayArgWorkflow", options);
                execution = await stub.StartAsync(new int[] { 0, 1, 2, 3, 4 });

                var arrayResult = await stub.GetResultAsync<int[]>();

                Assert.Equal(new int[] { 0, 1, 2, 3, 4 }, arrayResult);

                //-----------------------------------------
                // One Array and a String Arg 

                options = new StartWorkflowOptions()
                {
                    Id          = "OneArrayArgs-" + Guid.NewGuid().ToString("d"),
                    TaskQueue   = TemporalTestHelper.TaskQueue_TwfArgs
                };

                stub = client.NewUntypedWorkflowStub("ArrayArgsWorkflow", options);
                execution = await stub.StartAsync(new int[] { 0, 1, 2, 3, 4 }, "test");

                arrayResult = await stub.GetResultAsync<int[]>();

                Assert.Equal(new int[] { 0, 1, 2, 3, 4 }, arrayResult);

                //-----------------------------------------
                // Workflow that returns just an error:

                // Verify that things work when the workflow DOESN'T return an error.

                options = new StartWorkflowOptions()
                {
                    Id          = "ErrorWorkflow-NOERROR-" + Guid.NewGuid().ToString("d"),
                    TaskQueue   = TemporalTestHelper.TaskQueue_TwfArgs
                };

                stub      = client.NewUntypedWorkflowStub("ErrorWorkflow", options);
                execution = await stub.StartAsync("");

                await stub.GetResultAsync();

                // Verify that things work when the workflow DOES return an error.

                options = new StartWorkflowOptions()
                {
                    Id          = "ErrorWorkflow-ERROR-" + Guid.NewGuid().ToString("d"),
                    TaskQueue   = TemporalTestHelper.TaskQueue_TwfArgs
                };

                stub      = client.NewUntypedWorkflowStub("ErrorWorkflow", options);
                execution = await stub.StartAsync("error message");

                try
                {
                    await stub.GetResultAsync();
                }
                catch (TemporalGenericException e)
                {
                    Assert.Equal("error message", e.Reason);
                }
                catch (Exception e)
                {
                    Assert.True(false, $"Expected [{typeof(TemporalGenericException).FullName}] not [{e.GetType().FullName}].");
                }

                //-----------------------------------------
                // Workflow that returns a string or an error:

                // Verify that things work when the workflow DOESN'T return an error.

                options = new StartWorkflowOptions()
                {
                    Id          = "StringErrorWorkflow-NOERROR-" + Guid.NewGuid().ToString("d"),
                    TaskQueue   = TemporalTestHelper.TaskQueue_TwfArgs
                };

                stub      = client.NewUntypedWorkflowStub("StringErrorWorkflow", options);
                execution = await stub.StartAsync("JEFF", "");

                Assert.Equal("Hello JEFF!", await stub.GetResultAsync<string>());

                // Verify that things work when the workflow DOES return an error.

                options = new StartWorkflowOptions()
                {
                    Id          = "StringErrorWorkflow-ERROR-" + Guid.NewGuid().ToString("d"),
                    TaskQueue   = TemporalTestHelper.TaskQueue_TwfArgs
                };

                stub      = client.NewUntypedWorkflowStub("ErrorWorkflow", options);
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

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskQueue = TemporalTestHelper.TaskQueue)]
        public interface IGoUntypedActivityTester : IWorkflow
        {
            [WorkflowMethod(Name = "NoArgsWorkflow", IsFullName = true)]
            Task<string> NoArgsAsync();

            [WorkflowMethod(Name = "OneArgWorkflow", IsFullName = true)]
            Task<string> OneArgAsync(string name);

            [WorkflowMethod(Name = "TwoArgsWorkflow", IsFullName = true)]
            Task<string> TwoArgsAsync(string name1, string name2);
        }

        [Workflow(AutoRegister = true)]
        public class GoUntypedActivityTester : WorkflowBase, IGoUntypedActivityTester
        {
            private ActivityOptions options = new ActivityOptions() { TaskQueue = TemporalTestHelper.TaskQueue_TwfArgs };

            public async Task<string> NoArgsAsync()
            {
                var stub = Workflow.NewExternalActivityStub("NoArgsActivity", options);

                return await stub.ExecuteAsync<string>();
            }

            public async Task<string> OneArgAsync(string name)
            {
                var stub = Workflow.NewExternalActivityStub("OneArgActivity", options);

                return await stub.ExecuteAsync<string>(name);
            }

            public async Task<string> TwoArgsAsync(string name1, string name2)
            {
                // Test an untyped future stub that returns no value.  We're
                // essentially verifying that this doesn't barf.

                var future1Stub = Workflow.NewActivityFutureStub("TwoArgsActivity", options);
                var future1     = await future1Stub.StartAsync("JACK", "JILL");

                await future1.GetAsync();

                // Now test an a future stub that returns a value.

                var future2Stub = Workflow.NewActivityFutureStub("TwoArgsActivity", options);
                var future2     = await future2Stub.StartAsync<string>("JACK", "JILL");

                return await future2.GetAsync();
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Category, TestArea.NeonCadence)]
        public async Task Interop_Activity_Untyped()
        {
            await SyncContext.Clear;

            // Verify that we can use a untyped activity stub to interoperate 
            // with GOLANG activities using a non-standard activity type name.

            using (new TwfArgsWorker())
            {
                //-----------------------------------------
                // Zero args:

                var stub = client.NewWorkflowStub<IGoUntypedActivityTester>();

                Assert.Equal("Hello there!", await stub.NoArgsAsync());

                //-----------------------------------------
                // One arg:

                stub = client.NewWorkflowStub<IGoUntypedActivityTester>();

                Assert.Equal("Hello JACK!", await stub.OneArgAsync("JACK"));

                //-----------------------------------------
                // Two Args:

                stub = client.NewWorkflowStub<IGoUntypedActivityTester>();

                Assert.Equal("Hello JACK & JILL!", await stub.TwoArgsAsync("JACK", "JILL"));
            }
        }

        //---------------------------------------------------------------------

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Category, TestArea.NeonCadence)]
        public async Task Interop_Workflow_StubFullName()
        {
            await SyncContext.Clear;

            // Verify that we can can used a typed workflow stub to interoperate 
            // with a GOLANG workflow using a non-standard workflow type name.

            using (new TwfArgsWorker())
            {
                //-----------------------------------------
                // Zero args:

                var stub = client.NewWorkflowStub<IGoWorkflow>();

                Assert.Equal("Hello there!", await stub.NoArgsAsync());

                //-----------------------------------------
                // One arg:

                stub = client.NewWorkflowStub<IGoWorkflow>();

                Assert.Equal("Hello JACK!", await stub.OneArgAsync("JACK"));

                //-----------------------------------------
                // Two Args:

                stub = client.NewWorkflowStub<IGoWorkflow>();

                Assert.Equal("Hello JACK & JILL!", await stub.TwoArgsAsync("JACK", "JILL"));
            }
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskQueue = TemporalTestHelper.TaskQueue)]
        public interface IGoActivityTester : IWorkflow
        {
            [WorkflowMethod(Name = "NoArgsWorkflow")]
            Task<string> NoArgsAsync();

            [WorkflowMethod(Name = "OneArgWorkflow")]
            Task<string> OneArgAsync(string name);

            [WorkflowMethod(Name = "TwoArgsWorkflow")]
            Task<string> TwoArgsAsync(string name1, string name2);
        }

        [Workflow(AutoRegister = true)]
        public class GoActivityTester : WorkflowBase, IGoActivityTester
        {
            public async Task<string> NoArgsAsync()
            {
                var stub = Workflow.NewActivityStub<IGoActivity>();

                return await stub.NoArgsAsync();
            }

            public async Task<string> OneArgAsync(string name)
            {
                var stub = Workflow.NewActivityStub<IGoActivity>();

                return await stub.OneArgAsync(name);
            }

            public async Task<string> TwoArgsAsync(string name1, string name2)
            {
                var stub = Workflow.NewActivityStub<IGoActivity>();

                return await stub.TwoArgsAsync(name1, name2);
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Category, TestArea.NeonCadence)]
        public async Task Interop_Activity_StubFullName()
        {
            await SyncContext.Clear;

            // Verify that we can use a typed activity stub to interoperate 
            // with a GOLANG activity using a non-standard activity type name.

            using (new TwfArgsWorker())
            {
                //-----------------------------------------
                // Zero args:

                var stub = client.NewWorkflowStub<IGoWorkflow>();

                Assert.Equal("Hello there!", await stub.NoArgsAsync());

                //-----------------------------------------
                // One arg:

                stub = client.NewWorkflowStub<IGoWorkflow>();

                Assert.Equal("Hello JACK!", await stub.OneArgAsync("JACK"));

                //-----------------------------------------
                // Two Args:

                stub = client.NewWorkflowStub<IGoWorkflow>();

                Assert.Equal("Hello JACK & JILL!", await stub.TwoArgsAsync("JACK", "JILL"));
            }
        }
    }
}
