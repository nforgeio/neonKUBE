//-----------------------------------------------------------------------------
// FILE:        Test_Messages.Workflow.cs
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
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Data;
using Neon.IO;
using Neon.Temporal;
using Neon.Temporal.Internal;
using Neon.Time;
using Neon.Xunit;
using Neon.Xunit.Temporal;

using Newtonsoft.Json;
using Test.Neon.Models;
using Xunit;

namespace TestTemporal
{
    public partial class Test_Messages
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowRegisterRequest()
        {
            WorkflowRegisterRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowRegisterRequest();

                Assert.Equal(InternalMessageTypes.WorkflowRegisterReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowRegisterRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.WorkerId);
                Assert.Null(message.Name);
                Assert.False(message.DisableAlreadyRegisteredCheck);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.WorkerId = 666;
                message.Name = "Foo";
                message.DisableAlreadyRegisteredCheck = true;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);
                Assert.Equal("Foo", message.Name);
                Assert.True(message.DisableAlreadyRegisteredCheck);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowRegisterRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);
                Assert.Equal("Foo", message.Name);
                Assert.True(message.DisableAlreadyRegisteredCheck);

                // Clone()

                message = (WorkflowRegisterRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);
                Assert.Equal("Foo", message.Name);
                Assert.True(message.DisableAlreadyRegisteredCheck);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);
                Assert.Equal("Foo", message.Name);
                Assert.True(message.DisableAlreadyRegisteredCheck);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowRegisterReply()
        {
            WorkflowRegisterReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowRegisterReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowRegisterReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowRegisterReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (WorkflowRegisterReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowExecuteRequest()
        {
            WorkflowExecuteRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowExecuteRequest();

                Assert.Equal(InternalMessageTypes.WorkflowExecuteReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowExecuteRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ContextId);
                Assert.Null(message.Namespace);
                Assert.Null(message.Workflow);
                Assert.Null(message.Args);
                Assert.Null(message.Options);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.ContextId = 666;
                message.Namespace = "my-namespace";
                message.Workflow = "Foo";
                message.Args = new byte[] { 0, 1, 2, 3, 4 };
                message.Options = new WorkflowOptions() { TaskList = "my-list", StartToCloseTimeout = TimeSpan.FromSeconds(100) };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("Foo", message.Workflow);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-list", message.Options.TaskList);
                Assert.Equal(TimeSpan.FromSeconds(100), message.Options.StartToCloseTimeout);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowExecuteRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("Foo", message.Workflow);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-list", message.Options.TaskList);
                Assert.Equal(TimeSpan.FromSeconds(100), message.Options.StartToCloseTimeout);

                // Clone()

                message = (WorkflowExecuteRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("Foo", message.Workflow);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-list", message.Options.TaskList);
                Assert.Equal(TimeSpan.FromSeconds(100), message.Options.StartToCloseTimeout);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("Foo", message.Workflow);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-list", message.Options.TaskList);
                Assert.Equal(TimeSpan.FromSeconds(100), message.Options.StartToCloseTimeout);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowExecuteReply()
        {
            WorkflowExecuteReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowExecuteReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowExecuteReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Execution);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Execution = new WorkflowExecution("foo", "bar");
                message.Error = new TemporalError("MyError");

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("foo", message.Execution.WorkflowId);
                Assert.Equal("bar", message.Execution.RunId);
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowExecuteReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("foo", message.Execution.WorkflowId);
                Assert.Equal("bar", message.Execution.RunId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (WorkflowExecuteReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("foo", message.Execution.WorkflowId);
                Assert.Equal("bar", message.Execution.RunId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("foo", message.Execution.WorkflowId);
                Assert.Equal("bar", message.Execution.RunId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowInvokeRequest()
        {
            WorkflowInvokeRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowInvokeRequest();

                Assert.Equal(InternalMessageTypes.WorkflowInvokeReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowInvokeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ContextId);
                Assert.Equal(InternalReplayStatus.Unspecified, message.ReplayStatus);
                Assert.Null(message.Name);
                Assert.Null(message.Args);
                Assert.Null(message.Namespace);
                Assert.Null(message.WorkflowId);
                Assert.Null(message.RunId);
                Assert.Null(message.WorkflowType);
                Assert.Null(message.TaskList);
                Assert.Equal(TimeSpan.Zero, message.ExecutionStartToCloseTimeout);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.ContextId = 666;
                message.ReplayStatus = InternalReplayStatus.Replaying;
                message.Name = "Foo";
                message.Args = new byte[] { 0, 1, 2, 3, 4 };
                message.Namespace = "my-namespace";
                message.WorkflowId = "my-workflowid";
                message.RunId = "my-runid";
                message.WorkflowType = "my-workflowtype";
                message.ExecutionStartToCloseTimeout = TimeSpan.FromDays(1);

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(InternalReplayStatus.Replaying, message.ReplayStatus);
                Assert.Equal("Foo", message.Name);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-workflowid", message.WorkflowId);
                Assert.Equal("my-runid", message.RunId);
                Assert.Equal("my-workflowtype", message.WorkflowType);
                Assert.Equal(TimeSpan.FromDays(1), message.ExecutionStartToCloseTimeout);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowInvokeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                message.ReplayStatus = InternalReplayStatus.Replaying;
                Assert.Equal("Foo", message.Name);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-workflowid", message.WorkflowId);
                Assert.Equal("my-runid", message.RunId);
                Assert.Equal("my-workflowtype", message.WorkflowType);
                Assert.Equal(TimeSpan.FromDays(1), message.ExecutionStartToCloseTimeout);

                // Clone()

                message = (WorkflowInvokeRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                message.ReplayStatus = InternalReplayStatus.Replaying;
                Assert.Equal("Foo", message.Name);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-workflowid", message.WorkflowId);
                Assert.Equal("my-runid", message.RunId);
                Assert.Equal("my-workflowtype", message.WorkflowType);
                Assert.Equal(TimeSpan.FromDays(1), message.ExecutionStartToCloseTimeout);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                message.ReplayStatus = InternalReplayStatus.Replaying;
                Assert.Equal("Foo", message.Name);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-workflowid", message.WorkflowId);
                Assert.Equal("my-runid", message.RunId);
                Assert.Equal("my-workflowtype", message.WorkflowType);
                Assert.Equal(TimeSpan.FromDays(1), message.ExecutionStartToCloseTimeout);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowInvokeReply()
        {
            WorkflowInvokeReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowInvokeReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowInvokeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Result);
                Assert.Null(message.Error);
                Assert.False(message.ContinueAsNew);
                Assert.Null(message.ContinueAsNewArgs);
                Assert.Equal(0, message.ContinueAsNewExecutionStartToCloseTimeout);
                Assert.Equal(0, message.ContinueAsNewScheduleToCloseTimeout);
                Assert.Equal(0, message.ContinueAsNewScheduleToStartTimeout);
                Assert.Equal(0, message.ContinueAsNewStartToCloseTimeout);
                Assert.Null(message.ContinueAsNewWorkflow);
                Assert.Null(message.ContinueAsNewTaskList);
                Assert.Null(message.ContinueAsNewNamespace);
                Assert.False(message.ForceReplay);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");
                message.Result = new byte[] { 0, 1, 2, 3, 4 };
                message.ContinueAsNew = true;
                message.ContinueAsNewArgs = new byte[] { 5, 6, 7, 8, 9 };
                message.ContinueAsNewExecutionStartToCloseTimeout = 1000;
                message.ContinueAsNewScheduleToCloseTimeout = 2000;
                message.ContinueAsNewScheduleToStartTimeout = 3000;
                message.ContinueAsNewStartToCloseTimeout = 4000;
                message.ContinueAsNewWorkflow = "my-workflow";
                message.ContinueAsNewTaskList = "my-tasklist";
                message.ContinueAsNewNamespace = "my-namespace";
                message.ForceReplay = true;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
                Assert.True(message.ContinueAsNew);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.ContinueAsNewArgs);
                Assert.Equal(1000, message.ContinueAsNewExecutionStartToCloseTimeout);
                Assert.Equal("my-workflow", message.ContinueAsNewWorkflow);
                Assert.Equal("my-tasklist", message.ContinueAsNewTaskList);
                Assert.Equal("my-namespace", message.ContinueAsNewNamespace);
                Assert.True(message.ForceReplay);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowInvokeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
                Assert.True(message.ContinueAsNew);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.ContinueAsNewArgs);
                Assert.Equal(1000, message.ContinueAsNewExecutionStartToCloseTimeout);
                Assert.Equal(2000, message.ContinueAsNewScheduleToCloseTimeout);
                Assert.Equal(3000, message.ContinueAsNewScheduleToStartTimeout);
                Assert.Equal(4000, message.ContinueAsNewStartToCloseTimeout);
                Assert.Equal("my-workflow", message.ContinueAsNewWorkflow);
                Assert.Equal("my-tasklist", message.ContinueAsNewTaskList);
                Assert.Equal("my-namespace", message.ContinueAsNewNamespace);
                Assert.True(message.ForceReplay);

                // Clone()

                message = (WorkflowInvokeReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
                Assert.True(message.ContinueAsNew);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.ContinueAsNewArgs);
                Assert.Equal(1000, message.ContinueAsNewExecutionStartToCloseTimeout);
                Assert.Equal(2000, message.ContinueAsNewScheduleToCloseTimeout);
                Assert.Equal(3000, message.ContinueAsNewScheduleToStartTimeout);
                Assert.Equal(4000, message.ContinueAsNewStartToCloseTimeout);
                Assert.Equal("my-workflow", message.ContinueAsNewWorkflow);
                Assert.Equal("my-tasklist", message.ContinueAsNewTaskList);
                Assert.Equal("my-namespace", message.ContinueAsNewNamespace);
                Assert.True(message.ForceReplay);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
                Assert.True(message.ContinueAsNew);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.ContinueAsNewArgs);
                Assert.Equal(1000, message.ContinueAsNewExecutionStartToCloseTimeout);
                Assert.Equal(2000, message.ContinueAsNewScheduleToCloseTimeout);
                Assert.Equal(3000, message.ContinueAsNewScheduleToStartTimeout);
                Assert.Equal(4000, message.ContinueAsNewStartToCloseTimeout);
                Assert.Equal("my-workflow", message.ContinueAsNewWorkflow);
                Assert.Equal("my-tasklist", message.ContinueAsNewTaskList);
                Assert.Equal("my-namespace", message.ContinueAsNewNamespace);
                Assert.True(message.ForceReplay);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowCancelRequest()
        {
            WorkflowCancelRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowCancelRequest();

                Assert.Equal(InternalMessageTypes.WorkflowCancelReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowCancelRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.WorkflowId);
                Assert.Null(message.RunId);
                Assert.Null(message.Namespace);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.WorkflowId = "666";
                message.RunId = "777";
                message.Namespace = "my-namespace";

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowCancelRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);

                // Clone()

                message = (WorkflowCancelRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowCancelReply()
        {
            WorkflowCancelReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowCancelReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowCancelReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowCancelReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (WorkflowCancelReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowTerminateRequest()
        {
            WorkflowTerminateRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowTerminateRequest();

                Assert.Equal(InternalMessageTypes.WorkflowTerminateReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowTerminateRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.WorkflowId);
                Assert.Null(message.RunId);
                Assert.Null(message.Namespace);
                Assert.Null(message.Reason);
                Assert.Null(message.Details);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.WorkflowId = "666";
                message.RunId = "777";
                message.Namespace = "my-namespace";
                message.Reason = "my-reason";
                message.Details = new byte[] { 0, 1, 2, 3, 4 };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-reason", message.Reason);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Details);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowTerminateRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-reason", message.Reason);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Details);

                // Clone()

                message = (WorkflowTerminateRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-reason", message.Reason);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Details);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-reason", message.Reason);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Details);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowTerminateReply()
        {
            WorkflowTerminateReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowTerminateReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowTerminateReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowTerminateReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (WorkflowTerminateReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowSignalRequest()
        {
            WorkflowSignalRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowSignalRequest();

                Assert.Equal(InternalMessageTypes.WorkflowSignalReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.WorkflowId);
                Assert.Null(message.RunId);
                Assert.Null(message.Namespace);
                Assert.Null(message.SignalName);
                Assert.Null(message.SignalArgs);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.WorkflowId = "666";
                message.RunId = "777";
                message.Namespace = "my-namespace";
                message.SignalName = "my-signal";
                message.SignalArgs = new byte[] { 0, 1, 2, 3, 4 };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);

                // Clone()

                message = (WorkflowSignalRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowSignalReply()
        {
            WorkflowSignalReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowSignalReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (WorkflowSignalReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowSignalWithStartRequest()
        {
            WorkflowSignalWithStartRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowSignalWithStartRequest();

                Assert.Equal(InternalMessageTypes.WorkflowSignalWithStartReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalWithStartRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Workflow);
                Assert.Null(message.WorkflowId);
                Assert.Null(message.Namespace);
                Assert.Null(message.SignalName);
                Assert.Null(message.SignalArgs);
                Assert.Null(message.Options);
                Assert.Null(message.WorkflowArgs);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Workflow = "my-workflow";
                message.WorkflowId = "666";
                message.Namespace = "my-namespace";
                message.SignalName = "my-signal";
                message.SignalArgs = new byte[] { 0, 1, 2, 3, 4 };
                message.Options = new WorkflowOptions() { TaskList = "my-tasklist", WorkflowIdReusePolicy = WorkflowIdReusePolicy.AllowDuplicate };
                message.WorkflowArgs = new byte[] { 5, 6, 7, 8, 9 };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-workflow", message.Workflow);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);
                Assert.Equal("my-tasklist", message.Options.TaskList);
                Assert.Equal(WorkflowIdReusePolicy.AllowDuplicate, message.Options.WorkflowIdReusePolicy);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.WorkflowArgs);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalWithStartRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-workflow", message.Workflow);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);
                Assert.Equal("my-tasklist", message.Options.TaskList);
                Assert.Equal(WorkflowIdReusePolicy.AllowDuplicate, message.Options.WorkflowIdReusePolicy);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.WorkflowArgs);

                // Clone()

                message = (WorkflowSignalWithStartRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-workflow", message.Workflow);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);
                Assert.Equal("my-tasklist", message.Options.TaskList);
                Assert.Equal(WorkflowIdReusePolicy.AllowDuplicate, message.Options.WorkflowIdReusePolicy);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.WorkflowArgs);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-workflow", message.Workflow);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);
                Assert.Equal("my-tasklist", message.Options.TaskList);
                Assert.Equal(WorkflowIdReusePolicy.AllowDuplicate, message.Options.WorkflowIdReusePolicy);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.WorkflowArgs);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowSignalWithStartReply()
        {
            WorkflowSignalWithStartReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowSignalWithStartReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalWithStartReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Execution);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");
                message.Execution = new WorkflowExecution("666", "777");

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal("666", message.Execution.WorkflowId);
                Assert.Equal("777", message.Execution.RunId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalWithStartReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal("666", message.Execution.WorkflowId);
                Assert.Equal("777", message.Execution.RunId);

                // Clone()

                message = (WorkflowSignalWithStartReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal("666", message.Execution.WorkflowId);
                Assert.Equal("777", message.Execution.RunId);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal("666", message.Execution.WorkflowId);
                Assert.Equal("777", message.Execution.RunId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowQueryRequest()
        {
            WorkflowQueryRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowQueryRequest();

                Assert.Equal(InternalMessageTypes.WorkflowQueryReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueryRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.WorkflowId);
                Assert.Null(message.RunId);
                Assert.Null(message.Namespace);
                Assert.Null(message.QueryName);
                Assert.Null(message.QueryArgs);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.WorkflowId = "666";
                message.RunId = "777";
                message.Namespace = "my-namespace";
                message.QueryName = "my-query";
                message.QueryArgs = new byte[] { 0, 1, 2, 3, 4 };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-query", message.QueryName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.QueryArgs);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueryRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-query", message.QueryName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.QueryArgs);

                // Clone()

                message = (WorkflowQueryRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-query", message.QueryName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.QueryArgs);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.Equal("my-query", message.QueryName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.QueryArgs);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowQueryReply()
        {
            WorkflowQueryReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowQueryReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueryReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Result);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");
                message.Result = new byte[] { 0, 1, 2, 3, 4 };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueryReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Clone()

                message = (WorkflowQueryReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        [Obsolete("This was replaced by a local activity.")]
        public void Test_WorkflowMutableRequest()
        {
            WorkflowMutableRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowMutableRequest();

                Assert.Equal(InternalMessageTypes.WorkflowMutableReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowMutableRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ContextId);
                Assert.Null(message.MutableId);
                Assert.Null(message.Result);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.ContextId = 666;
                message.MutableId = "my-mutable";
                message.Result = new byte[] { 0, 1, 2, 3, 4 };

                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-mutable", message.MutableId);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowMutableRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-mutable", message.MutableId);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Clone()

                message = (WorkflowMutableRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-mutable", message.MutableId);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-mutable", message.MutableId);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        [Obsolete("This was replaced by a local activity.")]
        public void Test_WorkflowMutableReply()
        {
            WorkflowMutableReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowMutableReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowMutableReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Result);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");
                message.ContextId = 666;
                message.Result = new byte[] { 0, 1, 2, 3, 4 };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowMutableReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Clone()

                message = (WorkflowMutableReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowDescribeExecutionRequest()
        {
            WorkflowDescribeExecutionRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowDescribeExecutionRequest();

                Assert.Equal(InternalMessageTypes.WorkflowDescribeExecutionReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowDescribeExecutionRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.WorkflowId);
                Assert.Null(message.RunId);
                Assert.Null(message.Namespace);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.WorkflowId = "666";
                message.RunId = "777";
                message.Namespace = "my-namespace";

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowDescribeExecutionRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);

                // Clone()

                message = (WorkflowDescribeExecutionRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);
            }
        }

        /// <summary>
        /// Returns a test <see cref="WorkflowDescription"/>.
        /// </summary>
        private WorkflowDescription GetWorkflowDescription()
        {
            var description = new WorkflowDescription()
            {
                Configuration = new WorkflowConfiguration()
                {
                    TaskList                = "my-tasklist",
                    TaskListKind            = TaskListType.Activity,
                    StartToCloseTimeout     = TimeSpan.FromSeconds(1),
                    TaskStartToCloseTimeout = TimeSpan.FromSeconds(2),
                    ParentClosePolicy       = ParentClosePolicy.RequestCancel
                },

                ExeecutionInfo = new WorkflowExecutionInfo()
                {
                    Execution       = new WorkflowExecution("workflow-id", "run-id"),
                    TypeName        = "my-workflow",
                    StartTime       = new DateTime(2020, 5, 23, 13, 21, 0),
                    CloseTime       = new DateTime(2020, 5, 23, 13, 22, 0),
                    CloseStatus     = WorkflowCloseStatus.ContinuedAsNew,
                    HistoryLength   = 5000,
                    ParentNamespace = "parent-namespace",
                    ParentExecution = new WorkflowExecution("parent-id", "parent-runid"),
                    ExecutionTime   = TimeSpan.FromSeconds(6000),
                    Memo            = new Dictionary<string, byte[]>() { { "foo", new byte[] { 0, 1, 2, 3, 4 } } }
                },

                PendingActivities = new List<PendingActivityInfo>()
                {
                    new PendingActivityInfo()
                    {
                        ActivityId         = "my-activity-id",
                        ActivityTypeName   = "my-activity",
                        State              = PendingActivityState.Started,
                        HeartbeatDetails   = new byte[] { 0, 1, 2, 3, 4 },
                        LastHeartbeatTime  = new DateTime(2020, 5, 24, 13, 32, 0),
                        LastStartedTime    = new DateTime(2020, 5, 24, 13, 33, 0),
                        Attempt            = 2,
                        MaximumAttempts    = 3,
                        ScheduledTime      = new DateTime(2020, 5, 24, 13, 34, 0),
                        ExpirationTime     = new DateTime(2020, 5, 24, 13, 35, 0),
                        LastWorkerIdentity = "my-worker"
                    }
                },

                PendingChildren = new List<PendingChildExecutionInfo>()
                {
                    new PendingChildExecutionInfo()
                    {
                        WorkflowId       = "my-workflow-id",
                        RunId            = "my-run-id",
                        WorkflowTypeName = "my-workflow",
                        InitiatedId      = 16000
                    }
                }
            };

            return description;
        }

        /// <summary>
        /// Ensures that the <paramref name="description"/> passed matches the instance
        /// returned by <see cref="GetWorkflowDescription"/>.
        /// </summary>
        /// <param name="description">The response to ve validated.</param>
        private void VerifyWorkflowDescription(WorkflowDescription description)
        {
            var expected = GetWorkflowDescription();

            Assert.NotNull(description);

            //---------------------------------------------

            var config = description.Configuration;

            Assert.NotNull(config);
            Assert.NotNull(config.TaskList);
            Assert.Equal(expected.Configuration.TaskList, config.TaskList);
            Assert.Equal(expected.Configuration.StartToCloseTimeout, config.StartToCloseTimeout);
            Assert.Equal(expected.Configuration.TaskStartToCloseTimeout, config.TaskStartToCloseTimeout);
            Assert.Equal(expected.Configuration.ParentClosePolicy, config.ParentClosePolicy);

            //---------------------------------------------

            var status = description.ExeecutionInfo;

            Assert.NotNull(status);
            Assert.NotNull(status.Execution);
            Assert.Equal(expected.ExeecutionInfo.Execution.WorkflowId, status.Execution.WorkflowId);
            Assert.Equal(expected.ExeecutionInfo.Execution.RunId, status.Execution.RunId);
            Assert.NotNull(status.TypeName);
            Assert.Equal(expected.ExeecutionInfo.TypeName, status.TypeName);
            Assert.Equal(expected.ExeecutionInfo.StartTime, status.StartTime);
            Assert.Equal(expected.ExeecutionInfo.CloseTime, status.CloseTime);
            Assert.Equal(expected.ExeecutionInfo.CloseStatus, status.CloseStatus);
            Assert.Equal(expected.ExeecutionInfo.HistoryLength, status.HistoryLength);
            Assert.Equal(expected.ExeecutionInfo.ParentNamespace, status.ParentNamespace);

            Assert.NotNull(status.ParentExecution);
            Assert.Equal(expected.ExeecutionInfo.ParentExecution.WorkflowId, status.ParentExecution.WorkflowId);
            Assert.Equal(expected.ExeecutionInfo.ParentExecution.RunId, status.ParentExecution.RunId);

            Assert.Equal(expected.ExeecutionInfo.ExecutionTime, status.ExecutionTime);

            //---------------------------------------------

            Assert.NotNull(description.PendingActivities);
            Assert.Single(description.PendingActivities);

            var pendingActivity = description.PendingActivities.First();

            Assert.Equal("my-activity-id", pendingActivity.ActivityId);
            Assert.Equal("my-activity", pendingActivity.ActivityTypeName);
            Assert.Equal(PendingActivityState.Started, PendingActivityState.Started);
            Assert.Equal(2, pendingActivity.Attempt);
            Assert.Equal(3, pendingActivity.MaximumAttempts);
            Assert.Equal(new DateTime(2020, 5, 24, 13, 34, 0), pendingActivity.ScheduledTime);
            Assert.Equal(new DateTime(2020, 5, 24, 13, 35, 0), pendingActivity.ExpirationTime);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, pendingActivity.HeartbeatDetails);
            Assert.Equal(new DateTime(2020, 5, 24, 13, 32, 0), pendingActivity.LastHeartbeatTime);
            Assert.Equal(new DateTime(2020, 5, 24, 13, 33, 0), pendingActivity.LastStartedTime);

            //---------------------------------------------

            Assert.NotNull(description.PendingChildren);
            Assert.Single(description.PendingChildren);

            var pendingChild = description.PendingChildren.First();

            Assert.Equal("my-workflow-id", pendingChild.WorkflowId);
            Assert.Equal("my-run-id", pendingChild.RunId);
            Assert.Equal("my-workflow-typename", pendingChild.WorkflowTypeName);
            Assert.Equal(16000, pendingChild.InitiatedId);

            //---------------------------------------------

            Assert.NotNull(status.Memo);
            Assert.Equal(expected.ExeecutionInfo.Memo.Count, status.Memo.Count);

            for (int i = 0; i < expected.ExeecutionInfo.Memo.Count; i++)
            {
                var refField = expected.ExeecutionInfo.Memo.ToArray()[i];
                var field    = status.Memo.ToArray()[i];

                Assert.Equal(refField.Key, field.Key);
                Assert.Equal(refField.Value, field.Value);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowDescribeExecutionReply()
        {
            WorkflowDescribeExecutionReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowDescribeExecutionReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowDescribeExecutionReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Details);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");
                message.Details = GetWorkflowDescription();

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowDescribeExecutionReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                VerifyWorkflowDescription(message.Details);

                // Clone()

                message = (WorkflowDescribeExecutionReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                VerifyWorkflowDescription(message.Details);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                VerifyWorkflowDescription(message.Details);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowGetResultRequest()
        {
            WorkflowGetResultRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowGetResultRequest();

                Assert.Equal(InternalMessageTypes.WorkflowGetResultReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetResultRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.WorkflowId);
                Assert.Null(message.RunId);
                Assert.Null(message.Namespace);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.WorkflowId = "666";
                message.RunId = "777";
                message.Namespace = "my-namespace";

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetResultRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);

                // Clone()

                message = (WorkflowGetResultRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-namespace", message.Namespace);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowGetResultReply()
        {
            // In addition to verifying basic serialization for the [WorkflowGetResultReply]
            // message, we're also going to test transmitting small to very large (10MiB) 
            // payloads to ensure that these are handled correctly.

            var rand = new Random();

            WorkflowGetResultReply message;

            for (int size = 1024; size <= 1 * 1024 * 1024; size *= 2)
            {
                using (var stream = new MemoryStream())
                {
                    var data = new byte[size];

                    rand.NextBytes(data);

                    message = new WorkflowGetResultReply();

                    // Empty message.

                    stream.SetLength(0);
                    stream.Write(message.SerializeAsBytes());
                    stream.Seek(0, SeekOrigin.Begin);

                    message = ProxyMessage.Deserialize<WorkflowGetResultReply>(stream);
                    Assert.NotNull(message);
                    Assert.Equal(0, message.ClientId);
                    Assert.Equal(0, message.RequestId);
                    Assert.Null(message.Error);
                    Assert.Null(message.Result);

                    // Round-trip

                    message.ClientId = 444;
                    message.RequestId = 555;
                    message.Error = new TemporalError("MyError");
                    message.Result = data;

                    Assert.Equal(444, message.ClientId);
                    Assert.Equal(555, message.RequestId);
                    Assert.Equal("MyError", message.Error.String);
                    Assert.Equal(data, message.Result);

                    stream.SetLength(0);
                    stream.Write(message.SerializeAsBytes());
                    stream.Seek(0, SeekOrigin.Begin);

                    message = ProxyMessage.Deserialize<WorkflowGetResultReply>(stream);
                    Assert.NotNull(message);
                    Assert.Equal(444, message.ClientId);
                    Assert.Equal(555, message.RequestId);
                    Assert.Equal("MyError", message.Error.String);
                    Assert.Equal(data, message.Result);

                    // Clone()

                    message = (WorkflowGetResultReply)message.Clone();
                    Assert.NotNull(message);
                    Assert.Equal(444, message.ClientId);
                    Assert.Equal(555, message.RequestId);
                    Assert.Equal("MyError", message.Error.String);
                    Assert.Equal(data, message.Result);

                    // Echo the message via the associated [temporal-proxy] and verify.

                    message = EchoToProxy(message);
                    Assert.NotNull(message);
                    Assert.Equal(444, message.ClientId);
                    Assert.Equal(555, message.RequestId);
                    Assert.Equal("MyError", message.Error.String);
                    Assert.Equal(data, message.Result);
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowSignalSubscribeRequest()
        {
            WorkflowSignalSubscribeRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowSignalSubscribeRequest();

                Assert.Equal(InternalMessageTypes.WorkflowSignalSubscribeReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalSubscribeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.SignalName);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.SignalName = "my-signal";

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-signal", message.SignalName);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalSubscribeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-signal", message.SignalName);

                // Clone()

                message = (WorkflowSignalSubscribeRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-signal", message.SignalName);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-signal", message.SignalName);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowSignalSubscribeReply()
        {
            WorkflowSignalSubscribeReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowSignalSubscribeReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalSubscribeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalSubscribeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (WorkflowSignalSubscribeReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowSignalInvokeRequest()
        {
            WorkflowSignalInvokeRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowSignalInvokeRequest();

                Assert.Equal(InternalMessageTypes.WorkflowSignalInvokeReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalInvokeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.SignalName);
                Assert.Null(message.SignalArgs);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.SignalName = "my-signal";
                message.SignalArgs = new byte[] { 0, 1, 2, 3, 4 };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalInvokeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);

                // Clone()

                message = (WorkflowSignalInvokeRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowSignalInvokeReply()
        {
            WorkflowSignalInvokeReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowSignalInvokeReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalInvokeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalInvokeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (WorkflowSignalInvokeReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowHasLastResultRequest()
        {
            WorkflowHasLastResultRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowHasLastResultRequest();

                Assert.Equal(InternalMessageTypes.WorkflowHasLastResultReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowHasLastResultRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowHasLastResultRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Clone()

                message = (WorkflowHasLastResultRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowHasLastResultReply()
        {
            WorkflowHasLastResultReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowHasLastResultReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowHasLastResultReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.False(message.HasResult);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");
                message.HasResult = true;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.HasResult);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowHasLastResultReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.HasResult);

                // Clone()

                message = (WorkflowHasLastResultReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.HasResult);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.HasResult);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowGetLastResultRequest()
        {
            WorkflowGetLastResultRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowGetLastResultRequest();

                Assert.Equal(InternalMessageTypes.WorkflowGetLastResultReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetLastResultRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetLastResultRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Clone()

                message = (WorkflowGetLastResultRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowGetLastResultReply()
        {
            WorkflowGetLastLastReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowGetLastLastReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetLastLastReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Result);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");
                message.Result = new byte[] { 0, 1, 2, 3, 4 };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetLastLastReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Clone()

                message = (WorkflowGetLastLastReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowDisconnectContextRequest()
        {
            WorkflowDisconnectContextRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowDisconnectContextRequest();

                Assert.Equal(InternalMessageTypes.WorkflowDisconnectContextReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowDisconnectContextRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowDisconnectContextRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Clone()

                message = (WorkflowDisconnectContextRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowDisconnectContextReply()
        {
            WorkflowDisconnectContextReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowDisconnectContextReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowDisconnectContextReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowDisconnectContextReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (WorkflowDisconnectContextReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowGetTimeRequest()
        {
            WorkflowGetTimeRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowGetTimeRequest();

                Assert.Equal(InternalMessageTypes.WorkflowGetTimeReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetTimeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetTimeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Clone()

                message = (WorkflowGetTimeRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowGetTimeReply()
        {
            WorkflowGetTimeReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowGetTimeReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetTimeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Equal(DateTime.MinValue, message.Time);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");
                message.Time = new DateTime(2019, 5, 28);

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new DateTime(2019, 5, 28), message.Time);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetTimeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new DateTime(2019, 5, 28), message.Time);

                // Clone()

                message = (WorkflowGetTimeReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new DateTime(2019, 5, 28), message.Time);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new DateTime(2019, 5, 28), message.Time);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowSleepRequest()
        {
            WorkflowSleepRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowSleepRequest();

                Assert.Equal(InternalMessageTypes.WorkflowSleepReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSleepRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(TimeSpan.Zero, message.Duration);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Duration = TimeSpan.FromDays(2);

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(TimeSpan.FromDays(2), message.Duration);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSleepRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(TimeSpan.FromDays(2), message.Duration);

                // Clone()

                message = (WorkflowSleepRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(TimeSpan.FromDays(2), message.Duration);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(TimeSpan.FromDays(2), message.Duration);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowSleepReply()
        {
            WorkflowSleepReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowSleepReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSleepReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSleepReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (WorkflowSleepReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        private void AssertEqualChildOptions(ChildWorkflowOptions expected, ChildWorkflowOptions actual)
        {
            Assert.Equal(expected.TaskList, actual.TaskList);
            Assert.Equal(expected.Namespace, actual.Namespace);
            Assert.Equal(expected.ChildPolicy, actual.ChildPolicy);
            Assert.Equal(expected.CronSchedule, actual.CronSchedule);
            Assert.Equal(expected.WorkflowId, actual.WorkflowId);
            Assert.Equal(expected.WaitUntilFinished, actual.WaitUntilFinished);
            Assert.Equal(expected.ScheduleToStartTimeout, actual.ScheduleToStartTimeout);
            Assert.Equal(expected.StartToCloseTimeout, actual.StartToCloseTimeout);
            Assert.Equal(expected.DecisionTaskTimeout, actual.DecisionTaskTimeout);
            Assert.Equal(expected.WorkflowIdReusePolicy, actual.WorkflowIdReusePolicy);
            Assert.Equal(expected.RetryOptions.MaximumAttempts, actual.RetryOptions.MaximumAttempts);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowExecuteChildRequest()
        {
            WorkflowExecuteChildRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowExecuteChildRequest();

                Assert.Equal(InternalMessageTypes.WorkflowExecuteChildReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowExecuteChildRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Workflow);
                Assert.Null(message.Args);
                Assert.Null(message.Options);

                // Round-trip

                var options = new ChildWorkflowOptions()
                {
                    TaskList               = "my-tasklist",
                    Namespace              = "my-namespace",
                    ChildPolicy            = ParentClosePolicy.RequestCancel,
                    CronSchedule           = "* 12 * * *",
                    WorkflowId             = "my-workflow",
                    WaitUntilFinished      = true,
                    ScheduleToStartTimeout = TimeSpan.FromSeconds(1),
                    StartToCloseTimeout    = TimeSpan.FromSeconds(2),
                    DecisionTaskTimeout    = TimeSpan.FromSeconds(3),
                    WorkflowIdReusePolicy  = WorkflowIdReusePolicy.RejectDuplicate,
                    RetryOptions           = new RetryOptions()
                    {
                        MaximumAttempts = 100
                    }
                };

                message.ClientId = 444;
                message.RequestId = 555;
                message.Workflow = "my-workflow";
                message.Args = new byte[] { 5, 6, 7, 8, 9 };
                message.Options = options;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                AssertEqualChildOptions(options, message.Options);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.Args);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowExecuteChildRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-workflow", message.Workflow);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.Args);
                AssertEqualChildOptions(options, message.Options);

                // Clone()

                message = (WorkflowExecuteChildRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-workflow", message.Workflow);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.Args);
                AssertEqualChildOptions(options, message.Options);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                AssertEqualChildOptions(options, message.Options);
                Assert.Equal("my-workflow", message.Workflow);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.Args);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowExecuteChildReply()
        {
            WorkflowExecuteChildReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowExecuteChildReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowExecuteChildReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Equal(0, message.ChildId);
                Assert.Null(message.Execution);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");
                message.ChildId = 666;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(666, message.ChildId);

                message.Execution = new WorkflowExecution("foo", "bar");
                Assert.Equal("foo", message.Execution.WorkflowId);
                Assert.Equal("bar", message.Execution.RunId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowExecuteChildReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(666, message.ChildId);
                Assert.Equal("foo", message.Execution.WorkflowId);
                Assert.Equal("bar", message.Execution.RunId);

                // Clone()

                message = (WorkflowExecuteChildReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(666, message.ChildId);
                Assert.Equal("foo", message.Execution.WorkflowId);
                Assert.Equal("bar", message.Execution.RunId);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(666, message.ChildId);
                Assert.Equal("foo", message.Execution.WorkflowId);
                Assert.Equal("bar", message.Execution.RunId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowWaitForChildRequest()
        {
            WorkflowWaitForChildRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowWaitForChildRequest();

                Assert.Equal(InternalMessageTypes.WorkflowWaitForChildReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowWaitForChildRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ChildId);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.ChildId = 666;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowWaitForChildRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);

                // Clone()

                message = (WorkflowWaitForChildRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowWaitForChildReply()
        {
            WorkflowWaitForChildReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowWaitForChildReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowWaitForChildReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Result);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");
                message.Result = new byte[] { 0, 1, 2, 3, 4 };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowWaitForChildReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Clone()

                message = (WorkflowWaitForChildReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowSignalChildRequest()
        {
            WorkflowSignalChildRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowSignalChildRequest();

                Assert.Equal(InternalMessageTypes.WorkflowSignalChildReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalChildRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ChildId);
                Assert.Null(message.SignalName);
                Assert.Null(message.SignalArgs);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.ChildId = 666;
                message.SignalName = "my-signal";
                message.SignalArgs = new byte[] { 0, 1, 2, 3, 4 };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalChildRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);

                // Clone()

                message = (WorkflowSignalChildRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowSignalChildReply()
        {
            WorkflowSignalChildReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowSignalChildReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalChildReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Result);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");
                message.Result = new byte[] { 0, 1, 2, 3, 4 };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalChildReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Clone()

                message = (WorkflowSignalChildReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowCancelChildRequest()
        {
            WorkflowCancelChildRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowCancelChildRequest();

                Assert.Equal(InternalMessageTypes.WorkflowCancelChildReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowCancelChildRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ChildId);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.ChildId = 666;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowCancelChildRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);

                // Clone()

                message = (WorkflowCancelChildRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowCancelChildReply()
        {
            WorkflowCancelChildReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowCancelChildReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowCancelChildReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowCancelChildReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (WorkflowCancelChildReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowSetQueryHandlerRequest()
        {
            WorkflowSetQueryHandlerRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowSetQueryHandlerRequest();

                Assert.Equal(InternalMessageTypes.WorkflowSetQueryHandlerReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSetQueryHandlerRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.QueryName);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.QueryName = "my-query";

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-query", message.QueryName);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSetQueryHandlerRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-query", message.QueryName);

                // Clone()

                message = (WorkflowSetQueryHandlerRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-query", message.QueryName);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-query", message.QueryName);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowSetQueryHandlerReply()
        {
            WorkflowSetQueryHandlerReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowSetQueryHandlerReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSetQueryHandlerReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSetQueryHandlerReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (WorkflowSetQueryHandlerReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowQueryInvokeRequest()
        {
            WorkflowQueryInvokeRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowQueryInvokeRequest();

                Assert.Equal(InternalMessageTypes.WorkflowQueryInvokeReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueryInvokeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ContextId);
                Assert.Null(message.QueryName);
                Assert.Null(message.QueryArgs);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.ContextId = 666;
                message.QueryName = "my-query";
                message.QueryArgs = new byte[] { 0, 1, 2, 3, 4 };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-query", message.QueryName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.QueryArgs);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueryInvokeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-query", message.QueryName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.QueryArgs);

                // Clone()

                message = (WorkflowQueryInvokeRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-query", message.QueryName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.QueryArgs);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-query", message.QueryName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.QueryArgs);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowQueryInvokeReply()
        {
            WorkflowQueryInvokeReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowQueryInvokeReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueryInvokeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Result);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");
                message.Result = new byte[] { 0, 1, 2, 3, 4 };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueryInvokeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Clone()

                message = (WorkflowQueryInvokeReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowGetVersionRequest()
        {
            WorkflowGetVersionRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowGetVersionRequest();

                Assert.Equal(InternalMessageTypes.WorkflowGetVersionReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetVersionRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ContextId);
                Assert.Null(message.ChangeId);
                Assert.Equal(0, message.MaxSupported);
                Assert.Equal(0, message.MinSupported);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.ContextId = 666;
                message.ChangeId = "my-change";
                message.MinSupported = 10;
                message.MaxSupported = 20;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-change", message.ChangeId);
                Assert.Equal(10, message.MinSupported);
                Assert.Equal(20, message.MaxSupported);
 
                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);
                Assert.Equal("my-change", message.ChangeId);
                Assert.Equal(10, message.MinSupported);
                Assert.Equal(20, message.MaxSupported);

                message = ProxyMessage.Deserialize<WorkflowGetVersionRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-change", message.ChangeId);
                Assert.Equal(10, message.MinSupported);
                Assert.Equal(20, message.MaxSupported);

                // Clone()

                message = (WorkflowGetVersionRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-change", message.ChangeId);
                Assert.Equal(10, message.MinSupported);
                Assert.Equal(20, message.MaxSupported);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-change", message.ChangeId);
                Assert.Equal(10, message.MinSupported);
                Assert.Equal(20, message.MaxSupported);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowGetVersionReply()
        {
            WorkflowGetVersionReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowGetVersionReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetVersionReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Equal(0, message.Version);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");
                message.Version = 20;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(20, message.Version);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetVersionReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(20, message.Version);

                // Clone()

                message = (WorkflowGetVersionReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(20, message.Version);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(20, message.Version);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowFutureReadyRequest()
        {
            WorkflowFutureReadyRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowFutureReadyRequest();

                Assert.Equal(InternalMessageTypes.WorkflowFutureReadyReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowFutureReadyRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ContextId);
                Assert.Equal(0, message.FutureOperationId);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.ContextId = 666;
                message.FutureOperationId = 777;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.FutureOperationId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);
                Assert.Equal(777, message.FutureOperationId);

                message = ProxyMessage.Deserialize<WorkflowFutureReadyRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.FutureOperationId);

                // Clone()

                message = (WorkflowFutureReadyRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.FutureOperationId);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.FutureOperationId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowFutureReadyReply()
        {
            WorkflowFutureReadyReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowFutureReadyReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowFutureReadyReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowFutureReadyReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (WorkflowFutureReadyReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowQueueNewRequest()
        {
            WorkflowQueueNewRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowQueueNewRequest();

                Assert.Equal(InternalMessageTypes.WorkflowQueueNewReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueueNewRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ContextId);
                Assert.Equal(0, message.QueueId);
                Assert.Equal(0, message.Capacity);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.ContextId = 666;
                message.QueueId = 777;
                message.Capacity = 888;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.QueueId);
                Assert.Equal(888, message.Capacity);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueueNewRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.QueueId);
                Assert.Equal(888, message.Capacity);

                // Clone()

                message = (WorkflowQueueNewRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.QueueId);
                Assert.Equal(888, message.Capacity);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.QueueId);
                Assert.Equal(888, message.Capacity);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowQueueNewReply()
        {
            WorkflowQueueNewReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowQueueNewReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueueNewReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueueNewReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (WorkflowQueueNewReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowQueueWriteRequest()
        {
            WorkflowQueueWriteRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowQueueWriteRequest();

                Assert.Equal(InternalMessageTypes.WorkflowQueueWriteReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueueWriteRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ContextId);
                Assert.Equal(0, message.QueueId);
                Assert.False(message.NoBlock);
                Assert.Null(message.Data);

                // Round-trip

                message.ClientId  = 444;
                message.RequestId = 555;
                message.ContextId = 666;
                message.QueueId   = 777;
                message.NoBlock   = true;
                message.Data      = new byte[] { 0, 1, 2, 3, 4 };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.QueueId);
                Assert.True(message.NoBlock);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Data);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);
                Assert.Equal(777, message.QueueId);
                Assert.True(message.NoBlock);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Data);

                message = ProxyMessage.Deserialize<WorkflowQueueWriteRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.QueueId);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Data);

                // Clone()

                message = (WorkflowQueueWriteRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.QueueId);
                Assert.True(message.NoBlock);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Data);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.QueueId);
                Assert.True(message.NoBlock);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Data);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowQueueWriteReply()
        {
            WorkflowQueueWriteReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowQueueWriteReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueueWriteReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.False(message.IsFull);

                // Round-trip

                message.ClientId  = 444;
                message.RequestId = 555;
                message.Error     = new TemporalError("MyError");
                message.IsFull    = true;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.IsFull);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueueWriteReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.IsFull);

                // Clone()

                message = (WorkflowQueueWriteReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.IsFull);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.IsFull);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowQueueReadRequest()
        {
            WorkflowQueueReadRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowQueueReadRequest();

                Assert.Equal(InternalMessageTypes.WorkflowQueueReadReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueueReadRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ContextId);
                Assert.Equal(0, message.QueueId);
                Assert.Equal(TimeSpan.Zero, message.Timeout);

                // Round-trip

                message.ClientId  = 444;
                message.RequestId = 555;
                message.ContextId = 666;
                message.QueueId   = 777;
                message.Timeout   = TimeSpan.FromSeconds(55);

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.QueueId);
                Assert.Equal(TimeSpan.FromSeconds(55), message.Timeout);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);
                Assert.Equal(777, message.QueueId);
                Assert.Equal(TimeSpan.FromSeconds(55), message.Timeout);

                message = ProxyMessage.Deserialize<WorkflowQueueReadRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.QueueId);
                Assert.Equal(TimeSpan.FromSeconds(55), message.Timeout);

                // Clone()

                message = (WorkflowQueueReadRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.QueueId);
                Assert.Equal(TimeSpan.FromSeconds(55), message.Timeout);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.QueueId);
                Assert.Equal(TimeSpan.FromSeconds(55), message.Timeout);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowQueueReadReply()
        {
            WorkflowQueueReadReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowQueueReadReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueueReadReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.False(message.IsClosed);
                Assert.Null(message.Data);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");
                message.IsClosed = true;
                message.Data = new byte[] { 0, 1, 2, 3, 4 };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.IsClosed);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Data);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueueReadReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.True(message.IsClosed);
                Assert.Equal("MyError", message.Error.String);

                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Data);

                // Clone()

                message = (WorkflowQueueReadReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.IsClosed);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Data);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.IsClosed);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Data);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowQueueCloseRequest()
        {
            WorkflowQueueCloseRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowQueueCloseRequest();

                Assert.Equal(InternalMessageTypes.WorkflowQueueCloseReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueueCloseRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ContextId);
                Assert.Equal(0, message.QueueId);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.ContextId = 666;
                message.QueueId = 777;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.QueueId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);
                Assert.Equal(777, message.QueueId);

                message = ProxyMessage.Deserialize<WorkflowQueueCloseRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.QueueId);

                // Clone()

                message = (WorkflowQueueCloseRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.QueueId);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(777, message.QueueId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonTemporal)]
        public void Test_WorkflowQueueCloseReply()
        {
            WorkflowQueueCloseReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowQueueCloseReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueueCloseReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueueCloseReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (WorkflowQueueCloseReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }
    }
}
