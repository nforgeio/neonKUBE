//-----------------------------------------------------------------------------
// FILE:        Test_Messages.Workflow.cs
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
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Cryptography;
using Neon.Data;
using Neon.IO;
using Neon.Time;
using Neon.Xunit;
using Neon.Xunit.Cadence;

using Newtonsoft.Json;
using Test.Neon.Models;
using Xunit;

namespace TestCadence
{
    public partial class Test_Messages
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.RequestId = 555;
                message.Name = "Foo";
                Assert.Equal(555, message.RequestId);
                Assert.Equal("Foo", message.Name);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowRegisterRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("Foo", message.Name);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("Foo", message.Name);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("Foo", message.Name);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowRegisterReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ContextId);
                Assert.Null(message.Domain);
                Assert.Null(message.Workflow);
                Assert.Null(message.Args);
                Assert.Null(message.Options);

                // Round-trip

                message.RequestId = 555;
                message.ContextId = 666;
                message.Domain = "my-domain";
                message.Workflow = "Foo";
                message.Args = new byte[] { 0, 1, 2, 3, 4 };
                message.Options = new InternalStartWorkflowOptions() { TaskList = "my-list", ExecutionStartToCloseTimeout = GoTimeSpan.Parse("100s").Ticks };
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("Foo", message.Workflow);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-list", message.Options.TaskList);
                Assert.Equal(GoTimeSpan.Parse("100s").Ticks, message.Options.ExecutionStartToCloseTimeout);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowExecuteRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("Foo", message.Workflow);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-list", message.Options.TaskList);
                Assert.Equal(GoTimeSpan.Parse("100s").Ticks, message.Options.ExecutionStartToCloseTimeout);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("Foo", message.Workflow);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-list", message.Options.TaskList);
                Assert.Equal(GoTimeSpan.Parse("100s").Ticks, message.Options.ExecutionStartToCloseTimeout);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("Foo", message.Workflow);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-list", message.Options.TaskList);
                Assert.Equal(GoTimeSpan.Parse("100s").Ticks, message.Options.ExecutionStartToCloseTimeout);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Execution);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Execution = new InternalWorkflowExecution() { ID = "foo", RunID = "bar" };
                Assert.Equal("foo", message.Execution.ID);
                Assert.Equal("bar", message.Execution.RunID);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowExecuteReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("foo", message.Execution.ID);
                Assert.Equal("bar", message.Execution.RunID);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("foo", message.Execution.ID);
                Assert.Equal("bar", message.Execution.RunID);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("foo", message.Execution.ID);
                Assert.Equal("bar", message.Execution.RunID);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ContextId);
                Assert.Null(message.Name);
                Assert.Null(message.Args);
                Assert.Null(message.Domain);
                Assert.Null(message.WorkflowId);
                Assert.Null(message.RunId);
                Assert.Null(message.WorkflowType);
                Assert.Null(message.TaskList);
                Assert.Equal(TimeSpan.Zero, message.ExecutionStartToCloseTimeout);
                Assert.Equal(InternalReplayStatus.Unspecified, message.ReplayStatus);

                // Round-trip

                message.RequestId = 555;
                message.ContextId = 666;
                message.Name = "Foo";
                message.Args = new byte[] { 0, 1, 2, 3, 4 };
                message.Domain = "my-domain";
                message.WorkflowId = "my-workflowid";
                message.RunId = "my-runid";
                message.WorkflowType = "my-workflowtype";
                message.ExecutionStartToCloseTimeout = TimeSpan.FromDays(1);
                message.ReplayStatus = InternalReplayStatus.Replaying;
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("Foo", message.Name);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("my-workflowid", message.WorkflowId);
                Assert.Equal("my-runid", message.RunId);
                Assert.Equal("my-workflowtype", message.WorkflowType);
                Assert.Equal(TimeSpan.FromDays(1), message.ExecutionStartToCloseTimeout);
                Assert.Equal(InternalReplayStatus.Replaying, message.ReplayStatus);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowInvokeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("Foo", message.Name);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("my-workflowid", message.WorkflowId);
                Assert.Equal("my-runid", message.RunId);
                Assert.Equal("my-workflowtype", message.WorkflowType);
                Assert.Equal(TimeSpan.FromDays(1), message.ExecutionStartToCloseTimeout);
                Assert.Equal(InternalReplayStatus.Replaying, message.ReplayStatus);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("Foo", message.Name);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("my-workflowid", message.WorkflowId);
                Assert.Equal("my-runid", message.RunId);
                Assert.Equal("my-workflowtype", message.WorkflowType);
                Assert.Equal(TimeSpan.FromDays(1), message.ExecutionStartToCloseTimeout);
                Assert.Equal(InternalReplayStatus.Replaying, message.ReplayStatus);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("Foo", message.Name);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("my-workflowid", message.WorkflowId);
                Assert.Equal("my-runid", message.RunId);
                Assert.Equal("my-workflowtype", message.WorkflowType);
                Assert.Equal(TimeSpan.FromDays(1), message.ExecutionStartToCloseTimeout);
                Assert.Equal(InternalReplayStatus.Replaying, message.ReplayStatus);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Result);
                Assert.Null(message.Error);
                Assert.False(message.ContinueAsNew);
                Assert.Null(message.ContinueAsNewArgs);
                Assert.Equal(0, message.ContinueAsNewExecutionStartToCloseTimeout);
                Assert.Equal(0, message.ContinueAsNewScheduleToCloseTimeout);
                Assert.Equal(0, message.ContinueAsNewScheduleToStartTimeout);
                Assert.Equal(0, message.ContinueAsNewStartToCloseTimeout);
                Assert.Null(message.ContinueAsNewTaskList);
                Assert.Null(message.ContinueAsNewDomain);

                // Round-trip

                message.RequestId = 555;
                message.Error = new CadenceError("MyError");
                message.Result = new byte[] { 0, 1, 2, 3, 4 };
                message.ContinueAsNew = true;
                message.ContinueAsNewArgs = new byte[] { 5, 6, 7, 8, 9 };
                message.ContinueAsNewExecutionStartToCloseTimeout = 1000;
                message.ContinueAsNewScheduleToCloseTimeout = 2000;
                message.ContinueAsNewScheduleToStartTimeout = 3000;
                message.ContinueAsNewStartToCloseTimeout = 4000;
                message.ContinueAsNewTaskList = "my-tasklist";
                message.ContinueAsNewDomain = "my-domain";
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
                Assert.True(message.ContinueAsNew);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.ContinueAsNewArgs);
                Assert.Equal(1000, message.ContinueAsNewExecutionStartToCloseTimeout);
                Assert.Equal("my-tasklist", message.ContinueAsNewTaskList);
                Assert.Equal("my-domain", message.ContinueAsNewDomain);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowInvokeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
                Assert.True(message.ContinueAsNew);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.ContinueAsNewArgs);
                Assert.Equal(1000, message.ContinueAsNewExecutionStartToCloseTimeout);
                Assert.Equal(2000, message.ContinueAsNewScheduleToCloseTimeout);
                Assert.Equal(3000, message.ContinueAsNewScheduleToStartTimeout);
                Assert.Equal(4000, message.ContinueAsNewStartToCloseTimeout);
                Assert.Equal("my-tasklist", message.ContinueAsNewTaskList);
                Assert.Equal("my-domain", message.ContinueAsNewDomain);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
                Assert.True(message.ContinueAsNew);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.ContinueAsNewArgs);
                Assert.Equal(1000, message.ContinueAsNewExecutionStartToCloseTimeout);
                Assert.Equal(2000, message.ContinueAsNewScheduleToCloseTimeout);
                Assert.Equal(3000, message.ContinueAsNewScheduleToStartTimeout);
                Assert.Equal(4000, message.ContinueAsNewStartToCloseTimeout);
                Assert.Equal("my-tasklist", message.ContinueAsNewTaskList);
                Assert.Equal("my-domain", message.ContinueAsNewDomain);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
                Assert.True(message.ContinueAsNew);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.ContinueAsNewArgs);
                Assert.Equal(1000, message.ContinueAsNewExecutionStartToCloseTimeout);
                Assert.Equal(2000, message.ContinueAsNewScheduleToCloseTimeout);
                Assert.Equal(3000, message.ContinueAsNewScheduleToStartTimeout);
                Assert.Equal(4000, message.ContinueAsNewStartToCloseTimeout);
                Assert.Equal("my-tasklist", message.ContinueAsNewTaskList);
                Assert.Equal("my-domain", message.ContinueAsNewDomain);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.WorkflowId);
                Assert.Null(message.RunId);
                Assert.Null(message.Domain);

                // Round-trip

                message.RequestId = 555;
                message.WorkflowId = "666";
                message.RunId = "777";
                message.Domain = "my-domain";
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-domain", message.Domain);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowCancelRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-domain", message.Domain);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-domain", message.Domain);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-domain", message.Domain);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowCancelReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.WorkflowId);
                Assert.Null(message.RunId);
                Assert.Null(message.Reason);
                Assert.Null(message.Details);

                // Round-trip

                message.RequestId = 555;
                message.WorkflowId = "666";
                message.RunId = "777";
                message.Reason = "my-reason";
                message.Details = new byte[] { 0, 1, 2, 3, 4 };
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-reason", message.Reason);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Details);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowTerminateRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-reason", message.Reason);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Details);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-reason", message.Reason);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Details);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-reason", message.Reason);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Details);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowTerminateReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.WorkflowId);
                Assert.Null(message.RunId);
                Assert.Null(message.SignalName);
                Assert.Null(message.SignalArgs);

                // Round-trip

                message.RequestId = 555;
                message.WorkflowId = "666";
                message.RunId = "777";
                message.SignalName = "my-signal";
                message.SignalArgs = new byte[] { 0, 1, 2, 3, 4 };
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Workflow);
                Assert.Null(message.WorkflowId);
                Assert.Null(message.SignalName);
                Assert.Null(message.SignalArgs);
                Assert.Null(message.Options);
                Assert.Null(message.WorkflowArgs);

                // Round-trip

                message.RequestId = 555;
                message.Workflow = "my-workflow";
                message.WorkflowId = "666";
                message.SignalName = "my-signal";
                message.SignalArgs = new byte[] { 0, 1, 2, 3, 4 };
                message.Options = new InternalStartWorkflowOptions() { TaskList = "my-tasklist", WorkflowIdReusePolicy = (int)WorkflowIdReusePolicy.AllowDuplicate };
                message.WorkflowArgs = new byte[] { 5, 6, 7, 8, 9 };
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-workflow", message.Workflow);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);
                Assert.Equal("my-tasklist", message.Options.TaskList);
                Assert.Equal((int)WorkflowIdReusePolicy.AllowDuplicate, message.Options.WorkflowIdReusePolicy);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.WorkflowArgs);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalWithStartRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-workflow", message.Workflow);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);
                Assert.Equal("my-tasklist", message.Options.TaskList);
                Assert.Equal((int)WorkflowIdReusePolicy.AllowDuplicate, message.Options.WorkflowIdReusePolicy);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.WorkflowArgs);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-workflow", message.Workflow);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);
                Assert.Equal("my-tasklist", message.Options.TaskList);
                Assert.Equal((int)WorkflowIdReusePolicy.AllowDuplicate, message.Options.WorkflowIdReusePolicy);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.WorkflowArgs);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-workflow", message.Workflow);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);
                Assert.Equal("my-tasklist", message.Options.TaskList);
                Assert.Equal((int)WorkflowIdReusePolicy.AllowDuplicate, message.Options.WorkflowIdReusePolicy);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.WorkflowArgs);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Execution);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);
                message.Execution = new InternalWorkflowExecution() { ID = "666", RunID = "777" };

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalWithStartReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal("666", message.Execution.ID);
                Assert.Equal("777", message.Execution.RunID);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal("666", message.Execution.ID);
                Assert.Equal("777", message.Execution.RunID);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal("666", message.Execution.ID);
                Assert.Equal("777", message.Execution.RunID);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.WorkflowId);
                Assert.Null(message.RunId);
                Assert.Null(message.QueryName);
                Assert.Null(message.QueryArgs);

                // Round-trip

                message.RequestId = 555;
                message.WorkflowId = "666";
                message.RunId = "777";
                message.QueryName = "my-query";
                message.QueryArgs = new byte[] { 0, 1, 2, 3, 4 };
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-query", message.QueryName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.QueryArgs);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueryRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-query", message.QueryName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.QueryArgs);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-query", message.QueryName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.QueryArgs);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
                Assert.Equal("my-query", message.QueryName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.QueryArgs);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Result);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);
                message.Result = new byte[] { 0, 1, 2, 3, 4 };

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueryReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ContextId);
                Assert.Null(message.MutableId);
                Assert.Null(message.Result);
                Assert.False(message.Update);

                // Round-trip

                message.RequestId = 555;
                message.ContextId = 666;
                message.MutableId = "my-mutable";
                message.Result = new byte[] { 0, 1, 2, 3, 4 };
                message.Update = true;
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-mutable", message.MutableId);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
                Assert.True(message.Update);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowMutableRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-mutable", message.MutableId);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
                Assert.True(message.Update);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-mutable", message.MutableId);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
                Assert.True(message.Update);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-mutable", message.MutableId);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
                Assert.True(message.Update);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Result);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);
                message.ContextId = 666;
                Assert.Equal(666, message.ContextId);
                message.Result = new byte[] { 0, 1, 2, 3, 4 };

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowMutableReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.WorkflowId);
                Assert.Null(message.RunId);

                // Round-trip

                message.RequestId = 555;
                message.WorkflowId = "666";
                message.RunId = "777";
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowDescribeExecutionRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
            }
        }

        /// <summary>
        /// Returns a test <see cref="InternalDescribeWorkflowExecutionResponse"/>.
        /// </summary>
        private InternalDescribeWorkflowExecutionResponse GetDescribeWorkflow()
        {
            var details = new InternalDescribeWorkflowExecutionResponse()
            {
                ExecutionConfiguration = new InternalWorkflowExecutionConfiguration()
                {
                    TaskList                       = new InternalTaskList() { Name = "my-tasklist", TaskListKind = (InternalTaskListKind)TaskListKind.Sticky },
                    ExecutionStartToCloseTimeout   = 1000,
                    TaskStartToCloseTimeoutSeconds = 2000,
                    ChildPolicy                    = InternalChildTerminationPolicy.REQUEST_CANCEL
                },

                WorkflowExecutionInfo = new InternalWorkflowExecutionInfo()
                {
                    Execution = new InternalWorkflowExecution2()
                    {
                        ID    = "workflow-id",
                        RunID = "run-id",
                    },

                    WorkflowType        = new InternalWorkflowType() { Name = "my-workflow" },
                    StartTime           = 3000,
                    CloseTime           = 4000,
                    WorkflowCloseStatus = (InternalWorkflowCloseStatus)InternalWorkflowCloseStatus.CONTINUED_AS_NEW,
                    HistoryLength       = 5000,
                    ParentDomainId      = "parent-domain",
                    ParentExecution     = new InternalWorkflowExecution2() { ID = "parent-id", RunID = "parent-runid" },
                    ExecutionTime       = 6000,

                    Memo = new InternalMemo()
                    {
                        Fields = new Dictionary<string, byte[]>() { { "foo", new byte[] { 0, 1, 2, 3, 4 } } }
                    }
                },

                PendingActivities = new List<InternalPendingActivityInfo>()
                {
                    new InternalPendingActivityInfo()
                    {
                        ActivityID             = "my-activityid",
                        ActivityType           = new InternalActivityType() { Name = "my-activity" },
                        Attempt                = 10000,
                        ExpirationTimestamp    = 11000,
                        HeartbeatDetails       = new byte[] { 0,1,2,3,4 },
                        LastHeartbeatTimestamp = 12000,
                        LastStartedTimestamp   = 13000,
                        MaximumAttempts        = 14000,
                        ScheduledTimestamp     = 15000,
                        State                  = InternalPendingActivityState.STARTED
                    }
                }
            };

            return details;
        }

        /// <summary>
        /// Ensures that the <paramref name="details"/> passed matches the instance
        /// returned by <see cref="GetDescribeWorkflow"/>.
        /// </summary>
        /// <param name="details">The response to ve validated.</param>
        private void VerifyDescribeWorkflow(InternalDescribeWorkflowExecutionResponse details)
        {
            var expected = GetDescribeWorkflow();

            Assert.NotNull(details);

            var config = details.ExecutionConfiguration;

            Assert.NotNull(config);
            Assert.NotNull(config.TaskList);
            Assert.Equal(expected.ExecutionConfiguration.TaskList.Name, config.TaskList.Name);
            Assert.Equal(expected.ExecutionConfiguration.ExecutionStartToCloseTimeout, config.ExecutionStartToCloseTimeout);
            Assert.Equal(expected.ExecutionConfiguration.TaskStartToCloseTimeoutSeconds, config.TaskStartToCloseTimeoutSeconds);
            Assert.Equal(expected.ExecutionConfiguration.ChildPolicy, config.ChildPolicy);

            var info = details.WorkflowExecutionInfo;

            Assert.NotNull(info);
            Assert.NotNull(info.Execution);
            Assert.Equal(expected.WorkflowExecutionInfo.Execution.ID, info.Execution.ID);
            Assert.Equal(expected.WorkflowExecutionInfo.Execution.RunID, info.Execution.RunID);
            Assert.NotNull(info.WorkflowType);
            Assert.Equal(expected.WorkflowExecutionInfo.WorkflowType.Name, info.WorkflowType.Name);
            Assert.Equal(expected.WorkflowExecutionInfo.StartTime, info.StartTime);
            Assert.Equal(expected.WorkflowExecutionInfo.CloseTime, info.CloseTime);
            Assert.Equal(expected.WorkflowExecutionInfo.WorkflowCloseStatus, info.WorkflowCloseStatus);
            Assert.Equal(expected.WorkflowExecutionInfo.HistoryLength, info.HistoryLength);
            Assert.Equal(expected.WorkflowExecutionInfo.ParentDomainId, info.ParentDomainId);

            Assert.NotNull(info.ParentExecution);
            Assert.Equal(expected.WorkflowExecutionInfo.ParentExecution.ID, info.ParentExecution.ID);
            Assert.Equal(expected.WorkflowExecutionInfo.ParentExecution.RunID, info.ParentExecution.RunID);

            Assert.Equal(expected.WorkflowExecutionInfo.ExecutionTime, info.ExecutionTime);

            Assert.NotNull(info.Memo);
            Assert.NotNull(info.Memo.Fields);
            Assert.Equal(expected.WorkflowExecutionInfo.Memo.Fields.Count, info.Memo.Fields.Count);

            for (int i = 0; i < expected.WorkflowExecutionInfo.Memo.Fields.Count; i++)
            {
                var refField = expected.WorkflowExecutionInfo.Memo.Fields.ToArray()[i];
                var field    = info.Memo.Fields.ToArray()[i];

                Assert.Equal(refField.Key, field.Key);
                Assert.Equal(refField.Value, field.Value);
            }

        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Details);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);
                message.Details = GetDescribeWorkflow();

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowDescribeExecutionReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                VerifyDescribeWorkflow(message.Details);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                VerifyDescribeWorkflow(message.Details);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                VerifyDescribeWorkflow(message.Details);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.WorkflowId);
                Assert.Null(message.RunId);

                // Round-trip

                message.RequestId = 555;
                message.WorkflowId = "666";
                message.RunId = "777";
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetResultRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("777", message.RunId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Test_WorkflowGetResultReply()
        {
            WorkflowGetResultReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowGetResultReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetResultReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Result);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);
                message.Result = new byte[] { 0, 1, 2, 3, 4 };

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetResultReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.SignalName);

                // Round-trip

                message.RequestId = 555;
                message.SignalName = "my-signal";
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-signal", message.SignalName);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalSubscribeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-signal", message.SignalName);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-signal", message.SignalName);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-signal", message.SignalName);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalSubscribeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.SignalName);
                Assert.Null(message.SignalArgs);
                Assert.Equal(InternalReplayStatus.Unspecified, message.ReplayStatus);

                // Round-trip

                message.RequestId = 555;
                message.SignalName = "my-signal";
                message.SignalArgs = new byte[] { 0, 1, 2, 3, 4 };
                message.ReplayStatus = InternalReplayStatus.Replaying;
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);
                Assert.Equal(InternalReplayStatus.Replaying, message.ReplayStatus);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalInvokeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);
                Assert.Equal(InternalReplayStatus.Replaying, message.ReplayStatus);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);
                Assert.Equal(InternalReplayStatus.Replaying, message.ReplayStatus);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);
                Assert.Equal(InternalReplayStatus.Replaying, message.ReplayStatus);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalInvokeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowHasLastResultRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.False(message.HasResult);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);
                message.HasResult = true;
                Assert.True(message.HasResult);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowHasLastResultReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.HasResult);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.HasResult);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.HasResult);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetLastResultRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Result);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);
                message.Result = new byte[] { 0, 1, 2, 3, 4 }; ;
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetLastLastReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowDisconnectContextRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowDisconnectContextReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetTimeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Equal(DateTime.MinValue, message.Time);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);
                message.Time = new DateTime(2019, 5, 28);
                Assert.Equal(new DateTime(2019, 5, 28), message.Time);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetTimeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new DateTime(2019, 5, 28), message.Time);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new DateTime(2019, 5, 28), message.Time);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new DateTime(2019, 5, 28), message.Time);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Equal(TimeSpan.Zero, message.Duration);

                // Round-trip

                message.RequestId = 555;
                message.Duration = TimeSpan.FromDays(2);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(TimeSpan.FromDays(2), message.Duration);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSleepRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(TimeSpan.FromDays(2), message.Duration);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(TimeSpan.FromDays(2), message.Duration);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(TimeSpan.FromDays(2), message.Duration);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSleepReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        private void AssertEqualChildOptions(InternalChildWorkflowOptions expected, InternalChildWorkflowOptions actual)
        {
            Assert.Equal(expected.TaskList, actual.TaskList);
            Assert.Equal(expected.Domain, actual.Domain);
            Assert.Equal(expected.ChildPolicy, actual.ChildPolicy);
            Assert.Equal(expected.CronSchedule, actual.CronSchedule);
            Assert.Equal(expected.WorkflowID, actual.WorkflowID);
            Assert.Equal(expected.WaitForCancellation, actual.WaitForCancellation);
            Assert.Equal(expected.ExecutionStartToCloseTimeout, actual.ExecutionStartToCloseTimeout);
            Assert.Equal(expected.TaskStartToCloseTimeout, actual.TaskStartToCloseTimeout);
            Assert.Equal(expected.WorkflowIdReusePolicy, actual.WorkflowIdReusePolicy);
            Assert.Equal(expected.RetryPolicy.MaximumAttempts, actual.RetryPolicy.MaximumAttempts);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Workflow);
                Assert.Null(message.Args);
                Assert.Null(message.Options);

                // Round-trip

                var options = new InternalChildWorkflowOptions()
                {
                    TaskList                     = "my-tasklist",
                    Domain                       = "my-domain",
                    ChildPolicy                  = (int)InternalChildTerminationPolicy.REQUEST_CANCEL,
                    CronSchedule                 = "* 12 * * *",
                    WorkflowID                   = "my-workflow",
                    WaitForCancellation          = true,
                    ExecutionStartToCloseTimeout = 1000,
                    TaskStartToCloseTimeout      = 2000,
                    WorkflowIdReusePolicy        = (int)WorkflowIdReusePolicy.RejectDuplicate,
                    RetryPolicy                  = new InternalRetryPolicy()
                    {
                        MaximumAttempts = 100
                    }
                };

                message.RequestId = 555;
                message.Workflow = "my-workflow";
                message.Args = new byte[] { 5, 6, 7, 8, 9 };
                message.Options = options;
                Assert.Equal(555, message.RequestId);
                AssertEqualChildOptions(options, message.Options);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.Args);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowExecuteChildRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-workflow", message.Workflow);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.Args);
                AssertEqualChildOptions(options, message.Options);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                AssertEqualChildOptions(options, message.Options);
                Assert.Equal("my-workflow", message.Workflow);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.Args);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                AssertEqualChildOptions(options, message.Options);
                Assert.Equal("my-workflow", message.Workflow);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.Args);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Equal(0, message.ChildId);
                Assert.Null(message.Execution);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);
                message.ChildId = 666;
                Assert.Equal(666, message.ChildId);

                message.Execution = new InternalWorkflowExecution() { ID = "foo", RunID = "bar" };
                Assert.Equal("foo", message.Execution.ID);
                Assert.Equal("bar", message.Execution.RunID);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowExecuteChildReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(666, message.ChildId);
                Assert.Equal("foo", message.Execution.ID);
                Assert.Equal("bar", message.Execution.RunID);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(666, message.ChildId);
                Assert.Equal("foo", message.Execution.ID);
                Assert.Equal("bar", message.Execution.RunID);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(666, message.ChildId);
                Assert.Equal("foo", message.Execution.ID);
                Assert.Equal("bar", message.Execution.RunID);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ChildId);

                // Round-trip

                message.RequestId = 555;
                message.ChildId = 666;
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowWaitForChildRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Result);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);
                message.Result = new byte[] { 0, 1, 2, 3, 4 };
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowWaitForChildReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ChildId);
                Assert.Null(message.SignalName);
                Assert.Null(message.SignalArgs);

                // Round-trip

                message.RequestId = 555;
                message.ChildId = 666;
                message.SignalName = "my-signal";
                message.SignalArgs = new byte[] { 0, 1, 2, 3, 4 };
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalChildRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Result);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);
                message.Result = new byte[] { 0, 1, 2, 3, 4 };
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalChildReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ChildId);

                // Round-trip

                message.RequestId = 555;
                message.ChildId = 666;
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowCancelChildRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ChildId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowCancelChildReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.QueryName);

                // Round-trip

                message.RequestId = 555;
                message.QueryName = "my-query";
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-query", message.QueryName);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSetQueryHandlerRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-query", message.QueryName);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-query", message.QueryName);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-query", message.QueryName);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSetQueryHandlerReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ContextId);
                Assert.Null(message.QueryName);
                Assert.Null(message.QueryArgs);
                Assert.Equal(InternalReplayStatus.Unspecified, message.ReplayStatus);

                // Round-trip

                message.RequestId = 555;
                message.ContextId = 666;
                message.QueryName = "my-query";
                message.QueryArgs = new byte[] { 0, 1, 2, 3, 4 };
                message.ReplayStatus = InternalReplayStatus.Replaying;
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-query", message.QueryName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.QueryArgs);
                Assert.Equal(InternalReplayStatus.Replaying, message.ReplayStatus);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueryInvokeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-query", message.QueryName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.QueryArgs);
                Assert.Equal(InternalReplayStatus.Replaying, message.ReplayStatus);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-query", message.QueryName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.QueryArgs);
                Assert.Equal(InternalReplayStatus.Replaying, message.ReplayStatus);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-query", message.QueryName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.QueryArgs);
                Assert.Equal(InternalReplayStatus.Replaying, message.ReplayStatus);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Result);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);
                message.Result = new byte[] { 0, 1, 2, 3, 4 };

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueryInvokeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ContextId);
                Assert.Null(message.ChangeId);
                Assert.Equal(0, message.MaxSupported);
                Assert.Equal(0, message.MinSupported);

                // Round-trip

                message.RequestId = 555;
                message.ContextId = 666;
                message.ChangeId = "my-change";
                message.MinSupported = 10;
                message.MaxSupported = 20;
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
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-change", message.ChangeId);
                Assert.Equal(10, message.MinSupported);
                Assert.Equal(20, message.MaxSupported);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-change", message.ChangeId);
                Assert.Equal(10, message.MinSupported);
                Assert.Equal(20, message.MaxSupported);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
                Assert.Equal("my-change", message.ChangeId);
                Assert.Equal(10, message.MinSupported);
                Assert.Equal(20, message.MaxSupported);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Equal(0, message.Version);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);
                message.Version = 20;
                Assert.Equal(20, message.Version);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowGetVersionReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(20, message.Version);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(20, message.Version);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(20, message.Version);
            }
        }
    }
}

