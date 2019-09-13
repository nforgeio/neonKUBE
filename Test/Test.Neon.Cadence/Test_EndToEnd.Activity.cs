//-----------------------------------------------------------------------------
// FILE:        Test_EndToEnd.Activity.cs
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
        //---------------------------------------------------------------------

        private static bool activityTests_ActivityWithNoResultCalled;
        private static bool activityTests_WorkflowWithNoResultCalled;

        public interface IActivityWithNoResult : IActivity
        {
            [ActivityMethod]
            Task RunAsync();
        }

        [Activity(AutoRegister = true)]
        public class ActivityWithNoResult : ActivityBase, IActivityWithNoResult
        {
            public async Task RunAsync()
            {
                activityTests_ActivityWithNoResultCalled = true;

                await Task.CompletedTask;
            }
        }

        public interface IActivityWorkflowWithNoResult : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class ActivityWorkflowWithNoResult : WorkflowBase, IActivityWorkflowWithNoResult
        {
            public async Task RunAsync()
            {
                activityTests_WorkflowWithNoResultCalled = true;

                var stub = Workflow.NewActivityStub<IActivityWithNoResult>();

                await stub.RunAsync();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_WithNoResult()
        {
            // Verify that we can call a simple workflow that accepts a
            // parameter, calls a similarly simple activity and results
            // a result.

            activityTests_ActivityWithNoResultCalled = false;
            activityTests_WorkflowWithNoResultCalled = false;

            var stub = client.NewWorkflowStub<IActivityWorkflowWithNoResult>();

            await stub.RunAsync();

            Assert.True(activityTests_WorkflowWithNoResultCalled);
            Assert.True(activityTests_ActivityWithNoResultCalled);
        }

        //---------------------------------------------------------------------

        public interface IActivityWithResult : IActivity
        {
            [ActivityMethod]
            Task<string> HelloAsync(string name);
        }

        [Activity(AutoRegister = true)]
        public class ActivityWithResult : ActivityBase, IActivityWithResult
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }
        }

        public interface IActivityWorkflowWithResult : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);
        }

        [Workflow(AutoRegister = true)]
        public class ActivityWorkflowWithResult : WorkflowBase, IActivityWorkflowWithResult
        {
            public async Task<string> HelloAsync(string name)
            {
                var stub = Workflow.NewActivityStub<IActivityWithResult>();

                return await stub.HelloAsync(name);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_WithResult()
        {
            // Verify that we can call a simple workflow that accepts a
            // parameter, calls a similarly simple activity that returns
            // a result.

            var stub = client.NewWorkflowStub<IActivityWorkflowWithResult>();

            Assert.Equal("Hello Jeff!", await stub.HelloAsync("Jeff"));
        }

        //---------------------------------------------------------------------

        public interface ILocalActivityWithResult : IActivity
        {
            [ActivityMethod]
            Task<string> HelloAsync(string name);
        }

        public class LocalActivityWithResult : ActivityBase, ILocalActivityWithResult
        {
            [ActivityMethod]
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }
        }

        public interface ILocalActivityWorkflowWithResult : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);
        }

        [Workflow(AutoRegister = true)]
        public class LocalActivityWorkflowWithResult : WorkflowBase, ILocalActivityWorkflowWithResult
        {
            public async Task<string> HelloAsync(string name)
            {
                var stub = Workflow.NewLocalActivityStub<ILocalActivityWithResult, LocalActivityWithResult>();

                return await stub.HelloAsync(name);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task LocalActivity_WithResult()
        {
            // Verify that we can call a simple workflow that accepts a
            // parameter, calls a similarly simple local activity that
            // returns a result.

            var stub = client.NewWorkflowStub<ILocalActivityWorkflowWithResult>();

            Assert.Equal("Hello Jeff!", await stub.HelloAsync("Jeff"));
        }

        //---------------------------------------------------------------------

        public interface ILocalActivityWithoutResult : IActivity
        {
            [ActivityMethod]
            Task HelloAsync(string name);
        }

        public class LocalActivityWithouthResult : ActivityBase, ILocalActivityWithoutResult
        {
            public static string Name { get; private set; } = null;

            public new static void Reset()
            {
                Name = null;
            }

            [ActivityMethod]
            public async Task HelloAsync(string name)
            {
                LocalActivityWithouthResult.Name = name;

                await Task.CompletedTask;
            }
        }

        public interface ILocalActivityWorkflowWithoutResult : IWorkflow
        {
            [WorkflowMethod]
            Task HelloAsync(string name);
        }

        [Workflow(AutoRegister = true)]
        public class LocalActivityWorkflowWithoutResult : WorkflowBase, ILocalActivityWorkflowWithoutResult
        {
            public async Task HelloAsync(string name)
            {
                var stub = Workflow.NewLocalActivityStub<ILocalActivityWithoutResult, LocalActivityWithouthResult>();

                await stub.HelloAsync(name);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task LocalActivity_WithoutResult()
        {
            LocalActivityWithouthResult.Reset();

            // Verify that we can call a simple workflow that accepts a
            // parameter, calls a similarly simple local activity that
            // doesn't return a result.

            var stub = client.NewWorkflowStub<ILocalActivityWorkflowWithoutResult>();

            await stub.HelloAsync("Jeff");
            Assert.Equal("Jeff", LocalActivityWithouthResult.Name);
        }

        //---------------------------------------------------------------------

        public interface IActivityLogger : IActivity
        {
            [ActivityMethod]
            Task RunAsync();
        }

        [Activity(AutoRegister = true)]
        public class ActivityLogger : ActivityBase, IActivityLogger
        {
            public async Task RunAsync()
            {
                Activity.Logger.LogInfo("Hello World!");
                await Task.CompletedTask;
            }
        }

        public interface IActivityWorkflowLogger : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class ActivityWorkflowLogger : WorkflowBase, IActivityWorkflowLogger
        {
            public async Task RunAsync()
            {
                var stub = Workflow.NewActivityStub<IActivityLogger>();
                
                await stub.RunAsync();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_Logger()
        {
            // Verify that logging within an activity doesn't barf.

            // $todo(jeff.lill):
            //
            // It would be nice to add additional tests that actually
            // verify that something reasonable was logged, including
            // using the workflow run ID as the log context.
            //
            // I did verify this manually.

            var stub = client.NewWorkflowStub<IActivityWorkflowLogger>();

            await stub.RunAsync();
        }

        //---------------------------------------------------------------------

        public interface IActivityMultipleStubCalls : IActivity
        {
            [ActivityMethod]
            Task<string> HelloAsync(string name);
        }

        [Activity(AutoRegister = true)]
        public class ActivityMultipleStubCalls : ActivityBase, IActivityMultipleStubCalls
        {
            [ActivityMethod]
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }
        }

        public interface IActivityWorkflowMultipleStubCalls : IWorkflow
        {
            [WorkflowMethod]
            Task<List<string>> RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class ActivityWorkflowMultipleStubCalls : WorkflowBase, IActivityWorkflowMultipleStubCalls
        {
            public async Task<List<string>> RunAsync()
            {
                var stub = Workflow.NewActivityStub<IActivityMultipleStubCalls>();
                var list = new List<string>();

                list.Add(await stub.HelloAsync("Jack"));
                list.Add(await stub.HelloAsync("Jill"));

                return list;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_MultipleStubs()
        {
            // Verify that we can reuse an activity stub to make multiple calls.

            var stub = client.NewWorkflowStub<IActivityWorkflowMultipleStubCalls>();
            var list = await stub.RunAsync();

            Assert.Equal(new List<string>() { "Hello Jack!", "Hello Jill!" }, list);
        }

        //---------------------------------------------------------------------

        public interface IActivityDifferentNamesInterface : IActivity
        {
            [ActivityMethod]
            Task<string> HelloAsync(string name);
        }

        [Activity(AutoRegister = true)]
        public class ActivityDifferentNamesClass : ActivityBase, IActivityDifferentNamesInterface
        {
            [ActivityMethod]
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }
        }

        public interface IWorkflowActivityDifferentNames : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowActivityDifferentNames : WorkflowBase, IWorkflowActivityDifferentNames
        {
            public async Task<string> HelloAsync(string name)
            {
                var stub = Workflow.NewActivityStub<IActivityDifferentNamesInterface>();

                return await stub.HelloAsync(name);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_DifferentNames()
        {
            // Verify that an activity whose class and interface names
            // don't match works.  This ensures that the Cadence client
            // doesn't make any assumptions about naming conventions.
            //
            // ...which was happening in earlier times.

            var stub = client.NewWorkflowStub<IWorkflowActivityDifferentNames>();

            Assert.Equal($"Hello Jeff!", await stub.HelloAsync("Jeff"));
        }

        //---------------------------------------------------------------------

        public enum HeartbeatMode
        {
            SendHeartbeat,
            HeartbeatWithDefaults,
            HeartbeatWithDetails,
            HeartbeatWithInterval
        }

        public interface IActivityHeartbeat : IActivity
        {
            [ActivityMethod]
            Task<bool> RunAsync(HeartbeatMode mode);
        }

        [Activity(AutoRegister = true)]
        public class ActivityHeartbeat : ActivityBase, IActivityHeartbeat
        {
            [ActivityMethod]
            public async Task<bool> RunAsync(HeartbeatMode mode)
            {
                switch (mode)
                {
                    case HeartbeatMode.SendHeartbeat:

                        await Activity.SendHeartbeatAsync(new byte[] { 0, 1, 2, 3, 4 });
                        break;

                    case HeartbeatMode.HeartbeatWithDefaults:

                        // The first heartbeat should always be recorded.

                        if (!await Activity.HeartbeatAsync())
                        {
                            return false;
                        }

                        // The next (immediate) heartbeat should not be recorded.

                        if (await Activity.HeartbeatAsync())
                        {
                            return false;
                        }

                        // Sleep for 1/2 the heartbeat timeout and verify that the
                        // next heartbeat is recorded afterwards.

                        await Task.Delay(TimeSpan.FromTicks(Activity.Task.HeartbeatTimeout.Ticks / 2));

                        if (!await Activity.HeartbeatAsync())
                        {
                            return false;
                        }
                        break;

                    case HeartbeatMode.HeartbeatWithDetails:

                        var detailsRetrieved = false;

                        if (!await Activity.HeartbeatAsync(
                            () =>
                            {
                                detailsRetrieved = true;
                                return new byte[] { 0, 1, 2, 3, 4 };
                            }))
                        {
                            return false;
                        }

                        if (!detailsRetrieved)
                        {
                            return false;
                        }
                        break;

                    case HeartbeatMode.HeartbeatWithInterval:

                        // The first heartbeat should always be recorded.

                        if (!await Activity.HeartbeatAsync(interval: TimeSpan.FromSeconds(1)))
                        {
                            return false;
                        }

                        // The next (immediate) heartbeat should not be recorded.

                        if (await Activity.HeartbeatAsync())
                        {
                            return false;
                        }

                        // Sleep for 1 second which is less than the 5 second heartbeat timeout
                        // specified by the workflow and verify that next heartbeat is recorded.

                        await Task.Delay(TimeSpan.FromSeconds(1));

                        if (!await Activity.HeartbeatAsync())
                        {
                            return false;
                        }
                        break;
                }

                return true;
            }
        }

        public interface IWorkflowActivityHeartbeat : IWorkflow
        {
            [WorkflowMethod]
            Task<bool> RunAsync(HeartbeatMode mode);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowActivityHeartbeat : WorkflowBase, IWorkflowActivityHeartbeat
        {
            public async Task<bool> RunAsync(HeartbeatMode mode)
            {
                var stub = Workflow.NewActivityStub<IActivityHeartbeat>(new ActivityOptions() { HeartbeatTimeout = TimeSpan.FromSeconds(10) });

                return await stub.RunAsync(mode);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_SendHeartbeat()
        {
            // Verify that recording heartbeats the standard way works.

            var stub = client.NewWorkflowStub<IWorkflowActivityHeartbeat>();

            Assert.True(await stub.RunAsync(HeartbeatMode.SendHeartbeat));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_Heartbeat_WithDefaults()
        {
            // Verify that recording heartbeats the using the convinence method works.

            var stub = client.NewWorkflowStub<IWorkflowActivityHeartbeat>();

            Assert.True(await stub.RunAsync(HeartbeatMode.HeartbeatWithDefaults));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_Heartbeat_WithDetails()
        {
            // Verify that recording heartbeats the using the convinence method works.

            var stub = client.NewWorkflowStub<IWorkflowActivityHeartbeat>();

            Assert.True(await stub.RunAsync(HeartbeatMode.HeartbeatWithDetails));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_Heartbeat_WithInterval()
        {
            // Verify that recording heartbeats the using the convinence method works.

            var stub = client.NewWorkflowStub<IWorkflowActivityHeartbeat>();

            Assert.True(await stub.RunAsync(HeartbeatMode.HeartbeatWithInterval));
        }

        //---------------------------------------------------------------------

        public interface IActivityFail : IActivity
        {
            [ActivityMethod]
            Task RunAsync();
        }

        [Activity(AutoRegister = true)]
        public class ActivityFail : ActivityBase, IActivityFail
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;
                throw new ArgumentException("forced-failure");
            }
        }

        public interface IWorkflowActivityFail : IWorkflow
        {
            [WorkflowMethod]
            Task<string> RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowActivityFail : WorkflowBase, IWorkflowActivityFail
        {
            public async Task<string> RunAsync()
            {
                var options = new ActivityOptions()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(5)
                };

                var stub = Workflow.NewActivityStub<IActivityFail>(options);

                try
                {
                    await stub.RunAsync();
                    return null;
                }
                catch (Exception e)
                {
                    return e.Message;
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_Fail()
        {
            // Verify that we can call a workflow that calls an activity
            // which throws an exception and that we see the error.

            var options = new WorkflowOptions()
            {
                TaskStartToCloseTimeout = TimeSpan.FromSeconds(60)
            };

            var stub  = client.NewWorkflowStub<IWorkflowActivityFail>(options);
            var error = await stub.RunAsync();

            Assert.NotNull(error);
            Assert.Contains("ArgumentException", error);
            Assert.Contains("forced-failure", error);
        }

        //---------------------------------------------------------------------

        public class ComplexActivityData
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }

        public interface IActivityData : IActivity
        {
            [ActivityMethod]
            Task<ComplexActivityData> RunAsync(ComplexActivityData data);
        }

        [Activity(AutoRegister = true)]
        public class ActivityData : ActivityBase, IActivityData
        {
            public async Task<ComplexActivityData> RunAsync(ComplexActivityData data)
            {
                return await Task.FromResult(data);
            }
        }

        public interface IWorkflowActivityComplexData : IWorkflow
        {
            [WorkflowMethod]
            Task<ComplexActivityData> RunAsync(ComplexActivityData data);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowActivityComplexDataClass : WorkflowBase, IWorkflowActivityComplexData
        {
            public async Task<ComplexActivityData> RunAsync(ComplexActivityData data)
            {
                var stub = Workflow.NewActivityStub<IActivityData>();

                return await stub.RunAsync(data);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_ComplexData()
        {
            // Verify that we can round-trip an object to a workflow and activity
            // and then back.

            var data = new ComplexActivityData
            {
                Name = "Jeff",
                Age  = 58
            };

            var stub   = client.NewWorkflowStub<IWorkflowActivityComplexData>();
            var result = await stub.RunAsync(data);

            Assert.Equal(data.Name, result.Name);
            Assert.Equal(data.Age, result.Age);
        }

        //---------------------------------------------------------------------

        public interface IActivityExternalCompletion : IActivity
        {
            [ActivityMethod]
            Task<string> RunAsync();
        }

        [Activity(AutoRegister = true)]
        public class ActivityExternalCompletion : ActivityBase, IActivityExternalCompletion
        {
            public static Activity ActivityInstance = null;

            public static new void Reset()
            {
                ActivityInstance = null;
            }

            public static Activity WaitForActivity()
            {
                NeonHelper.WaitFor(() => ActivityInstance != null, timeout: TimeSpan.FromSeconds(90));
                Thread.Sleep(TimeSpan.FromSeconds(1));      // Give the activity method a chance to return.

                return ActivityInstance;
            }

            public async Task<string> RunAsync()
            {
                ActivityInstance = this.Activity;
                Activity.DoNotCompleteOnReturn();

                return await Task.FromResult<string>(null);
            }
        }

        public interface IWorkflowActivityExternalCompletion : IWorkflow
        {
            [WorkflowMethod]
            Task<string> RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowActivityExternalCompletion : WorkflowBase, IWorkflowActivityExternalCompletion
        {
            public async Task<string> RunAsync()
            {
                var stub = Workflow.NewActivityStub<IActivityExternalCompletion>();

                return await stub.RunAsync();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_ExternalSuccessByToken()
        {
            // Verify that we can externally heartbeat and complete an activity
            // using its task token.

            ActivityExternalCompletion.Reset();

            var stub     = client.NewWorkflowStub<IWorkflowActivityExternalCompletion>();
            var task     = stub.RunAsync();
            var activity = ActivityExternalCompletion.WaitForActivity();

            await client.ActivityHeartbeatByTokenAsync(activity.Task.TaskToken);
            await client.ActivityHeartbeatByTokenAsync(activity.Task.TaskToken, "Heartbeat");
            await client.ActivityCompletedByTokenAsync(activity.Task.TaskToken, "Hello World!");

            Assert.Equal("Hello World!", await task);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_ExternalSuccessById()
        {
            // Verify that we can externally heartbeat and complete an activity
            // using the workflow execution and the activity ID.

            ActivityExternalCompletion.Reset();

            var stub     = client.NewWorkflowStub<IWorkflowActivityExternalCompletion>();
            var task     = stub.RunAsync();
            var activity = ActivityExternalCompletion.WaitForActivity();

            await client.ActivityHeartbeatByIdAsync(activity.Task.WorkflowExecution, activity.Task.ActivityId);
            await client.ActivityHeartbeatByIdAsync(activity.Task.WorkflowExecution, activity.Task.ActivityId, "Heartbeat");
            await client.ActivityCompletedByIdAsync(activity.Task.WorkflowExecution, activity.Task.ActivityId, "Hello World!");

            Assert.Equal("Hello World!", await task);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_ExternalFailByToken()
        {
            // Verify that we can externally fail an activity
            // using its task token.

            ActivityExternalCompletion.Reset();

            var stub     = client.NewWorkflowStub<IWorkflowActivityExternalCompletion>();
            var task     = stub.RunAsync();
            var activity = ActivityExternalCompletion.WaitForActivity();

            await client.ActivityFailByTokenAsync(activity.Task.TaskToken, new Exception("error"));

            try
            {
                await task;
            }
            catch (Exception e)
            {
                // $todo(jeff.lill): Verify the exception
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_ExternalFailById()
        {
            // Verify that we can externally heartbeat and complete an activity
            // using the workflow execution and the activity ID.

            ActivityExternalCompletion.Reset();

            var stub     = client.NewWorkflowStub<IWorkflowActivityExternalCompletion>();
            var task     = stub.RunAsync();
            var activity = ActivityExternalCompletion.WaitForActivity();

            await client.ActivityFailByIdAsync(activity.Task.WorkflowExecution, activity.Task.ActivityId, new Exception("error"));

            try
            {
                await task;
            }
            catch (Exception e)
            {
                // $todo(jeff.lill): Verify the exception
            }
        }

        [Fact(Skip = "Implement this")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task ActivityExternalCancelByToken()
        {
            // $todo(jeff.lill): Implement this

            await Task.CompletedTask;
        }

        [Fact(Skip = "Implement this")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task ActivityExternalCancelById()
        {
            // $todo(jeff.lill): Implement this

            await Task.CompletedTask;
        }
    }
}
