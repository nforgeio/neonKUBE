//-----------------------------------------------------------------------------
// FILE:        Test_Options.cs
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

using Microsoft.Extensions.DependencyInjection;
using Xunit;

using Neon.Common;
using Neon.Data;
using Neon.IO;
using Neon.Temporal;
using Neon.Temporal.Internal;
using Neon.Xunit;
using Neon.Xunit.Temporal;

namespace TestTemporal
{
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public partial class Test_Options : IClassFixture<TemporalFixture>, IDisposable
    {
        // IMPLEMENTATION NOTES:
        //
        // This implements tests that verify that the workflow and activity options are determined
        // correctly by taking interface and method attributes into account as well as specific
        // workflow and activity options and default client settings.
        //
        // We're going to establish three client connections in addition to default client connection
        // established by the TemporalFixture.  Two of these connections will be setup to default to
        // the "test1-taskqueue" and "test2-namespace" task queues and "test2-taskqueue" and "test2-namespace"
        // namespaces.  The third client will not be assigned a default task queue and the namespace
        // we'll be left as the default.
        //
        // We'll start workers for the two new namespaces and tasks lists which will implement very
        // simple workflows and activities.  The tests will configure handlers for these internal
        // events:
        //
        //      client.LocalActivityExecuteEvent
        //      client.ActivityExecuteEvent
        //      client.WorkflowExecuteEvent
        //      client.ChildWorkflowExecuteEvent
        //
        // These events will be raised just before the workflow or activity is executed, passing
        // the workflow/activity options.  The tests will verify that the options were set correctly
        // based on the workflow/activity interface and methods attributes combined with any specific
        // options and finally, the client settings.
        //
        // Option Precedence (highest to lowest)
        // -------------------------------------
        //
        //  1. StartWorkflowOptions, ChildWorkflowOptions, ActivityOptions, LocalActivityOptions
        //
        //      Any options specified explicitly in options passed to an execute method
        //      will take precedence over all other settings.  This allows developers to
        //      override settings for any specific execution at will.
        //
        //  2. Settings specified by [WorkflowMethod] or [ActivityMethod] attribute tagging
        //     the workflow or activity interface method definition.
        //
        //  3. Settings specified by [WorkflowInterface] or [ActivityInterface] attribute tagging
        //     the workflow or activity interface definition.
        //
        //  4. Client settings.

        private TemporalFixture      fixture;
        private TemporalClient       fixtureClient;
        private TemporalSettings     fixtureSettings;
        private TemporalClient       test1Client;
        private TemporalClient       test2Client;
        private TemporalClient       test3Client;
        private TemporalSettings     test1Settings;
        private TemporalSettings     test2Settings;
        private TemporalSettings     test3Settings;

        public Test_Options(TemporalFixture fixture)
        {
            fixtureSettings = new TemporalSettings()
            {
                Namespace              = TemporalFixture.Namespace,
                ProxyLogLevel          = TemporalTestHelper.ProxyLogLevel,
                CreateNamespace        = true,
                Debug                  = TemporalTestHelper.Debug,
                DebugPrelaunched       = TemporalTestHelper.DebugPrelaunched,
                DebugDisableHeartbeats = TemporalTestHelper.DebugDisableHeartbeats,
                ClientIdentity         = TemporalTestHelper.ClientIdentity
            };

            test1Settings = new TemporalSettings()
            {
                Namespace                             = "test1-namespace",
                TaskQueue                      = "test1-taskqueue",
                ProxyLogLevel                         = TemporalTestHelper.ProxyLogLevel,
                CreateNamespace                       = true,
                Debug                                 = TemporalTestHelper.Debug,
                DebugPrelaunched                      = TemporalTestHelper.DebugPrelaunched,
                DebugDisableHeartbeats                = TemporalTestHelper.DebugDisableHeartbeats,
                ClientIdentity                        = TemporalTestHelper.ClientIdentity,

                // Initialize these to custom values for verification.

                ActivityHeartbeatTimeoutSeconds       = 20,
                ActivityScheduleToCloseTimeoutSeconds = 30,
                ActivityScheduleToStartTimeoutSeconds = 40,
                ActivityStartToCloseTimeoutSeconds    = 50,
                WorkflowTaskTimeoutSeconds    = 10,
                WorkflowIdReusePolicy                 = WorkflowIdReusePolicy.UseDefault,
                WorkflowExecutionTimeoutSeconds    = 70,
                WorkflowRunTimeoutSeconds = 80,
            };

            test2Settings = new TemporalSettings()
            {
                Namespace                             = "test2-namespace",
                TaskQueue                      = "test2-taskqueue",
                ProxyLogLevel                         = TemporalTestHelper.ProxyLogLevel,
                CreateNamespace                       = true,
                Debug                                 = TemporalTestHelper.Debug,
                DebugPrelaunched                      = TemporalTestHelper.DebugPrelaunched,
                DebugDisableHeartbeats                = TemporalTestHelper.DebugDisableHeartbeats,
                ClientIdentity                        = TemporalTestHelper.ClientIdentity,

                // Initialize these to custom values for verification.

                ActivityHeartbeatTimeoutSeconds       = 21,
                ActivityScheduleToCloseTimeoutSeconds = 31,
                ActivityScheduleToStartTimeoutSeconds = 41,
                ActivityStartToCloseTimeoutSeconds    = 51,
                WorkflowTaskTimeoutSeconds    = 11,
                WorkflowIdReusePolicy                 = WorkflowIdReusePolicy.AllowDuplicate,
                WorkflowExecutionTimeoutSeconds    = 71,
                WorkflowRunTimeoutSeconds = 81,
            };

            test3Settings = new TemporalSettings()
            {
                TaskQueue                      = null,
                ProxyLogLevel                         = TemporalTestHelper.ProxyLogLevel,
                CreateNamespace                       = true,
                Debug                                 = TemporalTestHelper.Debug,
                DebugPrelaunched                      = TemporalTestHelper.DebugPrelaunched,
                DebugDisableHeartbeats                = TemporalTestHelper.DebugDisableHeartbeats,
                ClientIdentity                        = TemporalTestHelper.ClientIdentity,

                // Initialize these to custom values for verification.

                ActivityHeartbeatTimeoutSeconds       = 22,
                ActivityScheduleToCloseTimeoutSeconds = 32,
                ActivityScheduleToStartTimeoutSeconds = 42,
                ActivityStartToCloseTimeoutSeconds    = 52,
                WorkflowTaskTimeoutSeconds    = 12,
                WorkflowIdReusePolicy                 = WorkflowIdReusePolicy.RejectDuplicate,
                WorkflowExecutionTimeoutSeconds    = 72,
                WorkflowRunTimeoutSeconds = 82,
            };

            if (fixture.Start(fixtureSettings, composeFile: TemporalTestHelper.TemporalStackDefinition, reconnect: true, keepRunning: TemporalTestHelper.KeepTemporalServerOpen) == TestFixtureStatus.Started)
            {
                // Initialize the default default client and worker.

                this.fixture       = fixture;
                this.fixtureClient = fixture.Client;

                var fixtureWorker = fixtureClient.NewWorkerAsync().Result;

                fixtureWorker.RegisterAssemblyAsync(Assembly.GetExecutingAssembly()).Wait();
                fixtureWorker.StartAsync().Wait();

                // Initialize the test clients and workers

                test1Settings.HostPort        = fixtureClient.Settings.HostPort;
                test1Client                   = TemporalClient.ConnectAsync(test1Settings).Result;
                fixture.State["test1-client"] = test1Client;

                var test1Worker = test1Client.NewWorkerAsync().Result;

                test1Worker.RegisterAssemblyAsync(Assembly.GetExecutingAssembly()).Wait();
                test1Worker.StartAsync().Wait();

                test2Settings.HostPort        = fixtureClient.Settings.HostPort;
                test2Client                   = TemporalClient.ConnectAsync(test2Settings).Result;
                fixture.State["test2-client"] = test2Client;

                var test2Worker = test2Client.NewWorkerAsync().Result;

                test2Worker.RegisterAssemblyAsync(Assembly.GetExecutingAssembly()).Wait();
                test2Worker.StartAsync().Wait();

                test3Settings.HostPort        = fixtureClient.Settings.HostPort;
                test3Client                   = TemporalClient.ConnectAsync(test3Settings).Result;
                fixture.State["test3-client"] = test3Client;

                var test3Worker = test3Client.NewWorkerAsync().Result;

                test3Worker.RegisterAssemblyAsync(Assembly.GetExecutingAssembly()).Wait();
                test3Worker.StartAsync().Wait();
            }
            else
            {
                // Load the default client.

                this.fixture       = fixture;
                this.fixtureClient = fixture.Client;

                // Load the test clients from fixture state.

                test1Client = (TemporalClient)fixture.State["test1-client"];
                test2Client = (TemporalClient)fixture.State["test2-client"];
                test3Client = (TemporalClient)fixture.State["test3-client"];
            }
        }

        public void Dispose()
        {
        }

        //---------------------------------------------------------------------
        // Test workflows and activities

        [WorkflowInterface]
        public interface IWorkflowWithNoAttributes : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowWithNoAttributes : WorkflowBase, IWorkflowWithNoAttributes
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;
            }
        }

        [WorkflowInterface(Namespace = "test2-namespace", TaskQueue = "test2-taskqueue")]
        public interface IWorkflowWithInterfaceAttributes : IWorkflow
        {
            [WorkflowMethod]
            Task RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowWithInterfaceAttributes : WorkflowBase, IWorkflowWithInterfaceAttributes
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;
            }
        }

        // NOTE: These properties will be overriden by the method attribute below.

        [WorkflowInterface(Namespace = "test2-namespace", TaskQueue = "test2-taskqueue")]
        public interface IWorkflowWithMethodAttributes : IWorkflow
        {
            [WorkflowMethod(
                Namespace                       = "test1-namespace", 
                TaskQueue                       = "test1-taskqueue",
                WorkflowTaskTimeoutSeconds      = 55,
                WorkflowRunTimeoutSeconds       = 56,
                WorkflowExecutionTimeoutSeconds = 57,
                WorkflowIdReusePolicy           = WorkflowIdReusePolicy.RejectDuplicate)]
            Task RunAsync();
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowWithMethodAttributes : WorkflowBase, IWorkflowWithMethodAttributes
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;
            }
        }

        [WorkflowInterface(Namespace = TemporalFixture.Namespace, TaskQueue = TemporalTestHelper.TaskQueue)]
        public interface IOptionsTester : IWorkflow
        {
            [WorkflowMethod(Name = "ExecuteChildWithNoAttributes")]
            Task ExecuteChildWithNoAttributes();

            [WorkflowMethod(Name = "ExecuteChildWithInterfaceAttributes")]
            Task ExecuteChildWithInterfaceAttributes();

            [WorkflowMethod(Name = "ExecuteChildWithMethodAttributes")]
            Task ExecuteChildWithMethodAttributes();

            [WorkflowMethod(Name = "ExecuteChildWithOptions")]
            Task ExecuteChildWithOptions(ChildWorkflowOptions options);

            [WorkflowMethod(Name = "ExecuteActivityWithNoAttributes")]
            Task ExecuteActivityWithNoAttributes();

            [WorkflowMethod(Name = "ExecuteActivityWithInterfaceAttributes")]
            Task ExecuteActivityWithInterfaceAttributes();

            [WorkflowMethod(Name = "ExecuteActivityWithMethodAttributes")]
            Task ExecuteActivityWithMethodAttributes(ActivityOptions options = null);
        }

        [Workflow(AutoRegister = true)]
        public class OptionsTester : WorkflowBase, IOptionsTester
        {
            public async Task ExecuteChildWithInterfaceAttributes()
            {
                var stub = Workflow.NewChildWorkflowStub<IWorkflowWithInterfaceAttributes>();

                await stub.RunAsync();
            }

            public async Task ExecuteChildWithMethodAttributes()
            {
                var stub = Workflow.NewChildWorkflowStub<IWorkflowWithMethodAttributes>();

                await stub.RunAsync();
            }

            public async Task ExecuteChildWithNoAttributes()
            {
                var stub = Workflow.NewChildWorkflowStub<IWorkflowWithNoAttributes>();

                await stub.RunAsync();
            }

            public async Task ExecuteChildWithOptions(ChildWorkflowOptions options)
            {
                var stub = Workflow.NewChildWorkflowStub<IWorkflowWithMethodAttributes>(options);

                await stub.RunAsync();
            }

            public async Task ExecuteActivityWithNoAttributes()
            {
                var stub = Workflow.NewActivityStub<IActivityWithNoAttributes>();

                await stub.RunAsync();
            }

            public async Task ExecuteActivityWithInterfaceAttributes()
            {
                var stub = Workflow.NewActivityStub<IActivityWithInterfaceAttributes>();

                await stub.RunAsync();
            }

            public async Task ExecuteActivityWithMethodAttributes()
            {
                var stub = Workflow.NewActivityStub<IActivityWithMethodAttributes>();

                await stub.RunAsync();
            }

            public async Task ExecuteActivityWithMethodAttributes(ActivityOptions options = null)
            {
                var stub = Workflow.NewActivityStub<IActivityWithNoAttributes>();

                await stub.RunAsync();
            }
        }

        [ActivityInterface]
        public interface IActivityWithNoAttributes : IActivity
        {
            [ActivityMethod]
            Task RunAsync();
        }

        [Activity(AutoRegister = true)]
        public class ActivityWithNoAttributes : ActivityBase, IActivityWithNoAttributes
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;
            }
        }

        [ActivityInterface(Namespace = "test2-namespace", TaskQueue = "test2-taskqueue")]
        public interface IActivityWithInterfaceAttributes : IActivity
        {
            [ActivityMethod]
            Task RunAsync();
        }

        [Activity(AutoRegister = true)]
        public class ActivityWithInterfaceAttributes : ActivityBase, IActivityWithInterfaceAttributes
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;
            }
        }

        [ActivityInterface(Namespace = "test2-namespace", TaskQueue = "test2-taskqueue")]
        public interface IActivityWithMethodAttributes : IActivity
        {
            [ActivityMethod(
                Namespace                     = "test1-namespace",
                TaskQueue                     = "test1-taskqueue",
                HeartbeatTimeoutSeconds       = 30,
                ScheduleToCloseTimeoutSeconds = 29,
                ScheduleToStartTimeoutSeconds = 28,
                StartToCloseTimeoutSeconds    = 27)]
            Task RunAsync();
        }

        [Activity(AutoRegister = true)]
        public class ActivityWithMethodAttributes : ActivityBase, IActivityWithMethodAttributes
        {
            public async Task RunAsync()
            {
                await Task.CompletedTask;
            }
        }

        //---------------------------------------------------------------------
        // Test helpers

        /// <summary>
        /// Temporarily hooks the <see cref="TemporalClient.WorkflowExecuteEvent"/> of the
        /// client passed and then executes the <see cref="IWorkflowWithNoAttributes.RunAsync()"/> 
        /// workflow.  The <paramref name="optionsChecker"/> function will be called with
        /// the <see cref="StartWorkflowOptions"/> received from the hook giving the function a
        /// chance to verify that the options are correct by returning <c>true</c>.
        /// </summary>
        /// <param name="client">The Temporal client.</param>
        /// <param name="optionsChecker">
        /// The options checker.  This should use <see cref="Assert.Equal(object, object)"/>
        /// to verify option correctness.
        /// </param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ExecuteWorkflowWithNoAttributesAsync(TemporalClient client, Action<StartWorkflowOptions> optionsChecker)
        {
            EventHandler<StartWorkflowOptions> hook =
                (object sender, StartWorkflowOptions options) =>
                {
                    optionsChecker(options);
                };

            client.WorkflowExecuteEvent += hook;

            try
            {
                var stub = client.NewWorkflowStub<IWorkflowWithNoAttributes>();

                await stub.RunAsync();
            }
            finally
            {
                client.WorkflowExecuteEvent -= hook;
            }
        }

        /// <summary>
        /// Temporarily hooks the <see cref="TemporalClient.WorkflowExecuteEvent"/> of the
        /// client passed and then executes the <see cref="IWorkflowWithInterfaceAttributes.RunAsync()"/> 
        /// workflow.  The <paramref name="optionsChecker"/> function will be called with
        /// the <see cref="StartWorkflowOptions"/> received from the hook giving the function a
        /// chance to verify that the options are correct by returning <c>true</c>.
        /// </summary>
        /// <param name="client">The Temporal client.</param>
        /// <param name="optionsChecker">
        /// The options checker.  This should use <see cref="Assert.Equal(object, object)"/>
        /// to verify option correctness.
        /// </param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ExecuteWorkflowWithInterfaceAttributesAsync(TemporalClient client, Action<StartWorkflowOptions> optionsChecker)
        {
            EventHandler<StartWorkflowOptions> hook =
                (object sender, StartWorkflowOptions options) =>
                {
                    optionsChecker(options);
                };

            client.WorkflowExecuteEvent += hook;

            try
            {
                var stub = client.NewWorkflowStub<IWorkflowWithInterfaceAttributes>();

                await stub.RunAsync();
            }
            finally
            {
                client.WorkflowExecuteEvent -= hook;
            }
        }

        /// <summary>
        /// Temporarily hooks the <see cref="TemporalClient.WorkflowExecuteEvent"/> of the
        /// client passed and then executes the <see cref="IWorkflowWithMethodAttributes.RunAsync()"/> 
        /// workflow.  The <paramref name="optionsChecker"/> function will be called with
        /// the <see cref="StartWorkflowOptions"/> received from the hook giving the function a
        /// chance to verify that the options are correct by returning <c>true</c>.
        /// </summary>
        /// <param name="client">The Temporal client.</param>
        /// <param name="optionsChecker">
        /// The options checker.  This should use <see cref="Assert.Equal(object, object)"/>
        /// to verify option correctness.
        /// </param>
        /// <param name="options">Optional workflow options that should override any other settings.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ExecuteWorkflowWithMethodAttributesAsync(TemporalClient client, Action<StartWorkflowOptions> optionsChecker, StartWorkflowOptions options = null)
        {
            EventHandler<StartWorkflowOptions> hook =
                (object sender, StartWorkflowOptions options) =>
                {
                    optionsChecker(options);
                };

            client.WorkflowExecuteEvent += hook;

            try
            {
                var stub = client.NewWorkflowStub<IWorkflowWithMethodAttributes>(options: options);

                await stub.RunAsync();
            }
            finally
            {
                client.WorkflowExecuteEvent -= hook;
            }
        }

        /// <summary>
        /// Temporarily hooks the <see cref="TemporalClient.ChildWorkflowExecuteEvent"/> of the
        /// client passed and then executes the <see cref="IOptionsTester.ExecuteChildWithNoAttributes()"/> 
        /// workflow.  The <paramref name="optionsChecker"/> function will be called with
        /// the <see cref="ChildWorkflowOptions"/> received from the hook giving the function a
        /// chance to verify that the options are correct by returning <c>true</c>.
        /// </summary>
        /// <param name="client">The Temporal client.</param>
        /// <param name="optionsChecker">
        /// The options checker.  This should use <see cref="Assert.Equal(object, object)"/>
        /// to verify option correctness.
        /// </param>
        /// <param name="parentNamespace">Optionally specifies the Namespace for the parent workflow.</param>
        /// <param name="parentTaskQueue">Optionally specifies the task queue for the parent workflow.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ExecuteChildWorkflowWithNoAttributesAsync(TemporalClient client, Action<ChildWorkflowOptions> optionsChecker, string parentNamespace = null, string parentTaskQueue = null)
        {
            EventHandler<ChildWorkflowOptions> hook =
                (object sender, ChildWorkflowOptions options) =>
                {
                    optionsChecker(options);
                };

            client.ChildWorkflowExecuteEvent += hook;

            try
            {
                var options = new StartWorkflowOptions()
                {
                    Namespace = parentNamespace,
                    TaskQueue = parentTaskQueue
                };

                var stub = client.NewWorkflowStub<IOptionsTester>(options);

                await stub.ExecuteChildWithNoAttributes();
            }
            finally
            {
                client.ChildWorkflowExecuteEvent -= hook;
            }
        }

        /// <summary>
        /// Temporarily hooks the <see cref="TemporalClient.ChildWorkflowExecuteEvent"/> of the
        /// client passed and then executes the <see cref="IOptionsTester.ExecuteChildWithInterfaceAttributes()"/> 
        /// workflow.  The <paramref name="optionsChecker"/> function will be called with
        /// the <see cref="ChildWorkflowOptions"/> received from the hook giving the function a
        /// chance to verify that the options are correct by returning <c>true</c>.
        /// </summary>
        /// <param name="client">The Temporal client.</param>
        /// <param name="optionsChecker">
        /// The options checker.  This should use <see cref="Assert.Equal(object, object)"/>
        /// to verify option correctness.
        /// </param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ExecuteChildWorkflowWithInterfaceAttributesAsync(TemporalClient client, Action<ChildWorkflowOptions> optionsChecker)
        {
            EventHandler<ChildWorkflowOptions> hook =
                (object sender, ChildWorkflowOptions options) =>
                {
                    optionsChecker(options);
                };

            client.ChildWorkflowExecuteEvent += hook;

            try
            {
                var stub = client.NewWorkflowStub<IOptionsTester>();

                await stub.ExecuteChildWithInterfaceAttributes();
            }
            finally
            {
                client.ChildWorkflowExecuteEvent -= hook;
            }
        }

        /// <summary>
        /// Temporarily hooks the <see cref="TemporalClient.ChildWorkflowExecuteEvent"/> of the
        /// client passed and then executes the <see cref="IOptionsTester.ExecuteChildWithMethodAttributes()"/> 
        /// workflow.  The <paramref name="optionsChecker"/> function will be called with
        /// the <see cref="ChildWorkflowOptions"/> received from the hook giving the function a
        /// chance to verify that the options are correct by returning <c>true</c>.
        /// </summary>
        /// <param name="client">The Temporal client.</param>
        /// <param name="optionsChecker">
        /// The options checker.  This should use <see cref="Assert.Equal(object, object)"/>
        /// to verify option correctness.
        /// </param>
        /// <param name="options">Optional workflow options that should override any other settings.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ExecuteChildWorkflowWithMethodAttributesAsync(TemporalClient client, Action<ChildWorkflowOptions> optionsChecker, StartWorkflowOptions options = null)
        {
            EventHandler<ChildWorkflowOptions> hook =
                (object sender, ChildWorkflowOptions options) =>
                {
                    optionsChecker(options);
                };

            client.ChildWorkflowExecuteEvent += hook;

            try
            {
                var stub = client.NewWorkflowStub<IOptionsTester>(options: options);

                await stub.ExecuteChildWithMethodAttributes();
            }
            finally
            {
                client.ChildWorkflowExecuteEvent -= hook;
            }
        }

        /// <summary>
        /// Temporarily hooks the <see cref="TemporalClient.ActivityExecuteEvent"/> of the
        /// client passed and then executes the <see cref="IActivityWithNoAttributes.ExecuteActivityWithNoAttributes()"/> 
        /// activity (via a workflow).  The <paramref name="optionsChecker"/> function will be called with
        /// the <see cref="ActivityOptions"/> received from the hook giving the function a
        /// chance to verify that the options are correct by returning <c>true</c>.
        /// </summary>
        /// <param name="client">The Temporal client.</param>
        /// <param name="optionsChecker">
        /// The options checker.  This should use <see cref="Assert.Equal(object, object)"/>
        /// to verify option correctness.
        /// </param>
        /// <param name="parentNamespace">Optionally specifies the Namespace for the parent workflow.</param>
        /// <param name="parentTaskQueue">Optionally specifies the task queue for the parent workflow.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ExecuteActivityWithNoAttributesAsync(TemporalClient client, Action<ActivityOptions> optionsChecker, string parentNamespace = null, string parentTaskQueue = null)
        {
            EventHandler<ActivityOptions> hook =
                (object sender, ActivityOptions options) =>
                {
                    optionsChecker(options);
                };

            client.ActivityExecuteEvent += hook;

            try
            {
                var options = new StartWorkflowOptions()
                {
                    Namespace = parentNamespace,
                    TaskQueue = parentTaskQueue
                };

                var stub = client.NewWorkflowStub<IOptionsTester>(options);

                await stub.ExecuteActivityWithNoAttributes();
            }
            finally
            {
                client.ActivityExecuteEvent -= hook;
            }
        }

        /// <summary>
        /// Temporarily hooks the <see cref="TemporalClient.ActivityExecuteEvent"/> of the
        /// client passed and then executes the <see cref="IActivityWithInterfaceAttributes.ExecuteActivityWithInterfaceAttributes()"/> 
        /// activity (via a workflow).  The <paramref name="optionsChecker"/> function will be called with
        /// the <see cref="ActivityOptions"/> received from the hook giving the function a
        /// chance to verify that the options are correct by returning <c>true</c>.
        /// </summary>
        /// <param name="client">The Temporal client.</param>
        /// <param name="optionsChecker">
        /// The options checker.  This should use <see cref="Assert.Equal(object, object)"/>
        /// to verify option correctness.
        /// </param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ExecuteActivityWithInterfaceAttributesAsync(TemporalClient client, Action<ActivityOptions> optionsChecker)
        {
            EventHandler<ActivityOptions> hook =
                (object sender, ActivityOptions options) =>
                {
                    optionsChecker(options);
                };

            client.ActivityExecuteEvent += hook;

            try
            {
                var stub = client.NewWorkflowStub<IOptionsTester>();

                await stub.ExecuteChildWithInterfaceAttributes();
            }
            finally
            {
                client.ActivityExecuteEvent -= hook;
            }
        }

        /// <summary>
        /// Temporarily hooks the <see cref="TemporalClient.ActivityExecuteEvent"/> of the
        /// client passed and then executes the <see cref="IActivityWithMethodAttributes.ExecuteActivityWithMethodAttributes()"/> 
        /// activity (via a workflow).  The <paramref name="optionsChecker"/> function will be called with
        /// the <see cref="ChildWorkflowOptions"/> received from the hook giving the function a
        /// chance to verify that the options are correct by returning <c>true</c>.
        /// </summary>
        /// <param name="client">The Temporal client.</param>
        /// <param name="optionsChecker">
        /// The options checker.  This should use <see cref="Assert.Equal(object, object)"/>
        /// to verify option correctness.
        /// </param>
        /// <param name="options">Optional activity options that should override any other settings.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task ExecuteActivityWithMethodAttributesAsync(TemporalClient client, Action<ActivityOptions> optionsChecker, ActivityOptions options = null)
        {
            EventHandler<ActivityOptions> hook =
                (object sender, ActivityOptions options) =>
                {
                    optionsChecker(options);
                };

            client.ActivityExecuteEvent += hook;

            try
            {
                var stub = client.NewWorkflowStub<IOptionsTester>();

                await stub.ExecuteActivityWithMethodAttributes(options);
            }
            finally
            {
                client.ActivityExecuteEvent -= hook;
            }
        }

        //---------------------------------------------------------------------
        // External workflow tests

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task Workflow_UseClientSettings()
        {
            // Verify that the [test1Client] settings are honored for workflows
            // with no attributes.

            await ExecuteWorkflowWithNoAttributesAsync(test1Client,
                options =>
                {
                    Assert.Equal(test1Settings.Namespace, options.Namespace);
                    Assert.Equal(test1Settings.TaskQueue, options.TaskQueue);
                    Assert.Equal(test1Settings.WorkflowTaskTimeout, options.WorkflowTaskTimeout);
                    Assert.Equal(test1Settings.WorkflowExecutionTimeout, options.WorkflowExecutionTimeout);
                    Assert.Equal(test1Settings.WorkflowRunTimeout, options.WorkflowRunTimeout);
                    Assert.Equal(test1Settings.WorkflowIdReusePolicy, options.WorkflowIdReusePolicy);
                });

            // For kicks, we're going to do the same for [test2Client].

            await ExecuteWorkflowWithNoAttributesAsync(test2Client,
                options =>
                {
                    Assert.Equal(test2Settings.Namespace, options.Namespace);
                    Assert.Equal(test2Settings.TaskQueue, options.TaskQueue);
                    Assert.Equal(test2Settings.WorkflowTaskTimeout, options.WorkflowTaskTimeout);
                    Assert.Equal(test2Settings.WorkflowExecutionTimeout, options.WorkflowExecutionTimeout);
                    Assert.Equal(test2Settings.WorkflowRunTimeout, options.WorkflowExecutionTimeout);
                    Assert.Equal(test2Settings.WorkflowIdReusePolicy, options.WorkflowIdReusePolicy);
                });
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task Workflow_UseInterfaceAttributes()
        {
            // Verify that workflow interface attributes are honored.

            await ExecuteWorkflowWithInterfaceAttributesAsync(test1Client,
                options =>
                {
                    Assert.Equal("test2-namespace", options.Namespace);
                    Assert.Equal("test2-taskqueue", options.TaskQueue);
                });
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task Workflow_UseMethodAttributes()
        {
            // Verify that workflow interface method attributes are honored.

            await ExecuteWorkflowWithMethodAttributesAsync(test3Client,
                options =>
                {
                    Assert.Equal("test1-namespace", options.Namespace);
                    Assert.Equal("test1-taskqueue", options.TaskQueue);
                    Assert.Equal(55, options.WorkflowTaskTimeout.TotalSeconds);
                    Assert.Equal(56, options.WorkflowRunTimeout.TotalSeconds);
                    Assert.Equal(57, options.WorkflowExecutionTimeout.TotalSeconds);
                    Assert.Equal(WorkflowIdReusePolicy.RejectDuplicate, options.WorkflowIdReusePolicy);
                });
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task Workflow_UseWorkflowOptions()
        {
            // Verify that workflow options are honored.

            var workflowOptions = new StartWorkflowOptions()
            {
                Namespace              = "test1-namespace",
                TaskQueue              = "test1-taskqueue",
                WorkflowTaskTimeout    = TimeSpan.FromSeconds(40),
                WorkflowRunTimeout = TimeSpan.FromSeconds(41),
                WorkflowExecutionTimeout    = TimeSpan.FromSeconds(42),
                WorkflowIdReusePolicy  = WorkflowIdReusePolicy.AllowDuplicate
            };

            await ExecuteWorkflowWithMethodAttributesAsync(test3Client,
                options =>
                {
                    Assert.Equal("test1-namespace", options.Namespace);
                    Assert.Equal("test1-taskqueue", options.TaskQueue);
                    Assert.Equal(40, options.WorkflowTaskTimeout.TotalSeconds);
                    Assert.Equal(41, options.WorkflowRunTimeout.TotalSeconds);
                    Assert.Equal(42, options.WorkflowExecutionTimeout.TotalSeconds);
                    Assert.Equal(WorkflowIdReusePolicy.AllowDuplicate, options.WorkflowIdReusePolicy);
                },
                options: workflowOptions);
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task Workflow_OtherNamespaceTaskQueue()
        {
            // Verify that we can call a workflow whose worker is running in
            // a different Namespace and task queue from the defaults set for
            // the client.

            await ExecuteWorkflowWithInterfaceAttributesAsync(test1Client,
                options =>
                {
                    Assert.Equal("test2-namespace", options.Namespace);
                    Assert.Equal("test2-taskqueue", options.TaskQueue);
                });
        }

        //---------------------------------------------------------------------
        // Child workflow tests

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task ChildWorkflow_UseClientSettings()
        {
            // Verify that the [test1Client] settings are honored for child workflows
            // with no attributes.

            await ExecuteChildWorkflowWithNoAttributesAsync(test1Client,
                options =>
                {
                    Assert.Equal(test1Settings.Namespace, options.Namespace);
                    Assert.Equal(test1Settings.TaskQueue, options.TaskQueue);
                    Assert.Equal(test1Settings.WorkflowTaskTimeout, options.WorkflowTaskTimeout);
                    Assert.Equal(test1Settings.WorkflowExecutionTimeout, options.WorkflowExecutionTimeout);
                    Assert.Equal(test1Settings.WorkflowRunTimeout, options.WorkflowRunTimeout);
                    Assert.Equal(test1Settings.WorkflowIdReusePolicy, options.WorkflowIdReusePolicy);
                });

            // For kicks, we're going to do the same for [test2Client].

            await ExecuteChildWorkflowWithNoAttributesAsync(test2Client,
                options =>
                {
                    Assert.Equal(test2Settings.Namespace, options.Namespace);
                    Assert.Equal(test2Settings.TaskQueue, options.TaskQueue);
                    Assert.Equal(test2Settings.WorkflowTaskTimeout, options.WorkflowTaskTimeout);
                    Assert.Equal(test2Settings.WorkflowExecutionTimeout, options.WorkflowExecutionTimeout);
                    Assert.Equal(test2Settings.WorkflowRunTimeout, options.WorkflowRunTimeout);
                    Assert.Equal(test2Settings.WorkflowIdReusePolicy, options.WorkflowIdReusePolicy);
                });
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task ChildWorkflow_UseParentSettings()
        {
            // Verify that child workflows will default to the parent workflow's
            // Namespace and task queue.

            await ExecuteChildWorkflowWithNoAttributesAsync(fixtureClient,
                options =>
                {
                    Assert.Equal("test2-namespace", options.Namespace);
                    Assert.Equal("test2-taskqueue", options.TaskQueue);
                },
                parentNamespace:   "test2-namespace",
                parentTaskQueue: "test2-taskqueue");
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task ChildWorkflow_UseInterfaceAttributes()
        {
            // Verify that workflow interface attributes are honored for child workflows.

            await ExecuteChildWorkflowWithInterfaceAttributesAsync(test1Client,
                options =>
                {
                    Assert.Equal("test2-namespace", options.Namespace);
                    Assert.Equal("test2-taskqueue", options.TaskQueue);
                });
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task ChildWorkflow_UseMethodAttributes()
        {
            // Verify that workflow interface method attributes are honored for child workflows.

            await ExecuteChildWorkflowWithMethodAttributesAsync(test3Client,
                options =>
                {
                    Assert.Equal("test1-namespace", options.Namespace);
                    Assert.Equal("test1-taskqueue", options.TaskQueue);
                    Assert.Equal(55, options.WorkflowTaskTimeout.TotalSeconds);
                    Assert.Equal(56, options.WorkflowRunTimeout.TotalSeconds);
                    Assert.Equal(57, options.WorkflowExecutionTimeout.TotalSeconds);
                    Assert.Equal(WorkflowIdReusePolicy.RejectDuplicate, options.WorkflowIdReusePolicy);
                });
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task ChildWorkflow_UseWorkflowOptions()
        {
            // Verify that workflow options are honored for child workflows.

            var workflowOptions = new StartWorkflowOptions()
            {
                Namespace                = "test1-namespace",
                TaskQueue                = "test1-taskqueue",
                WorkflowTaskTimeout      = TimeSpan.FromSeconds(40),
                WorkflowRunTimeout       = TimeSpan.FromSeconds(41),
                WorkflowExecutionTimeout = TimeSpan.FromSeconds(42),
                WorkflowIdReusePolicy    = WorkflowIdReusePolicy.AllowDuplicate
            };

            await ExecuteChildWorkflowWithMethodAttributesAsync(test3Client,
                options =>
                {
                    Assert.Equal("test1-namespace", options.Namespace);
                    Assert.Equal("test1-taskqueue", options.TaskQueue);
                    Assert.Equal(40, options.WorkflowTaskTimeout.TotalSeconds);
                    Assert.Equal(41, options.WorkflowRunTimeout.TotalSeconds);
                    Assert.Equal(42, options.WorkflowExecutionTimeout.TotalSeconds);
                    Assert.Equal(WorkflowIdReusePolicy.AllowDuplicate, options.WorkflowIdReusePolicy);
                },
                options: workflowOptions);
        }

        //---------------------------------------------------------------------
        // Activity options tests

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task Activity_UseClientSettings()
        {
            // Verify that the [test1Client] settings are honored for child workflows
            // with no attributes.

            await ExecuteActivityWithNoAttributesAsync(test1Client,
                options =>
                {
                    Assert.Equal(test1Settings.Namespace, options.Namespace);
                    Assert.Equal(test1Settings.TaskQueue, options.TaskQueue);
                    Assert.Equal(test1Settings.ActivityScheduleToCloseTimeout, options.ScheduleToCloseTimeout);
                    Assert.Equal(test1Settings.ActivityStartToCloseTimeout, options.StartToCloseTimeout);
                    Assert.Equal(test1Settings.ActivityScheduleToStartTimeout, options.ScheduleToStartTimeout);
                });

            // For kicks, we're going to do the same for [test2Client].

            await ExecuteActivityWithNoAttributesAsync(test2Client,
                options =>
                {
                    Assert.Equal(test2Settings.Namespace, options.Namespace);
                    Assert.Equal(test2Settings.TaskQueue, options.TaskQueue);
                    Assert.Equal(test2Settings.ActivityScheduleToCloseTimeout, options.ScheduleToCloseTimeout);
                    Assert.Equal(test2Settings.ActivityStartToCloseTimeout, options.StartToCloseTimeout);
                    Assert.Equal(test2Settings.ActivityScheduleToStartTimeout, options.ScheduleToStartTimeout);
                });
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task Activity_UseParentSettings()
        {
            // Verify that activities will default to the parent workflow's
            // Namespace and task queue.

            await ExecuteActivityWithNoAttributesAsync(fixtureClient,
                options =>
                {
                    Assert.Equal("test2-namespace", options.Namespace);
                    Assert.Equal("test2-taskqueue", options.TaskQueue);
                },
                parentNamespace: "test2-namespace",
                parentTaskQueue: "test2-taskqueue");
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task Activity_UseInterfaceAttributes()
        {
            // Verify that activity interface attributes are honored.

            await ExecuteActivityWithInterfaceAttributesAsync(test1Client,
                options =>
                {
                    Assert.Equal("test2-namespace", options.Namespace);
                    Assert.Equal("test2-taskqueue", options.TaskQueue);
                });
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task Activity_UseMethodAttributes()
        {
            // Verify that activity interface method attributes are honored.

            await ExecuteActivityWithMethodAttributesAsync(test3Client,
                options =>
                {
                    Assert.Equal("test1-namespace", options.Namespace);
                    Assert.Equal("test1-taskqueue", options.TaskQueue);
                    Assert.Equal(30, options.HeartbeatTimeout.TotalSeconds);
                    Assert.Equal(29, options.ScheduleToCloseTimeout.TotalSeconds);
                    Assert.Equal(28, options.ScheduleToStartTimeout.TotalSeconds);
                    Assert.Equal(27, options.StartToCloseTimeout.TotalSeconds);
                });
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task Activity_UseWorkflowOptions()
        {
            // Verify that activity options are honored.

            var activityOptions = new ActivityOptions()
            {
                Namespace              = "test1-namespace",
                TaskQueue              = "test1-taskqueue",
                HeartbeatTimeout       = TimeSpan.FromSeconds(25),
                ScheduleToCloseTimeout = TimeSpan.FromSeconds(24),
                ScheduleToStartTimeout = TimeSpan.FromSeconds(23),
                StartToCloseTimeout    = TimeSpan.FromSeconds(22)
            };

            await ExecuteActivityWithMethodAttributesAsync(test3Client,
                options =>
                {
                    Assert.Equal("test1-namespace", options.Namespace);
                    Assert.Equal("test1-taskqueue", options.TaskQueue);
                    Assert.Equal(25, options.HeartbeatTimeout.TotalSeconds);
                    Assert.Equal(24, options.ScheduleToCloseTimeout.TotalSeconds);
                    Assert.Equal(23, options.ScheduleToStartTimeout.TotalSeconds);
                    Assert.Equal(22, options.StartToCloseTimeout.TotalSeconds);
                },
                options: activityOptions);
        }

        //---------------------------------------------------------------------
        // The tests below verify that we can wait on external workflows directly
        // or from a child workflow and that this works against workflows running
        // in a different namespace.
        //
        // These aren't exactly options tests but we'll implement them here
        // because we have a few client instances to play with.

        [WorkflowInterface]
        public interface IWorkflowExternalWait : IWorkflow
        {
            [WorkflowMethod(Name = "HelloAsync")]
            Task<string> HelloAsync(string name);

            [WorkflowMethod(Name = "WaitByExecutionAsync")]
            Task<string> WaitByExecutionAsync(WorkflowExecution execution);

            [WorkflowMethod(Name = "WaitByIdsAsync")]
            Task<string> WaitByIdsAsync(string workflowId, string runId);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowExternalWait : WorkflowBase, IWorkflowExternalWait
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }

            public async Task<string> WaitByExecutionAsync(WorkflowExecution execution)
            {
                var stub = Workflow.Client.NewUntypedWorkflowStub(execution);

                return await stub.GetResultAsync<string>();
            }

            public async Task<string> WaitByIdsAsync(string workflowId, string runId)
            {
                var stub = Workflow.Client.NewUntypedWorkflowStub(workflowId, runId);

                return await stub.GetResultAsync<string>();
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task WorkflowWaitForExternalByExecution()
        {
            // Verify that a workflow can wait on an external workflow
            // running in a different namespace by execution.

            var options = new StartWorkflowOptions() { TaskQueue = "test1-taskqueue", Namespace = "test1-namespace" };
            var helloStub = test3Client.NewWorkflowFutureStub<IWorkflowExternalWait>("HelloAsync", options);
            var helloFuture = await helloStub.StartAsync<string>("JEFF");

            Assert.Equal("Hello JEFF!", await helloFuture.GetAsync());

            var execution = helloFuture.Execution;
            var waitStub = test2Client.NewWorkflowStub<IWorkflowExternalWait>(options);

            Assert.Equal("Hello JEFF!", await waitStub.WaitByExecutionAsync(execution));
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task WorkflowWaitForExternalByIds()
        {
            // Verify that a workflow can wait on an external workflow
            // running in a different namespace by both workflow IDs.

            var options = new StartWorkflowOptions() { TaskQueue = "test1-taskqueue", Namespace = "test1-namespace" };
            var helloStub = test3Client.NewWorkflowFutureStub<IWorkflowExternalWait>("HelloAsync", options);
            var helloFuture = await helloStub.StartAsync<string>("JEFF");

            Assert.Equal("Hello JEFF!", await helloFuture.GetAsync());

            var execution = helloFuture.Execution;
            var waitStub = test2Client.NewWorkflowStub<IWorkflowExternalWait>(options);

            Assert.Equal("Hello JEFF!", await waitStub.WaitByIdsAsync(workflowId: execution.WorkflowId, runId: execution.RunId));
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonTemporal)]
        public async Task WorkflowWaitForExternalByWorkflowId()
        {
            // Verify that a workflow can wait on an external workflow
            // running in a different namespace by both workflow ID only.

            var options = new StartWorkflowOptions() { TaskQueue = "test1-taskqueue", Namespace = "test1-namespace" };
            var helloStub = test3Client.NewWorkflowFutureStub<IWorkflowExternalWait>("HelloAsync", options);
            var helloFuture = await helloStub.StartAsync<string>("JEFF");

            Assert.Equal("Hello JEFF!", await helloFuture.GetAsync());

            var execution = helloFuture.Execution;
            var waitStub = test2Client.NewWorkflowStub<IWorkflowExternalWait>(options);

            Assert.Equal("Hello JEFF!", await waitStub.WaitByIdsAsync(workflowId: execution.WorkflowId, runId: null));
        }
    }
}
