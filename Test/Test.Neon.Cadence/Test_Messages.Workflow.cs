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

                Assert.Equal(MessageTypes.WorkflowRegisterReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowRegisterRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.RequestId = 555;
                message.Name = "Foo";
                Assert.Equal(555, message.RequestId);
                Assert.Equal("Foo", message.Name);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowRegisterRequest>(stream, ignoreTypeCode: true);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowRegisterReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowRegisterReply>(stream, ignoreTypeCode: true);
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

                Assert.Equal(MessageTypes.WorkflowExecuteReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowExecuteRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.WorkflowContextId);
                Assert.Null(message.Domain);
                Assert.Null(message.Workflow);
                Assert.Null(message.Args);
                Assert.Null(message.Options);

                // Round-trip

                message.RequestId = 555;
                message.WorkflowContextId = 666;
                message.Domain = "my-domain";
                message.Workflow = "Foo";
                message.Args = new byte[] { 0, 1, 2, 3, 4 };
                message.Options = new InternalStartWorkflowOptions() { TaskList = "my-list", ExecutionStartToCloseTimeout = GoTimeSpan.Parse("100s").Ticks };
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkflowContextId);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("Foo", message.Workflow);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-list", message.Options.TaskList);
                Assert.Equal(GoTimeSpan.Parse("100s").Ticks, message.Options.ExecutionStartToCloseTimeout);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowExecuteRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkflowContextId);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("Foo", message.Workflow);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-list", message.Options.TaskList);
                Assert.Equal(GoTimeSpan.Parse("100s").Ticks, message.Options.ExecutionStartToCloseTimeout);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkflowContextId);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("Foo", message.Workflow);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-list", message.Options.TaskList);
                Assert.Equal(GoTimeSpan.Parse("100s").Ticks, message.Options.ExecutionStartToCloseTimeout);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkflowContextId);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowExecuteReply>(stream, ignoreTypeCode: true);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowExecuteReply>(stream, ignoreTypeCode: true);
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

                Assert.Equal(MessageTypes.WorkflowInvokeReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowInvokeRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.WorkflowContextId);
                Assert.Null(message.Name);
                Assert.Null(message.Args);
                Assert.Null(message.Domain);
                Assert.Null(message.WorkflowId);
                Assert.Null(message.RunId);
                Assert.Null(message.WorkflowType);
                Assert.Null(message.TaskList);
                Assert.Equal(TimeSpan.Zero, message.ExecutionStartToCloseTimeout);

                // Round-trip

                message.RequestId = 555;
                message.WorkflowContextId = 666;
                message.Name = "Foo";
                message.Args = new byte[] { 0, 1, 2, 3, 4 };
                message.Domain = "my-domain";
                message.WorkflowId = "my-workflowid";
                message.RunId = "my-runid";
                message.WorkflowType = "my-workflowtype";
                message.ExecutionStartToCloseTimeout = TimeSpan.FromDays(1);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkflowContextId);
                Assert.Equal("Foo", message.Name);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("my-workflowid", message.WorkflowId);
                Assert.Equal("my-runid", message.RunId);
                Assert.Equal("my-workflowtype", message.WorkflowType);
                Assert.Equal(TimeSpan.FromDays(1), message.ExecutionStartToCloseTimeout);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowInvokeRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkflowContextId);
                Assert.Equal("Foo", message.Name);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("my-workflowid", message.WorkflowId);
                Assert.Equal("my-runid", message.RunId);
                Assert.Equal("my-workflowtype", message.WorkflowType);
                Assert.Equal(TimeSpan.FromDays(1), message.ExecutionStartToCloseTimeout);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkflowContextId);
                Assert.Equal("Foo", message.Name);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("my-workflowid", message.WorkflowId);
                Assert.Equal("my-runid", message.RunId);
                Assert.Equal("my-workflowtype", message.WorkflowType);
                Assert.Equal(TimeSpan.FromDays(1), message.ExecutionStartToCloseTimeout);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkflowContextId);
                Assert.Equal("Foo", message.Name);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Args);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("my-workflowid", message.WorkflowId);
                Assert.Equal("my-runid", message.RunId);
                Assert.Equal("my-workflowtype", message.WorkflowType);
                Assert.Equal(TimeSpan.FromDays(1), message.ExecutionStartToCloseTimeout);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowInvokeReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Result);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);
                message.Result = new byte[] { 0, 1, 2, 3, 4 };
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Result);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowInvokeReply>(stream, ignoreTypeCode: true);
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
        public void Test_WorkflowCancelRequest()
        {
            WorkflowCancelRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowCancelRequest();

                Assert.Equal(MessageTypes.WorkflowCancelReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowCancelRequest>(stream, ignoreTypeCode: true);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowCancelRequest>(stream, ignoreTypeCode: true);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowCancelReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowCancelReply>(stream, ignoreTypeCode: true);
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

                Assert.Equal(MessageTypes.WorkflowTerminateReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowTerminateRequest>(stream, ignoreTypeCode: true);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowTerminateRequest>(stream, ignoreTypeCode: true);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowTerminateReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowTerminateReply>(stream, ignoreTypeCode: true);
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

                Assert.Equal(MessageTypes.WorkflowSignalReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalRequest>(stream, ignoreTypeCode: true);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalRequest>(stream, ignoreTypeCode: true);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalReply>(stream, ignoreTypeCode: true);
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

                Assert.Equal(MessageTypes.WorkflowSignalWithStartReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalWithStartRequest>(stream, ignoreTypeCode: true);
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
                message.Options = new InternalStartWorkflowOptions() { TaskList = "my-tasklist", WorkflowIdReusePolicy = (int)WorkflowIdReusePolicy.WorkflowIDReusePolicyAllowDuplicate };
                message.WorkflowArgs = new byte[] { 5, 6, 7, 8, 9 };
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-workflow", message.Workflow);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);
                Assert.Equal("my-tasklist", message.Options.TaskList);
                Assert.Equal((int)WorkflowIdReusePolicy.WorkflowIDReusePolicyAllowDuplicate, message.Options.WorkflowIdReusePolicy);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.WorkflowArgs);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalWithStartRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-workflow", message.Workflow);
                Assert.Equal("666", message.WorkflowId);
                Assert.Equal("my-signal", message.SignalName);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.SignalArgs);
                Assert.Equal("my-tasklist", message.Options.TaskList);
                Assert.Equal((int)WorkflowIdReusePolicy.WorkflowIDReusePolicyAllowDuplicate, message.Options.WorkflowIdReusePolicy);
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
                Assert.Equal((int)WorkflowIdReusePolicy.WorkflowIDReusePolicyAllowDuplicate, message.Options.WorkflowIdReusePolicy);
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
                Assert.Equal((int)WorkflowIdReusePolicy.WorkflowIDReusePolicyAllowDuplicate, message.Options.WorkflowIdReusePolicy);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalWithStartReply>(stream, ignoreTypeCode: true);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSignalWithStartReply>(stream, ignoreTypeCode: true);
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

                Assert.Equal(MessageTypes.WorkflowQueryReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueryRequest>(stream, ignoreTypeCode: true);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueryRequest>(stream, ignoreTypeCode: true);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueryReply>(stream, ignoreTypeCode: true);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowQueryReply>(stream, ignoreTypeCode: true);
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
        public void Test_WorkflowMutableRequest()
        {
            WorkflowMutableRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowMutableRequest();

                Assert.Equal(MessageTypes.WorkflowMutableReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowMutableRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.WorkflowContextId);
                Assert.Null(message.MutableId);

                // Round-trip

                message.RequestId = 555;
                message.WorkflowContextId = 666;
                message.MutableId = "my-mutable";
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkflowContextId);
                Assert.Equal("my-mutable", message.MutableId);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowMutableRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkflowContextId);
                Assert.Equal("my-mutable", message.MutableId);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkflowContextId);
                Assert.Equal("my-mutable", message.MutableId);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkflowContextId);
                Assert.Equal("my-mutable", message.MutableId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Test_WorkflowMutableReply()
        {
            WorkflowMutableReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowMutableReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowMutableReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Result);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);
                message.WorkflowContextId = 666;
                Assert.Equal(666, message.WorkflowContextId);
                message.Result = new byte[] { 0, 1, 2, 3, 4 };

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowMutableReply>(stream, ignoreTypeCode: true);
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
        public void Test_WorkflowMutableInvokeRequest()
        {
            WorkflowMutableInvokeRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowMutableInvokeRequest();

                Assert.Equal(MessageTypes.WorkflowMutableInvokeReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowMutableInvokeRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.MutableId);

                // Round-trip

                message.RequestId = 555;
                message.MutableId = "my-mutable";
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-mutable", message.MutableId);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowMutableInvokeRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-mutable", message.MutableId);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-mutable", message.MutableId);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-mutable", message.MutableId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Test_WorkflowMutableInvokeReply()
        {
            WorkflowMutableInvokeReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowMutableInvokeReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowMutableInvokeReply>(stream, ignoreTypeCode: true);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowMutableInvokeReply>(stream, ignoreTypeCode: true);
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

                Assert.Equal(MessageTypes.WorkflowDescribeExecutionReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowDescribeExecutionRequest>(stream, ignoreTypeCode: true);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowDescribeExecutionRequest>(stream, ignoreTypeCode: true);
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
                    TaskList                       = new InternalTaskList() { Name = "my-tasklist", TaskListKind = (int)TaskListKind.Sticky },
                    ExecutionStartToCloseTimeout   = 1000,
                    TaskStartToCloseTimeoutSeconds = 2000,
                    ChildPolicy                    = (int)ChildWorkflowPolicy.ChildWorkflowPolicyRequestCancel
                },

                WorkflowExecutionInfo = new InternalWorkflowExecutionInfo()
                {
                    Execution = new InternalWorkflowExecution()
                    {
                        ID = "workflow-id",
                        RunID = "run-id",
                    },

                    WorkflowType        = new InternalWorkflowType() { Name = "my-workflow" },
                    StartTime           = 3000,
                    CloseTime           = 4000,
                    WorkflowCloseStatus = (int)WorkflowCloseStatus.Restarted,
                    HistoryLength       = 5000,
                    ParentDomainId      = "parent-domain",
                    ParentExecution     = new InternalWorkflowExecution() { ID = "parent-id", RunID = "parent-runid" },
                    ExecutionTime       = 6000,

                    Memo = new InternalMemo()
                    {
                        Fields = new Dictionary<string, byte[]>() { { "foo", new byte[] { 0, 1, 2, 3, 4 } } }
                    },

                    AutoResetPoints = new InternalResetPoints()
                    {
                        Points = new List<InternalResetPointInfo>()
                         {
                             new InternalResetPointInfo()
                             {
                                BinaryChecksum           = "my-checksum",
                                CreatedTimeNano          = 7000,
                                ExpiringTimeNano         = 8000,
                                FirstDecisionCompletedId = 9000,
                                Resettable               = true,
                                RunId                    = "my-runid"
                             }
                         }
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
                        State                  = (int)ActivityState.Started
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

            Assert.NotNull(info.AutoResetPoints);
            Assert.NotNull(info.AutoResetPoints.Points);
            Assert.Equal(expected.WorkflowExecutionInfo.AutoResetPoints.Points.Count, info.AutoResetPoints.Points.Count);

            for (int i = 0; i < expected.WorkflowExecutionInfo.AutoResetPoints.Points.Count; i++)
            {
                var refPoint = expected.WorkflowExecutionInfo.AutoResetPoints.Points.ToArray()[i];
                var point    = info.AutoResetPoints.Points.ToArray()[i];

                Assert.Equal(refPoint.BinaryChecksum, point.BinaryChecksum);
                Assert.Equal(refPoint.CreatedTimeNano, point.CreatedTimeNano);
                Assert.Equal(refPoint.ExpiringTimeNano, point.ExpiringTimeNano);
                Assert.Equal(refPoint.FirstDecisionCompletedId, point.FirstDecisionCompletedId);
                Assert.Equal(refPoint.Resettable, point.Resettable);
                Assert.Equal(refPoint.RunId, point.RunId);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowDescribeExecutionReply>(stream, ignoreTypeCode: true);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowDescribeExecutionReply>(stream, ignoreTypeCode: true);
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
    }
}
