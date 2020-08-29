//-----------------------------------------------------------------------------
// FILE:        Test_EndToEnd.Activity.cs
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
        //---------------------------------------------------------------------

        private static bool activityTests_ActivityWithNoResultCalled;
        private static bool activityTests_WorkflowWithNoResultCalled;

        [ActivityInterface(TaskList = TemporalTestHelper.TaskList)]
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

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Activity_WithNoResult()
        {
            await SyncContext.ClearAsync;

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

        [ActivityInterface(TaskList = TemporalTestHelper.TaskList)]
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

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Activity_WithResult()
        {
            await SyncContext.ClearAsync;

            // Verify that we can call a simple workflow that accepts a
            // parameter, calls a similarly simple activity that returns
            // a result.

            var stub = client.NewWorkflowStub<IActivityWorkflowWithResult>();

            Assert.Equal("Hello Jeff!", await stub.HelloAsync("Jeff"));
        }

        //---------------------------------------------------------------------

        [ActivityInterface(TaskList = TemporalTestHelper.TaskList)]
        public interface IActivityWithError : IActivity
        {
            [ActivityMethod]
            Task RunAsync();
        }

        [Activity(AutoRegister = true)]
        public class ActivityWithError : ActivityBase, IActivityWithError
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;

                throw new TestException("activity failed");
            }
        }

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
        public interface IActivityWorkflowWithError : IWorkflow
        {
            // These methods return NULL on success, otherwise the error reason.

            [WorkflowMethod(Name = "normal-activity-error")]
            Task<string> ActivityErrorAsync();

            [WorkflowMethod(Name = "local-activity-error")]
            Task<string> LocalActivityErrorAsync();
        }

        [Workflow(AutoRegister = true)]
        public class ActivityWorkflowWithError : WorkflowBase, IActivityWorkflowWithError
        {
            public async Task<string> ActivityErrorAsync()
            {
                var stub = Workflow.NewActivityStub<IActivityWithError>();

                try
                {
                    await stub.RunAsync();

                    return "Exception expected";
                }
                catch (TemporalCustomException e)
                {
                    if (e.Reason != typeof(TestException).FullName)
                    {
                        return $"Expected REASON=[{typeof(TestException).FullName}] ACTUAL=[{e.Reason}]";
                    }
                    else if (e.Message != "activity failed")
                    {
                        return $"Expected MESSAGE=[\"activity failed\"] ACTUAL=[{e.Message}]";
                    }
                    else
                    {
                        return null; // Exception is OK
                    }
                }
                catch (Exception e)
                {
                    return $"Expected EXCEPTION={typeof(TemporalCustomException).FullName}] ACTUAL[{e.GetType().FullName}]";
                }
            }

            public async Task<string> LocalActivityErrorAsync()
            {
                var stub = Workflow.NewLocalActivityStub<IActivityWithError, ActivityWithError>();

                try
                {
                    await stub.RunAsync();

                    return "Exception expected";
                }
                catch (TemporalCustomException e)
                {
                    if (e.Reason != typeof(TestException).FullName)
                    {
                        return $"Expected REASON=[{typeof(TestException).FullName}] ACTUAL=[{e.Reason}]";
                    }
                    else if (e.Message != "activity failed")
                    {
                        return $"Expected MESSAGE=[\"activity failed\"] ACTUAL=[{e.Message}]";
                    }
                    else
                    {
                        return null; // Exception is OK
                    }
                }
                catch (Exception e)
                {
                    return $"Expected EXCEPTION={typeof(TemporalCustomException).FullName}] ACTUAL[{e.GetType().FullName}]";
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_WithError()
        {
            await SyncContext.ClearAsync;

            // Verify that an exception thrown by an normal activity can be caught
            // and verified by the parent workflow.

            var stub = client.NewWorkflowStub<IActivityWorkflowWithError>();

            Assert.Null(await stub.ActivityErrorAsync());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task ActivityLocal_WithError()
        {
            await SyncContext.ClearAsync;

            // Verify that an exception thrown by an normal activity can be caught
            // and verified by the parent workflow.

            var stub = client.NewWorkflowStub<IActivityWorkflowWithError>();

            Assert.Null(await stub.LocalActivityErrorAsync());
        }

        //---------------------------------------------------------------------

        [ActivityInterface()]
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

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task ActivityLocal_WithResult()
        {
            await SyncContext.ClearAsync;

            // Verify that we can call a simple workflow that accepts a
            // parameter, calls a similarly simple local activity that
            // returns a result.

            var stub = client.NewWorkflowStub<ILocalActivityWorkflowWithResult>();

            Assert.Equal("Hello Jeff!", await stub.HelloAsync("Jeff"));
        }

        //---------------------------------------------------------------------

        [ActivityInterface()]
        public interface ILocalActivityWithoutResult : IActivity
        {
            [ActivityMethod]
            Task HelloAsync(string name);
        }

        public class LocalActivityWithouthResult : ActivityBase, ILocalActivityWithoutResult
        {
            public static string Name { get; private set; } = null;
            public static ActivityTask Info { get; set; }

            public static void Reset()
            {
                Name = null;
                Info = null;
            }

            [ActivityMethod]
            public async Task HelloAsync(string name)
            {
                LocalActivityWithouthResult.Name = name;
                LocalActivityWithouthResult.Info = ActivityTask;

                await Task.CompletedTask;
            }
        }

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task ActivityLocal_WithoutResult()
        {
            await SyncContext.ClearAsync;
            LocalActivityWithouthResult.Reset();

            // Verify that we can call a simple workflow that accepts a
            // parameter, calls a similarly simple local activity that
            // doesn't return a result.

            var stub = client.NewWorkflowStub<ILocalActivityWorkflowWithoutResult>();

            await stub.HelloAsync("Jeff");
            Assert.Equal("Jeff", LocalActivityWithouthResult.Name);

            // Also verify that local activities also receive 
            // Activity Task info.

            Assert.NotNull(LocalActivityWithouthResult.Info);
            Assert.Null(LocalActivityWithouthResult.Info.ActivityTypeName);     // This is NULL for local activities
            Assert.Equal(TemporalTestHelper.TaskList, LocalActivityWithouthResult.Info.TaskList);
            Assert.Equal("test-namespace", LocalActivityWithouthResult.Info.WorkflowNamespace);
        }

        //---------------------------------------------------------------------

        [ActivityInterface()]
        public interface ILocalActivityMultipleMethods : IActivity
        {
            [ActivityMethod]
            Task<string> HelloAsync(string name);

            [ActivityMethod(Name = "goodbye")]
            Task<string> GoodbyeAsync(string name);
        }

        public class LocalActivityMultipleMethods : ActivityBase, ILocalActivityMultipleMethods
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

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
        public interface ILocalActivityWorkflowMultipleMethods : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);

            [WorkflowMethod(Name = "goodbye")]
            Task<string> GoodbyeAsync(string name);
        }

        [Workflow(AutoRegister = true)]
        public class LocalActivityWorkflowMultipleMethods : WorkflowBase, ILocalActivityWorkflowMultipleMethods
        {
            public async Task<string> HelloAsync(string name)
            {
                var stub = Workflow.NewLocalActivityStub<ILocalActivityMultipleMethods, LocalActivityMultipleMethods>();

                return await stub.HelloAsync(name);
            }

            public async Task<string> GoodbyeAsync(string name)
            {
                var stub = Workflow.NewLocalActivityStub<ILocalActivityMultipleMethods, LocalActivityMultipleMethods>();

                return await stub.GoodbyeAsync(name);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task ActivityLocal_WithMultipleMethods()
        {
            await SyncContext.ClearAsync;
            LocalActivityWithouthResult.Reset();

            // Verify that we can call different methods of a local activity.

            var stub1 = client.NewWorkflowStub<ILocalActivityWorkflowMultipleMethods>();

            Assert.Equal("Hello Jeff!", await stub1.HelloAsync("Jeff"));

            var stub2 = client.NewWorkflowStub<ILocalActivityWorkflowMultipleMethods>();

            Assert.Equal("Goodbye Jeff!", await stub2.GoodbyeAsync("Jeff"));
        }

        //---------------------------------------------------------------------

        [ActivityInterface(TaskList = TemporalTestHelper.TaskList)]
        public interface IActivityMultipleMethods : IActivity
        {
            [ActivityMethod]
            Task<string> HelloAsync(string name);

            [ActivityMethod(Name = "goodbye")]
            Task<string> GoodbyeAsync(string name);
        }

        public class ActivityMultipleMethods : ActivityBase, IActivityMultipleMethods
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

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
        public interface IActivityWorkflowMultipleMethods : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);

            [WorkflowMethod(Name = "goodbye")]
            Task<string> GoodbyeAsync(string name);
        }

        [Workflow(AutoRegister = true)]
        public class ActivityWorkflowMultipleMethods : WorkflowBase, IActivityWorkflowMultipleMethods
        {
            public async Task<string> HelloAsync(string name)
            {
                var stub = Workflow.NewLocalActivityStub<IActivityMultipleMethods, ActivityMultipleMethods>();

                return await stub.HelloAsync(name);
            }

            public async Task<string> GoodbyeAsync(string name)
            {
                var stub = Workflow.NewLocalActivityStub<IActivityMultipleMethods, ActivityMultipleMethods>();

                return await stub.GoodbyeAsync(name);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Activity_WithMultipleMethods()
        {
            await SyncContext.ClearAsync;
            LocalActivityWithouthResult.Reset();

            // Verify that we can call different methods of a regular activity.

            var stub1 = client.NewWorkflowStub<IActivityWorkflowMultipleMethods>();

            Assert.Equal("Hello Jeff!", await stub1.HelloAsync("Jeff"));
            
            var stub2 = client.NewWorkflowStub<IActivityWorkflowMultipleMethods>();

            Assert.Equal("Goodbye Jeff!", await stub2.GoodbyeAsync("Jeff"));
        }

        //---------------------------------------------------------------------

        [ActivityInterface(TaskList = TemporalTestHelper.TaskList)]
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

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Activity_Logger()
        {
            await SyncContext.ClearAsync;

            // Verify that logging within an activity doesn't barf.

            // $todo(jefflill):
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

        [ActivityInterface(TaskList = TemporalTestHelper.TaskList)]
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

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Activity_MultipleStubs()
        {
            await SyncContext.ClearAsync;

            // Verify that we can reuse an activity stub to make multiple calls.

            var stub = client.NewWorkflowStub<IActivityWorkflowMultipleStubCalls>();
            var list = await stub.RunAsync();

            Assert.Equal(new List<string>() { "Hello Jack!", "Hello Jill!" }, list);
        }

        //---------------------------------------------------------------------

        [ActivityInterface(TaskList = TemporalTestHelper.TaskList)]
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

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Activity_DifferentNames()
        {
            await SyncContext.ClearAsync;

            // Verify that an activity whose class and interface names
            // don't match works.  This ensures that the Temporal client
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

        public enum HeartbeatResult
        {
            OK,
            Error1,
            Error2,
            Error3,
            Error4,
            Error5,
            Error6,
            Error7,
            Error8,
        }

        [ActivityInterface(TaskList = TemporalTestHelper.TaskList)]
        public interface IActivityHeartbeat : IActivity
        {
            [ActivityMethod]
            Task<HeartbeatResult> RunAsync(HeartbeatMode mode);
        }

        [Activity(AutoRegister = true)]
        public class ActivityHeartbeat : ActivityBase, IActivityHeartbeat
        {
            [ActivityMethod]
            public async Task<HeartbeatResult> RunAsync(HeartbeatMode mode)
            {
                switch (mode)
                {
                    case HeartbeatMode.SendHeartbeat:

                        await Activity.RecordHeartbeatAsync(new byte[] { 0, 1, 2, 3, 4 });
                        break;

                    case HeartbeatMode.HeartbeatWithDefaults:

                        // The first heartbeat should always be recorded.

                        if (!await Activity.HeartbeatAsync())
                        {
                            return HeartbeatResult.Error1;
                        }

                        // The next (immediate) heartbeat should not be recorded.

                        if (await Activity.HeartbeatAsync())
                        {
                            return HeartbeatResult.Error2;
                        }

                        // Sleep for 1/2 the heartbeat timeout (plus a bit) and verify that the
                        // next heartbeat is recorded afterwards.

                        await Task.Delay(TimeSpan.FromTicks(Activity.Task.HeartbeatTimeout.Ticks / 2) + TimeSpan.FromMilliseconds(50));

                        if (!await Activity.HeartbeatAsync())
                        {
                            return HeartbeatResult.Error3;
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
                            return HeartbeatResult.Error4;
                        }

                        if (!detailsRetrieved)
                        {
                            return HeartbeatResult.Error5;
                        }
                        break;

                    case HeartbeatMode.HeartbeatWithInterval:

                        // The first heartbeat should always be recorded.

                        if (!await Activity.HeartbeatAsync(interval: TimeSpan.FromSeconds(1)))
                        {
                            return HeartbeatResult.Error6;
                        }

                        // The next (immediate) heartbeat should not be recorded.

                        if (await Activity.HeartbeatAsync())
                        {
                            return HeartbeatResult.Error7;
                        }

                        // Sleep long enough such that we'll definitely exceed the heart minimum
                        // and verify that next heartbeat is recorded.

                        await Task.Delay(TimeSpan.FromTicks(Activity.Task.HeartbeatTimeout.Ticks / 2) + TimeSpan.FromMilliseconds(50));

                        if (!await Activity.HeartbeatAsync())
                        {
                            return HeartbeatResult.Error8;
                        }
                        break;
                }

                return HeartbeatResult.OK;
            }
        }

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
        public interface IWorkflowActivityHeartbeat : IWorkflow
        {
            [WorkflowMethod]
            Task<HeartbeatResult> RunAsync(HeartbeatMode mode);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowActivityHeartbeat : WorkflowBase, IWorkflowActivityHeartbeat
        {
            public async Task<HeartbeatResult> RunAsync(HeartbeatMode mode)
            {
                var stub = Workflow.NewActivityStub<IActivityHeartbeat>(new ActivityOptions() { HeartbeatTimeout = TimeSpan.FromSeconds(10) });

                return await stub.RunAsync(mode);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Activity_Heartbeat()
        {
            await SyncContext.ClearAsync;

            // Verify that recording heartbeats the standard way works.

            var stub = client.NewWorkflowStub<IWorkflowActivityHeartbeat>();

            Assert.Equal(HeartbeatResult.OK, await stub.RunAsync(HeartbeatMode.SendHeartbeat));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Activity_Heartbeat_WithDefaults()
        {
            await SyncContext.ClearAsync;

            // Verify that recording heartbeats the using the convenience method works.

            var stub = client.NewWorkflowStub<IWorkflowActivityHeartbeat>();

            Assert.Equal(HeartbeatResult.OK, await stub.RunAsync(HeartbeatMode.HeartbeatWithDefaults));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Activity_Heartbeat_WithDetails()
        {
            await SyncContext.ClearAsync;

            // Verify that recording heartbeats the using the convenience method works.

            var stub = client.NewWorkflowStub<IWorkflowActivityHeartbeat>();

            Assert.Equal(HeartbeatResult.OK, await stub.RunAsync(HeartbeatMode.HeartbeatWithDetails));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Activity_Heartbeat_WithInterval()
        {
            await SyncContext.ClearAsync;

            // Verify that recording heartbeats the using the convenience method works.

            var stub = client.NewWorkflowStub<IWorkflowActivityHeartbeat>();

            Assert.Equal(HeartbeatResult.OK, await stub.RunAsync(HeartbeatMode.HeartbeatWithInterval));
        }

        //---------------------------------------------------------------------

        [ActivityInterface(TaskList = TemporalTestHelper.TaskList)]
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

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
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
                catch (TemporalException e)
                {
                    return $"{e.Reason}: {e.Message}";
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Activity_Fail()
        {
            await SyncContext.ClearAsync;

            // Verify that we can call a workflow that calls an activity
            // which throws an exception and that we see the error.

            var options = new WorkflowOptions()
            {
                DecisionTaskTimeout = TimeSpan.FromSeconds(60)
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

        [ActivityInterface(TaskList = TemporalTestHelper.TaskList)]
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

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Activity_ComplexData()
        {
            await SyncContext.ClearAsync;

            // Verify that we can round-trip an object to a workflow and activity
            // and then back.

            var data = new ComplexActivityData
            {
                Name = "Jeff",
                Age = 58
            };

            var stub = client.NewWorkflowStub<IWorkflowActivityComplexData>();
            var result = await stub.RunAsync(data);

            Assert.Equal(data.Name, result.Name);
            Assert.Equal(data.Age, result.Age);
        }

        //---------------------------------------------------------------------

        [ActivityInterface(TaskList = TemporalTestHelper.TaskList)]
        public interface IActivityExternalCompletion : IActivity
        {
            [ActivityMethod]
            Task<string> RunAsync();
        }

        [Activity(AutoRegister = true)]
        public class ActivityExternalCompletion : ActivityBase, IActivityExternalCompletion
        {
            public static Activity ActivityInstance = null;

            public static void Reset()
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

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
        public interface IWorkflowActivityExternalCompletion : IWorkflow
        {
            [WorkflowMethod]
            Task<string> RunAsync();

            [WorkflowMethod(Name = "activity-heartbeat-timeout")]
            Task<bool> ActivityHeartbeatTimeoutAsync();

            [WorkflowMethod(Name = "activity-with-heartbeats")]
            Task<string> ActivityWithHeartbeats();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowActivityExternalCompletion : WorkflowBase, IWorkflowActivityExternalCompletion
        {
            public async Task<string> RunAsync()
            {
                var stub = Workflow.NewActivityStub<IActivityExternalCompletion>();

                return await stub.RunAsync();
            }

            public async Task<bool> ActivityHeartbeatTimeoutAsync()
            {
                // We're going to start an activity that indicates that
                // it will be completed externally but will not heartbeat
                // externally so we should see an [ActivityHeartbeatTimeoutException].
                //
                // The method returns TRUE if we catch the desired
                // exception.

                var sleepTime   = TimeSpan.FromSeconds(5);
                var timeoutTime = TimeSpan.FromTicks(sleepTime.Ticks / 2);
                var stub        = Workflow.NewActivityStub<IActivityExternalCompletion>(new ActivityOptions() { HeartbeatTimeout = timeoutTime });

                try
                {
                    await stub.RunAsync();
                }
                catch (ActivityHeartbeatTimeoutException)
                {
                    return true;
                }

                return false;
            }

            public async Task<string> ActivityWithHeartbeats()
            {
                // We're going to start an activity that indicates that
                // it will be completed externally and expects external
                // heartbeats at about 1/sec.
                //
                // The method returns the value returned by the activity.

                var sleepTime   = TimeSpan.FromSeconds(5);
                var timeoutTime = TimeSpan.FromTicks(sleepTime.Ticks / 2);
                var stub        = Workflow.NewActivityStub<IActivityExternalCompletion>(new ActivityOptions() { HeartbeatTimeout = timeoutTime });

                return await stub.RunAsync();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Activity_External_CompleteByToken()
        {
            await SyncContext.ClearAsync;

            // Verify that we can externally heartbeat and complete an activity
            // using its task token.

            ActivityExternalCompletion.Reset();

            var stub     = client.NewWorkflowStub<IWorkflowActivityExternalCompletion>();
            var task     = stub.RunAsync();
            var activity = ActivityExternalCompletion.WaitForActivity();

            await client.ActivityHeartbeatByTokenAsync(activity.Task.TaskToken);
            await client.ActivityHeartbeatByTokenAsync(activity.Task.TaskToken, "Heartbeat");
            await client.ActivityCompleteByTokenAsync(activity.Task.TaskToken, "Hello World!");

            Assert.Equal("Hello World!", await task);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Activity_External_CompleteById()
        {
            await SyncContext.ClearAsync;

            // Verify that we can externally heartbeat and complete an activity
            // using the workflow execution and the activity ID.

            ActivityExternalCompletion.Reset();

            var stub     = client.NewWorkflowStub<IWorkflowActivityExternalCompletion>();
            var task     = stub.RunAsync();
            var activity = ActivityExternalCompletion.WaitForActivity();

            await client.ActivityHeartbeatByIdAsync(activity.Task.WorkflowExecution, activity.Task.ActivityId);
            await client.ActivityHeartbeatByIdAsync(activity.Task.WorkflowExecution, activity.Task.ActivityId, "Heartbeat");
            await client.ActivityCompleteByIdAsync(activity.Task.WorkflowExecution, activity.Task.ActivityId, "Hello World!");

            Assert.Equal("Hello World!", await task);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Activity_External_ErrorByToken()
        {
            await SyncContext.ClearAsync;

            // Verify that we can externally fail an activity
            // using its task token.

            ActivityExternalCompletion.Reset();

            var stub     = client.NewWorkflowStub<IWorkflowActivityExternalCompletion>();
            var task     = stub.RunAsync();
            var activity = ActivityExternalCompletion.WaitForActivity();

            await client.ActivityErrorByTokenAsync(activity.Task.TaskToken, new Exception("external activity failed"));

            try
            {
                await task;
                Assert.True(false, $"Expected [{nameof(TemporalCustomException)}]");
            }
            catch (TemporalCustomException e)
            {
                Assert.Equal(typeof(TestException).FullName, e.Reason);
                Assert.Equal("external activity failed", e.Message);
            }
            catch (Exception e)
            {
                Assert.True(false, $"Expected [{nameof(TemporalCustomException)}] not [{e.GetType().Name}]");
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public async Task Activity_External_ErrorById()
        {
            await SyncContext.ClearAsync;

            // Verify that we can externally fail an activity
            // using the workflow execution and the activity ID.

            ActivityExternalCompletion.Reset();

            var stub     = client.NewWorkflowStub<IWorkflowActivityExternalCompletion>();
            var task     = stub.RunAsync();
            var activity = ActivityExternalCompletion.WaitForActivity();

            await client.ActivityErrorByIdAsync(activity.Task.WorkflowExecution, activity.Task.ActivityId, new Exception("external activity failed"));

            try
            {
                await task;
                Assert.True(false, $"Expected [{nameof(TemporalCustomException)}]");
            }
            catch (TemporalCustomException e)
            {
                Assert.Equal(typeof(TestException).FullName, e.Reason);
                Assert.Equal("external activity failed", e.Message);
            }
            catch (Exception e)
            {
                Assert.True(false, $"Expected [{nameof(TemporalCustomException)}] not [{e.GetType().Name}]");
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_External_HeartbeatTimeout()
        {
            await SyncContext.ClearAsync;

            // Verify that externally completed activities will timeout when
            // there are no recorded heartbeats.

            ActivityExternalCompletion.Reset();

            var stub = client.NewWorkflowStub<IWorkflowActivityExternalCompletion>();

            Assert.True(await stub.ActivityHeartbeatTimeoutAsync());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_External_HeartbeatWithDefaults_ById()
        {
            // Verifies that external heartbeats submitted by ID works.

            await SyncContext.ClearAsync;

            ActivityExternalCompletion.Reset();

            var stub     = client.NewWorkflowStub<IWorkflowActivityExternalCompletion>();
            var task     = stub.ActivityWithHeartbeats();
            var activity = ActivityExternalCompletion.WaitForActivity();

            // Submit several heartbeats externally at 1/sec and and then complete
            // the activity externally to verify that the heartbeats are recorded
            // properly and the the activity doesn't timeout.

            var heartbeatInterval = TimeSpan.FromSeconds(1);

            for (int i = 0; i < 5; i++)
            {
                await Task.Delay(heartbeatInterval);
                await client.ActivityHeartbeatByIdAsync(activity.Task.WorkflowExecution, activity.Task.ActivityId);
            }

            // Have the activity return a result and verify.

            await client.ActivityCompleteByIdAsync(activity.Task.WorkflowExecution, activity.Task.ActivityId, "HELLO WORLD!");

            Assert.Equal("HELLO WORLD!", await task);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_External_HeartbeatWithDefaults_ByToken()
        {
            // Verifies that external heartbeats submitted by token works.

            await SyncContext.ClearAsync;

            ActivityExternalCompletion.Reset();

            var stub     = client.NewWorkflowStub<IWorkflowActivityExternalCompletion>();
            var task     = stub.ActivityWithHeartbeats();
            var activity = ActivityExternalCompletion.WaitForActivity();

            // Submit several heartbeats externally at 1/sec and and then complete
            // the activity externally to verify that the heartbeats are recorded
            // properly and the the activity doesn't timeout.

            var heartbeatInterval = TimeSpan.FromSeconds(1);

            for (int i = 0; i < 5; i++)
            {
                await Task.Delay(heartbeatInterval);
                await client.ActivityHeartbeatByTokenAsync(activity.Task.TaskToken);
            }

            // Have the activity return a result and verify.

            await client.ActivityCompleteByTokenAsync(activity.Task.TaskToken, "HELLO WORLD!");

            Assert.Equal("HELLO WORLD!", await task);
        }

        //---------------------------------------------------------------------

        [ActivityInterface(TaskList = TemporalTestHelper.TaskList)]
        public interface IActivityWithDependency : IActivity
        {
            [ActivityMethod]
            Task<bool> RunAsync();
        }

        [Activity(AutoRegister = true)]
        public class ActivityWithDependency : ActivityBase, IActivityWithDependency
        {
            private ActivityDependency dependency;

            public ActivityWithDependency(ActivityDependency dependency)
            {
                this.dependency = dependency;
            }

            public async Task<bool> RunAsync()
            {
                return await Task.FromResult(dependency != null && dependency.Hello == "World!");
            }
        }

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
        public interface IActivityWorkflowWithDependency : IWorkflow
        {
            [WorkflowMethod]
            Task<bool> RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class ActivityWorkflowWithDependency : WorkflowBase, IActivityWorkflowWithDependency
        {
            public async Task<bool> RunAsync()
            {
                var stub = Workflow.NewActivityStub<IActivityWithDependency>();

                return await stub.RunAsync();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_WithDependency()
        {
            await SyncContext.ClearAsync;

            // Verify that depdency injection works for activities.  Note that the
            // Test_EndToEnd constructor has configured a singleton ActivityDependency
            // instance with NeonHelper.ServiceContainer.  The workflow will invoke
            // an activity that expects this instance to be passed to the constructor.
            //
            // The activity and calling workflow return TRUE when the dependency
            // is passed correctly.

            var stub = client.NewWorkflowStub<IActivityWorkflowWithDependency>();

            Assert.True(await stub.RunAsync());
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
        public interface IActivityWorkflowNullables : IWorkflow
        {
            [WorkflowMethod]
            Task<TimeSpan?> TestAsync(TimeSpan? value);
        }

        [Workflow(AutoRegister = true)]
        public class ActivityWorkflowNullables : WorkflowBase, IActivityWorkflowNullables
        {
            public async Task<TimeSpan?> TestAsync(TimeSpan? value)
            {
                var stub = Workflow.NewActivityStub<IActivityNullables>();

                return await stub.TestAsync(value);
            }
        }

        [ActivityInterface(TaskList = TemporalTestHelper.TaskList)]
        public interface IActivityNullables : IActivity
        {
            [ActivityMethod]
            Task<TimeSpan?> TestAsync(TimeSpan? value);
        }

        [Activity(AutoRegister = true)]
        public class ActivityNullables : ActivityBase, IActivityNullables
        {
            public async Task<TimeSpan?> TestAsync(TimeSpan? value)
            {
                return await Task.FromResult(value);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_Nullable()
        {
            // Verify that nullable activity arguments and results are serialized properly.

            var stub = client.NewWorkflowStub<IActivityWorkflowNullables>();

            Assert.Null(await stub.TestAsync(null));

            stub = client.NewWorkflowStub<IActivityWorkflowNullables>();

            Assert.Equal(TimeSpan.FromSeconds(77), await stub.TestAsync(TimeSpan.FromSeconds(77)));
        }

        //---------------------------------------------------------------------

        [WorkflowInterface(TaskList = TemporalTestHelper.TaskList)]
        public interface IActivityWorkflowDefaultArg : IWorkflow
        {
            [WorkflowMethod(Name = "test")]
            Task<string> TestAsync(bool local, bool useDefault, string value = "default");
        }

        [Workflow(AutoRegister = true)]
        public class ActivityWorkflowDefaultArg : WorkflowBase, IActivityWorkflowDefaultArg
        {
            public async Task<string> TestAsync(bool local, bool useDefault, string value)
            {
                var stub = Workflow.NewActivityStub<IActivityDefaultArg>();

                return await stub.TestAsync(value);
            }
        }

        [ActivityInterface(TaskList = TemporalTestHelper.TaskList)]
        public interface IActivityDefaultArg : IActivity
        {
            [ActivityMethod]
            Task<string> TestAsync(string value = "default");
        }

        [Activity(AutoRegister = true)]
        public class ActivityDefaultArg : ActivityBase, IActivityDefaultArg
        {
            public async Task<string> TestAsync(string value)
            {
                return await Task.FromResult(value);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity_Default_Arg()
        {
            // Normal Activity: Verify that calling an activitiy with default arguments works.

            var stub = client.NewWorkflowStub<IActivityWorkflowDefaultArg>();
            Assert.Equal("default", await stub.TestAsync(local: false, useDefault: true));

            stub = client.NewWorkflowStub<IActivityWorkflowDefaultArg>();
            Assert.Equal("test", await stub.TestAsync(local: false, useDefault: false, "test"));

            // Local Activity: Verify that calling an activitiy with default arguments works.

            stub = client.NewWorkflowStub<IActivityWorkflowDefaultArg>();
            Assert.Equal("default", await stub.TestAsync(local: true, useDefault: true));

            stub = client.NewWorkflowStub<IActivityWorkflowDefaultArg>();
            Assert.Equal("test", await stub.TestAsync(local: true, useDefault: false, "test"));
        }
    }
}
