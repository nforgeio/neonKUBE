//-----------------------------------------------------------------------------
// FILE:        Test_Messages.Cluster.cs
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
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Temporal;
using Neon.Temporal.Internal;
using Neon.Xunit;
using Neon.Xunit.Temporal;

using Newtonsoft.Json;
using Test.Neon.Models;
using Xunit;

namespace TestTemporal
{
    public partial class Test_Messages
    {
        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_InitializeRequest()
        {
            InitializeRequest message;

            using (var stream = new MemoryStream())
            {
                message = new InitializeRequest();

                Assert.Equal(InternalMessageTypes.InitializeReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<InitializeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.LibraryAddress);
                Assert.Equal(0, message.LibraryPort);
                Assert.Equal(LogLevel.None, message.LogLevel);

                // Round-trip

                message.ClientId       = 444;
                message.RequestId      = 555;
                message.LibraryAddress = "1.2.3.4";
                message.LibraryPort    = 666;
                message.LogLevel       = LogLevel.Info;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.2.3.4", message.LibraryAddress);
                Assert.Equal(666, message.LibraryPort);
                Assert.Equal(LogLevel.Info, message.LogLevel);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<InitializeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.2.3.4", message.LibraryAddress);
                Assert.Equal(666, message.LibraryPort);
                Assert.Equal(LogLevel.Info, message.LogLevel);

                // Clone()

                message = (InitializeRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.2.3.4", message.LibraryAddress);
                Assert.Equal(666, message.LibraryPort);
                Assert.Equal(LogLevel.Info, message.LogLevel);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.2.3.4", message.LibraryAddress);
                Assert.Equal(666, message.LibraryPort);
                Assert.Equal(LogLevel.Info, message.LogLevel);
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_InitializeReply()
        {
            InitializeReply message;

            using (var stream = new MemoryStream())
            {
                message = new InitializeReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<InitializeReply>(stream);
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

                message = ProxyMessage.Deserialize<InitializeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (InitializeReply)message.Clone();
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

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_ConnectRequest()
        {
            ConnectRequest message;

            using (var stream = new MemoryStream())
            {
                message = new ConnectRequest();

                Assert.Equal(InternalMessageTypes.ConnectReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ConnectRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.HostPort);
                Assert.Null(message.Identity);
                Assert.Null(message.Namespace);
                Assert.False(message.CreateNamespace);
                Assert.Null(message.Namespace);
                Assert.Equal(TimeSpan.Zero, message.ClientTimeout);
                Assert.Equal(0, message.Retries);
                Assert.Equal(TimeSpan.Zero, message.RetryDelay);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.HostPort = "127.0.0.1:7233";
                message.Identity = "my-identity";
                message.ClientTimeout = TimeSpan.FromSeconds(30);
                message.Namespace = "my-namespace";
                message.CreateNamespace = true;
                message.Namespace = "my-namespace";
                message.Retries = 3;
                message.RetryDelay = TimeSpan.FromSeconds(2);

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("127.0.0.1:7233", message.HostPort);
                Assert.Equal("my-identity", message.Identity);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.True(message.CreateNamespace);
                Assert.Equal(3, message.Retries);
                Assert.Equal(TimeSpan.FromSeconds(2), message.RetryDelay);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ConnectRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("127.0.0.1:7233", message.HostPort);
                Assert.Equal("my-identity", message.Identity);
                Assert.Equal(TimeSpan.FromSeconds(30), message.ClientTimeout);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.True(message.CreateNamespace);
                Assert.Equal(3, message.Retries);
                Assert.Equal(TimeSpan.FromSeconds(2), message.RetryDelay);

                // Clone()

                message = (ConnectRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("127.0.0.1:7233", message.HostPort);
                Assert.Equal("my-identity", message.Identity);
                Assert.Equal(TimeSpan.FromSeconds(30), message.ClientTimeout);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.True(message.CreateNamespace);
                Assert.Equal(3, message.Retries);
                Assert.Equal(TimeSpan.FromSeconds(2), message.RetryDelay);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("127.0.0.1:7233", message.HostPort);
                Assert.Equal("my-identity", message.Identity);
                Assert.Equal(TimeSpan.FromSeconds(30), message.ClientTimeout);
                Assert.Equal("my-namespace", message.Namespace);
                Assert.True(message.CreateNamespace);
                Assert.Equal(3, message.Retries);
                Assert.Equal(TimeSpan.FromSeconds(2), message.RetryDelay);
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_ConnectReply()
        {
            ConnectReply message;

            using (var stream = new MemoryStream())
            {
                message = new ConnectReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ConnectReply>(stream);
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

                message = ProxyMessage.Deserialize<ConnectReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Clone()

                message = (ConnectReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_NamespaceDescribeRequest()
        {
            NamespaceDescribeRequest message;

            using (var stream = new MemoryStream())
            {
                message = new NamespaceDescribeRequest();

                Assert.Equal(InternalMessageTypes.NamespaceDescribeReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NamespaceDescribeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Name);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Name = "my-namespace";

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-namespace", message.Name);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NamespaceDescribeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-namespace", message.Name);

                // Clone()

                message = (NamespaceDescribeRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-namespace", message.Name);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-namespace", message.Name);
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_NamespaceDescribeReply()
        {
            NamespaceDescribeReply message;

            using (var stream = new MemoryStream())
            {
                message = new NamespaceDescribeReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NamespaceDescribeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.False(message.IsGlobalNamespace);
                Assert.Null(message.NamespaceInfo);
                Assert.Null(message.NamespaceConfig);
                Assert.Null(message.NamespaceReplicationConfig);
                Assert.Equal((Int64)0, message.FailoverVersion);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");
                message.IsGlobalNamespace = true;
                message.FailoverVersion = 1;
                message.NamespaceInfo = GetTestNamespaceInfo();
                message.NamespaceReplicationConfig = GetTestNamespaceReplicationConfig();
                message.NamespaceConfig = GetTestNamespaceConfig();

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.IsGlobalNamespace);
                Assert.Equal((Int64)1, message.FailoverVersion);
                ValidateTestNamespaceConfig(message.NamespaceConfig);
                ValidateTestNamespaceReplicationConfig(message.NamespaceReplicationConfig);
                ValidateTestNamespaceInfo(message.NamespaceInfo);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NamespaceDescribeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.IsGlobalNamespace);
                Assert.Equal((Int64)1, message.FailoverVersion);
                ValidateTestNamespaceConfig(message.NamespaceConfig);
                ValidateTestNamespaceReplicationConfig(message.NamespaceReplicationConfig);
                ValidateTestNamespaceInfo(message.NamespaceInfo);

                // Clone()

                message = (NamespaceDescribeReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.IsGlobalNamespace);
                Assert.Equal((Int64)1, message.FailoverVersion);
                ValidateTestNamespaceConfig(message.NamespaceConfig);
                ValidateTestNamespaceReplicationConfig(message.NamespaceReplicationConfig);
                ValidateTestNamespaceInfo(message.NamespaceInfo);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.IsGlobalNamespace);
                Assert.Equal((Int64)1, message.FailoverVersion);
                ValidateTestNamespaceConfig(message.NamespaceConfig);
                ValidateTestNamespaceReplicationConfig(message.NamespaceReplicationConfig);
                ValidateTestNamespaceInfo(message.NamespaceInfo);
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_NamespaceRegisterRequest()
        {
            NamespaceRegisterRequest message;

            using (var stream = new MemoryStream())
            {
                message = new NamespaceRegisterRequest();

                Assert.Equal(InternalMessageTypes.NamespaceRegisterReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NamespaceRegisterRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Name);
                Assert.Null(message.Description);
                Assert.Null(message.OwnerEmail);
                Assert.Equal(TimeSpan.Zero, message.WorkflowExecutionRetentionPeriod);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Name = "my-namespace";
                message.Description = "my-description";
                message.OwnerEmail = "my-email";
                message.WorkflowExecutionRetentionPeriod = TimeSpan.FromDays(14);

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-namespace", message.Name);
                Assert.Equal("my-description", message.Description);
                Assert.Equal("my-email", message.OwnerEmail);
                Assert.Equal(TimeSpan.FromDays(14), message.WorkflowExecutionRetentionPeriod);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NamespaceRegisterRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-namespace", message.Name);
                Assert.Equal("my-description", message.Description);
                Assert.Equal("my-email", message.OwnerEmail);
                Assert.Equal(TimeSpan.FromDays(14), message.WorkflowExecutionRetentionPeriod);

                // Clone()

                message = (NamespaceRegisterRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-namespace", message.Name);
                Assert.Equal("my-description", message.Description);
                Assert.Equal("my-email", message.OwnerEmail);
                Assert.Equal(TimeSpan.FromDays(14), message.WorkflowExecutionRetentionPeriod);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-namespace", message.Name);
                Assert.Equal("my-description", message.Description);
                Assert.Equal("my-email", message.OwnerEmail);
                Assert.Equal(TimeSpan.FromDays(14), message.WorkflowExecutionRetentionPeriod);
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_NamespaceRegisterReply()
        {
            NamespaceRegisterReply message;

            using (var stream = new MemoryStream())
            {
                message = new NamespaceRegisterReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NamespaceRegisterReply>(stream);
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

                message = ProxyMessage.Deserialize<NamespaceRegisterReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (NamespaceRegisterReply)message.Clone();
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

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_NamespaceUpdateRequest()
        {
            NamespaceUpdateRequest message;

            using (var stream = new MemoryStream())
            {
                message = new NamespaceUpdateRequest();

                Assert.Equal(InternalMessageTypes.NamespaceUpdateReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NamespaceUpdateRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Name);
                Assert.Null(message.UpdateNamespaceInfo);
                Assert.Null(message.NamespaceConfig);
                Assert.Null(message.NamespaceReplicationConfig);
                Assert.Null(message.SecurityToken);
                Assert.Null(message.DeleteBadBinary);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Name = "my-namespace";
                message.UpdateNamespaceInfo = GetTestUpdateNamespaceInfo();
                message.NamespaceReplicationConfig = GetTestNamespaceReplicationConfig();
                message.NamespaceConfig = GetTestNamespaceConfig();
                message.SecurityToken = "my-token";
                message.DeleteBadBinary = "bad-binary";

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-namespace", message.Name);
                Assert.Equal("my-token", message.SecurityToken);
                Assert.Equal("bad-binary", message.DeleteBadBinary);
                ValidateTestNamespaceConfig(message.NamespaceConfig);
                ValidateTestNamespaceReplicationConfig(message.NamespaceReplicationConfig);
                ValidateTestUpdateNamespaceInfo(message.UpdateNamespaceInfo);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NamespaceUpdateRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-namespace", message.Name);
                Assert.Equal("my-token", message.SecurityToken);
                Assert.Equal("bad-binary", message.DeleteBadBinary);
                ValidateTestNamespaceConfig(message.NamespaceConfig);
                ValidateTestNamespaceReplicationConfig(message.NamespaceReplicationConfig);
                ValidateTestUpdateNamespaceInfo(message.UpdateNamespaceInfo);

                // Clone()

                message = (NamespaceUpdateRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-namespace", message.Name);
                Assert.Equal("my-token", message.SecurityToken);
                Assert.Equal("bad-binary", message.DeleteBadBinary);
                ValidateTestNamespaceConfig(message.NamespaceConfig);
                ValidateTestNamespaceReplicationConfig(message.NamespaceReplicationConfig);
                ValidateTestUpdateNamespaceInfo(message.UpdateNamespaceInfo);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-namespace", message.Name);
                Assert.Equal("my-token", message.SecurityToken);
                Assert.Equal("bad-binary", message.DeleteBadBinary);
                ValidateTestNamespaceConfig(message.NamespaceConfig);
                ValidateTestNamespaceReplicationConfig(message.NamespaceReplicationConfig);
                ValidateTestUpdateNamespaceInfo(message.UpdateNamespaceInfo);
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_NamespaceUpdateReply()
        {
            NamespaceUpdateReply message;

            using (var stream = new MemoryStream())
            {
                message = new NamespaceUpdateReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NamespaceUpdateReply>(stream);
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

                message = ProxyMessage.Deserialize<NamespaceUpdateReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (NamespaceUpdateReply)message.Clone();
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

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_TerminateRequest()
        {
            TerminateRequest message;

            using (var stream = new MemoryStream())
            {
                message = new TerminateRequest();

                Assert.Equal(InternalMessageTypes.TerminateReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<TerminateRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;

                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<TerminateRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Clone()

                message = (TerminateRequest)message.Clone();
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

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_TerminateReply()
        {
            TerminateReply message;

            using (var stream = new MemoryStream())
            {
                message = new TerminateReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<TerminateReply>(stream);
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

                message = ProxyMessage.Deserialize<TerminateReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (TerminateReply)message.Clone();
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

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_HeartbeatRequest()
        {
            HeartbeatRequest message;

            using (var stream = new MemoryStream())
            {
                message = new HeartbeatRequest();

                Assert.Equal(InternalMessageTypes.HeartbeatReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<HeartbeatRequest>(stream);
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

                message = ProxyMessage.Deserialize<HeartbeatRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Clone()

                message = (HeartbeatRequest)message.Clone();
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

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_HeartbeatReply()
        {
            HeartbeatReply message;

            using (var stream = new MemoryStream())
            {
                message = new HeartbeatReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<HeartbeatReply>(stream);
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

                message = ProxyMessage.Deserialize<HeartbeatReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (HeartbeatReply)message.Clone();
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

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_CancelRequest()
        {
            CancelRequest message;

            using (var stream = new MemoryStream())
            {
                message = new CancelRequest();

                Assert.Equal(InternalMessageTypes.CancelReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<CancelRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.TargetRequestId = 666;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.TargetRequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<CancelRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.TargetRequestId);

                // Clone()

                message = (CancelRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.TargetRequestId);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.TargetRequestId);
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_CancelReply()
        {
            CancelReply message;

            using (var stream = new MemoryStream())
            {
                message = new CancelReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<CancelReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new TemporalError("MyError");
                message.WasCancelled = true;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.WasCancelled);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<CancelReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.WasCancelled);

                // Clone()

                message = (CancelReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.WasCancelled);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.True(message.WasCancelled);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_NewWorkerRequest()
        {
            NewWorkerRequest message;

            using (var stream = new MemoryStream())
            {
                message = new NewWorkerRequest();

                Assert.Equal(InternalMessageTypes.NewWorkerReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NewWorkerRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.WorkerId);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Options);

                // Round-trip

                message.ClientId  = 444;
                message.RequestId = 555;
                message.WorkerId  = 666;
                message.Options   = new WorkerOptions() 
                { 
                    MaxConcurrentActivityExecutionSize = 1234 
                };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);
                Assert.Equal(1234, message.Options.MaxConcurrentActivityExecutionSize);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NewWorkerRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);
                Assert.Equal(1234, message.Options.MaxConcurrentActivityExecutionSize);

                // Clone()

                message = (NewWorkerRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);
                Assert.Equal(1234, message.Options.MaxConcurrentActivityExecutionSize);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);
                Assert.Equal(1234, message.Options.MaxConcurrentActivityExecutionSize);
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_NewWorkerReply()
        {
            NewWorkerReply message;

            using (var stream = new MemoryStream())
            {
                message = new NewWorkerReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NewWorkerReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Equal(0, message.WorkerId);

                // Round-trip

                message.ClientId  = 444;
                message.RequestId = 555;
                message.WorkerId  = 666;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NewWorkerReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);

                // Clone()

                message = (NewWorkerReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);
            }
        }

        
        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_StartWorkerRequest()
        {
            StartWorkerRequest message;

            using (var stream = new MemoryStream())
            {
                message = new StartWorkerRequest();

                Assert.Equal(InternalMessageTypes.StartWorkerReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<StartWorkerRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.WorkerId);

                // Round-trip

                message.ClientId  = 444;
                message.RequestId = 555;
                message.WorkerId  = 666;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<StartWorkerRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);

                // Clone()

                message = (StartWorkerRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_StartWorkerReply()
        {
            StartWorkerReply message;

            using (var stream = new MemoryStream())
            {
                message = new StartWorkerReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<StartWorkerReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<StartWorkerReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Clone()

                message = (StartWorkerReply)message.Clone();
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

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_StopWorkerRequest()
        {
            StopWorkerRequest message;

            using (var stream = new MemoryStream())
            {
                message = new StopWorkerRequest();

                Assert.Equal(InternalMessageTypes.StopWorkerReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<StopWorkerRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.WorkerId);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.WorkerId = 666;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<StopWorkerRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);

                // Clone()

                message = (StopWorkerRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_StopWorkerReply()
        {
            StopWorkerReply message;

            using (var stream = new MemoryStream())
            {
                message = new StopWorkerReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<StopWorkerReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<StopWorkerReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Clone()

                message = (StopWorkerReply)message.Clone();
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

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_PingRequest()
        {
            PingRequest message;

            using (var stream = new MemoryStream())
            {
                message = new PingRequest();

                Assert.Equal(InternalMessageTypes.PingReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<PingRequest>(stream);
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

                message = ProxyMessage.Deserialize<PingRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Clone()

                message = (PingRequest)message.Clone();
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

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_PingReply()
        {
            PingReply message;

            using (var stream = new MemoryStream())
            {
                message = new PingReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<PingReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<PingReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Clone()

                message = (PingReply)message.Clone();
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

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_SetWorkflowCacheSizeRequest()
        {
            WorkflowSetCacheSizeRequest message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowSetCacheSizeRequest();

                Assert.Equal(InternalMessageTypes.WorkflowSetCacheSizeReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSetCacheSizeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.Size);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Size = 20000;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(20000, message.Size);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSetCacheSizeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(20000, message.Size);

                // Clone()

                message = (WorkflowSetCacheSizeRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(20000, message.Size);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(20000, message.Size);
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_SetWorkflowCacheReply()
        {
            WorkflowSetCacheSizeReply message;

            using (var stream = new MemoryStream())
            {
                message = new WorkflowSetCacheSizeReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSetCacheSizeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSetCacheSizeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);

                // Clone()

                message = (WorkflowSetCacheSizeReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_NamespaceDeprecateRequest()
        {
            NamespaceDeprecateRequest message;

            using (var stream = new MemoryStream())
            {
                message = new NamespaceDeprecateRequest();

                Assert.Equal(InternalMessageTypes.NamespaceDeprecateReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NamespaceDeprecateRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Name);
                Assert.Null(message.SecurityToken);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Name = "my-namespace";
                message.SecurityToken = "my-token";

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-namespace", message.Name);
                Assert.Equal("my-token", message.SecurityToken);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NamespaceDeprecateRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-namespace", message.Name);
                Assert.Equal("my-token", message.SecurityToken);

                // Clone()

                message = (NamespaceDeprecateRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-namespace", message.Name);
                Assert.Equal("my-token", message.SecurityToken);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-namespace", message.Name);
                Assert.Equal("my-token", message.SecurityToken);
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_NamespaceDeprecateReply()
        {
            NamespaceDeprecateReply message;

            using (var stream = new MemoryStream())
            {
                message = new NamespaceDeprecateReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NamespaceDeprecateReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NamespaceDeprecateReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Clone()

                message = (NamespaceDeprecateReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_DisconnectRequest()
        {
            DisconnectRequest message;

            using (var stream = new MemoryStream())
            {
                message = new DisconnectRequest();

                Assert.Equal(InternalMessageTypes.DisconnectReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DisconnectRequest>(stream);
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

                message = ProxyMessage.Deserialize<DisconnectRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Clone()

                message = (DisconnectRequest)message.Clone();
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
    
        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_DisconnectReply()
        {
            DisconnectReply message;

            using (var stream = new MemoryStream())
            {
                message = new DisconnectReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DisconnectReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DisconnectReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Clone()

                message = (DisconnectReply)message.Clone();
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

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_LogRequest()
        {
            LogRequest message;

            using (var stream = new MemoryStream())
            {
                message = new LogRequest();

                Assert.Equal(InternalMessageTypes.LogReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<LogRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(DateTime.MinValue, message.TimeUtc);
                Assert.Equal(Neon.Diagnostics.LogLevel.None, message.LogLevel);
                Assert.False(message.FromTemporal);
                Assert.Null(message.LogMessage);

                // Round-trip

                message.ClientId     = 444;
                message.RequestId    = 555;
                message.TimeUtc      = new DateTime(2019, 8, 27);
                message.FromTemporal = true;
                message.LogLevel     = Neon.Diagnostics.LogLevel.Info;
                message.LogMessage   = "Hello World!";

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(new DateTime(2019, 8, 27), message.TimeUtc);
                Assert.True(message.FromTemporal);
                Assert.Equal(Neon.Diagnostics.LogLevel.Info, message.LogLevel);
                Assert.Equal("Hello World!", message.LogMessage);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<LogRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(new DateTime(2019, 8, 27), message.TimeUtc);
                Assert.True(message.FromTemporal);
                Assert.Equal(Neon.Diagnostics.LogLevel.Info, message.LogLevel);
                Assert.Equal("Hello World!", message.LogMessage);

                // Clone()

                message = (LogRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(new DateTime(2019, 8, 27), message.TimeUtc);
                Assert.True(message.FromTemporal);
                Assert.Equal(Neon.Diagnostics.LogLevel.Info, message.LogLevel);
                Assert.Equal("Hello World!", message.LogMessage);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(new DateTime(2019, 8, 27), message.TimeUtc);
                Assert.True(message.FromTemporal);
                Assert.Equal(Neon.Diagnostics.LogLevel.Info, message.LogLevel);
                Assert.Equal("Hello World!", message.LogMessage);
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_LogReply()
        {
            LogReply message;

            using (var stream = new MemoryStream())
            {
                message = new LogReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<LogReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId  = 444;
                message.RequestId = 555;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<LogReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Clone()

                message = (LogReply)message.Clone();
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

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_NamespaceListRequest()
        {
            NamespaceListRequest message;

            using (var stream = new MemoryStream())
            {
                message = new NamespaceListRequest();

                Assert.Equal(InternalMessageTypes.NamespaceListReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NamespaceListRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.PageSize);
                Assert.Null(message.NextPageToken);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.PageSize = 666;
                message.NextPageToken = new byte[] { 5, 6, 7, 8, 9 };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.PageSize);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.NextPageToken);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NamespaceListRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.PageSize);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.NextPageToken);

                // Clone()

                message = (NamespaceListRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.PageSize);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.NextPageToken);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.PageSize);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.NextPageToken);
            }
        }

        /// <summary>
        /// Returns a <see cref="UpdateNamespaceInfo"/> instance for testing purposes.
        /// </summary>
        /// <returns>The test info.</returns>
        private UpdateNamespaceInfo GetTestUpdateNamespaceInfo()
        {
            return new UpdateNamespaceInfo()
            {
                Description = "Test domain",
                OwnerEmail = "jeff@lilltek.com",
                Data = new Dictionary<string, string>() { { "test", "value" } }
            };
        }

        /// <summary>
        /// Validates a <see cref="UpdateNamespaceInfo"/> instance for testing purposes.
        /// </summary>
        /// <param name="info">The domain info.</param>
        private void ValidateTestUpdateNamespaceInfo(UpdateNamespaceInfo info)
        {
            Assert.NotNull(info);
            Assert.Equal("jeff@lilltek.com", info.OwnerEmail);
            Assert.Single(info.Data);
            Assert.Equal("test", info.Data.First().Key);
            Assert.Equal("value", info.Data.First().Value);
        }

        /// <summary>
        /// Returns a <see cref="NamespaceInfo"/> instance for testing purposes.
        /// </summary>
        /// <returns>The test info.</returns>
        private NamespaceInfo GetTestNamespaceInfo()
        {
            return new NamespaceInfo()
            {
                Name        = "my-namespace",
                State       = NamespaceState.Deprecated,
                Description = "Test domain",
                OwnerEmail  = "jeff@lilltek.com",
                Id          = "1111-2222-3333-4444",
                Data        = new Dictionary<string, string>() { { "test", "value" } }
            };
        }

        /// <summary>
        /// Validates a <see cref="NamespaceInfo"/> instance for testing purposes.
        /// </summary>
        /// <param name="info">The domain info.</param>
        private void ValidateTestNamespaceInfo(NamespaceInfo info)
        {
            Assert.NotNull(info);
            Assert.Equal("my-namespace", info.Name);
            Assert.Equal(NamespaceState.Deprecated, info.State);
            Assert.Equal("jeff@lilltek.com", info.OwnerEmail);
            Assert.Single(info.Data);
            Assert.Equal("test", info.Data.First().Key);
            Assert.Equal("value", info.Data.First().Value);
            Assert.Equal("1111-2222-3333-4444", info.Id);
        }

        /// <summary>
        /// Returns a <see cref="NamespaceReplicationConfig"/> instance for testing purposes.
        /// </summary>
        /// <returns>The test replication configuration.</returns>
        private NamespaceReplicationConfig GetTestNamespaceReplicationConfig()
        {
            return new NamespaceReplicationConfig
            {
                ActiveClusterName = "active cluster",
                Clusters          = new List<ClusterReplicationConfig>() 
                {
                    new ClusterReplicationConfig 
                    {
                        ClusterName = "cluster"
                    }
                }
            };
        }

        /// <summary>
        /// Validates a <see cref="NamespaceReplicationConfig"/> instance for testing purposes.
        /// </summary>
        /// <param name="config">The namespace replication config.</param>
        private void ValidateTestNamespaceReplicationConfig(NamespaceReplicationConfig config)
        {
            Assert.NotNull(config);
            Assert.Equal("active cluster", config.ActiveClusterName);
            Assert.Equal("cluster", config.Clusters.First().ClusterName);
        }

        /// <summary>
        /// Returns a <see cref="NamespaceConfig"/> for testing purposes.
        /// </summary>
        /// <returns></returns>
        private NamespaceConfig GetTestNamespaceConfig()
        {
            return new NamespaceConfig()
            {
                HistoryArchivalState          = ArchivalState.Enabled,
                VisibilityArchivalState       = ArchivalState.Disabled,
                VisibilityArchivalUri         = "uri",
                HistoryArchivalUri            = "uri",
                WorkflowExecutionRetentionTtl = TimeSpan.FromSeconds(5),
                BadBinaries = new BadBinaries
                {
                    Binaries = new Dictionary<string, BadBinaryInfo>()
                    {
                        {
                            "badbinary", new BadBinaryInfo
                            {
                                CreateTime = DateTime.Parse("08/18/2018 07:22:16"),
                                Operator   = "operator",
                                Reason     = "reason"
                            }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Validates a test <see cref="NamespaceConfig"/>.
        /// </summary>
        /// <param name="config">The domain config.</param>
        private void ValidateTestNamespaceConfig(NamespaceConfig config)
        {
            Assert.NotNull(config);
            Assert.Equal(ArchivalState.Enabled, config.HistoryArchivalState);
            Assert.Equal(ArchivalState.Disabled, config.VisibilityArchivalState);
            Assert.Equal("uri", config.HistoryArchivalUri);
            Assert.Equal("uri", config.VisibilityArchivalUri);
            Assert.Equal(TimeSpan.FromSeconds(5), config.WorkflowExecutionRetentionTtl);
            Assert.Equal(DateTime.Parse("08/18/2018 07:22:16"), config.BadBinaries.Binaries["badbinary"].CreateTime);
            Assert.Equal("operator", config.BadBinaries.Binaries["badbinary"].Operator);
            Assert.Equal("reason", config.BadBinaries.Binaries["badbinary"].Reason);
        }

        /// <summary>
        /// Returns a list of test namespace descriptions.
        /// </summary>
        private List<NamespaceDescription> GetTestNamespaceDescriptions()
        {
            var list = new List<NamespaceDescription>();

            list.Add(
                new NamespaceDescription()
                {
                    IsGlobalNamespace = true,
                    FailoverVersion   = 1,
                    Config     = new NamespaceConfig()
                    {
                        HistoryArchivalState          = ArchivalState.Enabled,
                        VisibilityArchivalUri         = "uri",
                        VisibilityArchivalState       = ArchivalState.Unspecified,
                        HistoryArchivalUri            = "uri",
                        WorkflowExecutionRetentionTtl = TimeSpan.FromSeconds(5),
                        BadBinaries                   = new BadBinaries
                        {
                            Binaries = new Dictionary<string, BadBinaryInfo>()
                            {
                                {
                                    "badbinary", new BadBinaryInfo
                                    {
                                        CreateTime = DateTime.Parse("08/18/2018 07:22:16"),
                                        Operator   = "operator",
                                        Reason     = "reason"
                                    }
                                }
                            }
                        }
                    },
                    NamespaceInfo = new NamespaceInfo()
                    {
                        Name        = "my-namespace",
                        State       = NamespaceState.Deprecated,
                        Description = "Test domain",
                        OwnerEmail  = "jeff@lilltek.com",
                        Id          = "1111-2222-3333-4444",
                        Data        = new Dictionary<string, string>() { { "test", "value" } }
                    }
                });

            return list;
        }

        /// <summary>
        /// Verifies that a test namespace description list is valid.
        /// </summary>
        /// <param name="domains">The test domains.</param>
        private void ValidateTestNamespaceDescriptions(List<NamespaceDescription> namespaces)
        {
            Assert.NotNull(namespaces);
            Assert.Single(namespaces);

            var ns = namespaces.First();

            Assert.True(ns.IsGlobalNamespace);
            Assert.Equal(1, ns.FailoverVersion);

            Assert.NotNull(ns.Config);
            Assert.Equal(ArchivalState.Enabled, ns.Config.HistoryArchivalState);
            Assert.Equal(ArchivalState.Unspecified, ns.Config.VisibilityArchivalState);
            Assert.Equal("uri", ns.Config.HistoryArchivalUri);
            Assert.Equal("uri", ns.Config.VisibilityArchivalUri);
            Assert.Equal(TimeSpan.FromSeconds(5), ns.Config.WorkflowExecutionRetentionTtl);
            Assert.Equal(DateTime.Parse("08/18/2018 07:22:16"), ns.Config.BadBinaries.Binaries["badbinary"].CreateTime);
            Assert.Equal("operator", ns.Config.BadBinaries.Binaries["badbinary"].Operator);
            Assert.Equal("reason", ns.Config.BadBinaries.Binaries["badbinary"].Reason);

            Assert.NotNull(ns.NamespaceInfo);
            Assert.Equal("my-namespace", ns.NamespaceInfo.Name);
            Assert.Equal(NamespaceState.Deprecated, ns.NamespaceInfo.State);
            Assert.Equal("jeff@lilltek.com", ns.NamespaceInfo.OwnerEmail);
            Assert.Single(ns.NamespaceInfo.Data);
            Assert.Equal("test", ns.NamespaceInfo.Data.First().Key);
            Assert.Equal("value", ns.NamespaceInfo.Data.First().Value);
            Assert.Equal("1111-2222-3333-4444", ns.NamespaceInfo.Id);
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_DescribeTaskQueueRequest()
        {
            DescribeTaskQueueRequest message;

            using (var stream = new MemoryStream())
            {
                message = new DescribeTaskQueueRequest();

                Assert.Equal(InternalMessageTypes.DescribeTaskQueueReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DescribeTaskQueueRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Name);
                Assert.Equal(default, message.TaskQueueType);

                // Round-trip

                message.ClientId      = 444;
                message.RequestId     = 555;
                message.Name          = "my-taskqueue";
                message.TaskQueueType = TaskQueueType.Activity;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-taskqueue", message.Name);
                Assert.Equal(TaskQueueType.Activity, message.TaskQueueType);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DescribeTaskQueueRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-taskqueue", message.Name);
                Assert.Equal(TaskQueueType.Activity, message.TaskQueueType);

                // Clone()

                message = (DescribeTaskQueueRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-taskqueue", message.Name);
                Assert.Equal(TaskQueueType.Activity, message.TaskQueueType);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-taskqueue", message.Name);
                Assert.Equal(TaskQueueType.Activity, message.TaskQueueType);
            }
        }

        [Fact(Timeout = TemporalTestHelper.TestTimeout)]
        [Trait(TestTrait.Area, TestArea.NeonTemporal)]
        public void Test_DescribeTaskQueueReply()
        {
            DescribeTaskQueueReply message;

            using (var stream = new MemoryStream())
            {
                message = new DescribeTaskQueueReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DescribeTaskQueueReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Result);

                // Round-trip

                var lastAccessTime = new DateTime(2020, 5, 22, 8, 46, 0);

                message.ClientId  = 444;
                message.RequestId = 555;
                message.Result =
                    new TaskQueueDescription()
                    {
                        Pollers = new List<PollerInfo>()
                        {
                             new PollerInfo()
                             {
                                 Identity       = "my-poller",
                                 LastAccessTime = lastAccessTime,
                                 RatePerSecond  = 666
                             }
                        }
                    };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.NotNull(message.Result);
                Assert.Single(message.Result.Pollers);
                Assert.Equal("my-poller", message.Result.Pollers.First().Identity);
                Assert.Equal(lastAccessTime, message.Result.Pollers.First().LastAccessTime);
                Assert.Equal(666, message.Result.Pollers.First().RatePerSecond);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DescribeTaskQueueReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.NotNull(message.Result);
                Assert.Single(message.Result.Pollers);
                Assert.Equal("my-poller", message.Result.Pollers.First().Identity);
                Assert.Equal(lastAccessTime, message.Result.Pollers.First().LastAccessTime);
                Assert.Equal(666, message.Result.Pollers.First().RatePerSecond);

                // Clone()

                message = (DescribeTaskQueueReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.NotNull(message.Result);
                Assert.Single(message.Result.Pollers);
                Assert.Equal("my-poller", message.Result.Pollers.First().Identity);
                Assert.Equal(lastAccessTime, message.Result.Pollers.First().LastAccessTime);
                Assert.Equal(666, message.Result.Pollers.First().RatePerSecond);

                // Echo the message via the associated [temporal-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.NotNull(message.Result);
                Assert.Single(message.Result.Pollers);
                Assert.Equal("my-poller", message.Result.Pollers.First().Identity);
                Assert.Equal(lastAccessTime, message.Result.Pollers.First().LastAccessTime);
                Assert.Equal(666, message.Result.Pollers.First().RatePerSecond);
            }
        }
    }
}
