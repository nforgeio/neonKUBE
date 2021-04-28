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

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.IO;
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
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.2.3.4", message.LibraryAddress);
                Assert.Equal(666, message.LibraryPort);
                Assert.Equal(LogLevel.Info, message.LogLevel);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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
                message.Error = new CadenceError("MyError");

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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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
                Assert.Null(message.Endpoints);
                Assert.Null(message.Identity);
                Assert.Null(message.Domain);
                Assert.False(message.CreateDomain);
                Assert.Equal(TimeSpan.Zero, message.ClientTimeout);
                Assert.Equal(0, message.Retries);
                Assert.Equal(TimeSpan.Zero, message.RetryDelay);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Endpoints = "1.1.1.1:555,2.2.2.2:5555";
                message.Identity = "my-identity";
                message.ClientTimeout = TimeSpan.FromSeconds(30);
                message.Domain = "my-domain";
                message.CreateDomain = true;
                message.Retries = 3;
                message.RetryDelay = TimeSpan.FromSeconds(2);

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.1.1.1:555,2.2.2.2:5555", message.Endpoints);
                Assert.Equal("my-identity", message.Identity);
                Assert.Equal("my-domain", message.Domain);
                Assert.True(message.CreateDomain);
                Assert.Equal(3, message.Retries);
                Assert.Equal(TimeSpan.FromSeconds(2), message.RetryDelay);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ConnectRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.1.1.1:555,2.2.2.2:5555", message.Endpoints);
                Assert.Equal("my-identity", message.Identity);
                Assert.Equal(TimeSpan.FromSeconds(30), message.ClientTimeout);
                Assert.Equal("my-domain", message.Domain);
                Assert.True(message.CreateDomain);
                Assert.Equal(3, message.Retries);
                Assert.Equal(TimeSpan.FromSeconds(2), message.RetryDelay);

                // Clone()

                message = (ConnectRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.1.1.1:555,2.2.2.2:5555", message.Endpoints);
                Assert.Equal("my-identity", message.Identity);
                Assert.Equal(TimeSpan.FromSeconds(30), message.ClientTimeout);
                Assert.Equal("my-domain", message.Domain);
                Assert.True(message.CreateDomain);
                Assert.Equal(3, message.Retries);
                Assert.Equal(TimeSpan.FromSeconds(2), message.RetryDelay);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.1.1.1:555,2.2.2.2:5555", message.Endpoints);
                Assert.Equal("my-identity", message.Identity);
                Assert.Equal(TimeSpan.FromSeconds(30), message.ClientTimeout);
                Assert.Equal("my-domain", message.Domain);
                Assert.True(message.CreateDomain);
                Assert.Equal(3, message.Retries);
                Assert.Equal(TimeSpan.FromSeconds(2), message.RetryDelay);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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
                message.Error = new CadenceError("MyError");

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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
        public void Test_DomainDescribeRequest()
        {
            DomainDescribeRequest message;

            using (var stream = new MemoryStream())
            {
                message = new DomainDescribeRequest();

                Assert.Equal(InternalMessageTypes.DomainDescribeReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainDescribeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Name);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Name = "my-domain";

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainDescribeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);

                // Clone()

                message = (DomainDescribeRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
        public void Test_DomainDescribeReply()
        {
            DomainDescribeReply message;

            using (var stream = new MemoryStream())
            {
                message = new DomainDescribeReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainDescribeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.False(message.ConfigurationEmitMetrics);
                Assert.Equal(0, message.ConfigurationRetentionDays);
                Assert.Null(message.DomainInfoName);
                Assert.Null(message.DomainInfoDescription);
                Assert.Equal(DomainStatus.Registered, message.DomainInfoStatus);
                Assert.Null(message.DomainInfoOwnerEmail);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new CadenceError("MyError");
                message.ConfigurationEmitMetrics = true;
                message.ConfigurationRetentionDays = 7;
                message.DomainInfoName = "my-name";
                message.DomainInfoDescription = "my-description";
                message.DomainInfoStatus = DomainStatus.Deprecated;
                message.DomainInfoOwnerEmail = "joe@bloe.com";

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.ConfigurationEmitMetrics);
                Assert.Equal(7, message.ConfigurationRetentionDays);
                Assert.Equal("my-name", message.DomainInfoName);
                Assert.Equal("my-description", message.DomainInfoDescription);
                Assert.Equal(DomainStatus.Deprecated, message.DomainInfoStatus);
                Assert.Equal("joe@bloe.com", message.DomainInfoOwnerEmail);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainDescribeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.ConfigurationEmitMetrics);
                Assert.Equal(7, message.ConfigurationRetentionDays);
                Assert.Equal("my-name", message.DomainInfoName);
                Assert.Equal("my-description", message.DomainInfoDescription);
                Assert.Equal(DomainStatus.Deprecated, message.DomainInfoStatus);
                Assert.Equal("joe@bloe.com", message.DomainInfoOwnerEmail);

                // Clone()

                message = (DomainDescribeReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.ConfigurationEmitMetrics);
                Assert.Equal(7, message.ConfigurationRetentionDays);
                Assert.Equal("my-name", message.DomainInfoName);
                Assert.Equal("my-description", message.DomainInfoDescription);
                Assert.Equal(DomainStatus.Deprecated, message.DomainInfoStatus);
                Assert.Equal("joe@bloe.com", message.DomainInfoOwnerEmail);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.ConfigurationEmitMetrics);
                Assert.Equal(7, message.ConfigurationRetentionDays);
                Assert.Equal("my-name", message.DomainInfoName);
                Assert.Equal("my-description", message.DomainInfoDescription);
                Assert.Equal(DomainStatus.Deprecated, message.DomainInfoStatus);
                Assert.Equal("joe@bloe.com", message.DomainInfoOwnerEmail);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
        public void Test_DomainRegisterRequest()
        {
            DomainRegisterRequest message;

            using (var stream = new MemoryStream())
            {
                message = new DomainRegisterRequest();

                Assert.Equal(InternalMessageTypes.DomainRegisterReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainRegisterRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Name);
                Assert.Null(message.Description);
                Assert.Null(message.OwnerEmail);
                Assert.False(message.EmitMetrics);
                Assert.Equal(0, message.RetentionDays);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Name = "my-domain";
                message.Description = "my-description";
                message.OwnerEmail = "my-email";
                message.EmitMetrics = true;
                message.RetentionDays = 14;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);
                Assert.Equal("my-description", message.Description);
                Assert.Equal("my-email", message.OwnerEmail);
                Assert.True(message.EmitMetrics);
                Assert.Equal(14, message.RetentionDays);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainRegisterRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);
                Assert.Equal("my-description", message.Description);
                Assert.Equal("my-email", message.OwnerEmail);
                Assert.True(message.EmitMetrics);
                Assert.Equal(14, message.RetentionDays);

                // Clone()

                message = (DomainRegisterRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);
                Assert.Equal("my-description", message.Description);
                Assert.Equal("my-email", message.OwnerEmail);
                Assert.True(message.EmitMetrics);
                Assert.Equal(14, message.RetentionDays);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);
                Assert.Equal("my-description", message.Description);
                Assert.Equal("my-email", message.OwnerEmail);
                Assert.True(message.EmitMetrics);
                Assert.Equal(14, message.RetentionDays);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
        public void Test_DomainRegisterReply()
        {
            DomainRegisterReply message;

            using (var stream = new MemoryStream())
            {
                message = new DomainRegisterReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainRegisterReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new CadenceError("MyError");

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainRegisterReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (DomainRegisterReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
        public void Test_DomainUpdateRequest()
        {
            DomainUpdateRequest message;

            using (var stream = new MemoryStream())
            {
                message = new DomainUpdateRequest();

                Assert.Equal(InternalMessageTypes.DomainUpdateReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainUpdateRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Name);
                Assert.Null(message.UpdatedInfoDescription);
                Assert.Null(message.UpdatedInfoOwnerEmail);
                Assert.False(message.ConfigurationEmitMetrics);
                Assert.Equal(0, message.ConfigurationRetentionDays);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Name = "my-name";
                message.UpdatedInfoDescription = "my-description";
                message.UpdatedInfoOwnerEmail = "joe@bloe.com";
                message.ConfigurationEmitMetrics = true;
                message.ConfigurationRetentionDays = 7;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-name", message.Name);
                Assert.Equal("my-description", message.UpdatedInfoDescription);
                Assert.Equal("joe@bloe.com", message.UpdatedInfoOwnerEmail);
                Assert.True(message.ConfigurationEmitMetrics);
                Assert.Equal(7, message.ConfigurationRetentionDays);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainUpdateRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-name", message.Name);
                Assert.Equal("my-description", message.UpdatedInfoDescription);
                Assert.Equal("joe@bloe.com", message.UpdatedInfoOwnerEmail);
                Assert.True(message.ConfigurationEmitMetrics);
                Assert.Equal(7, message.ConfigurationRetentionDays);

                // Clone()

                message = (DomainUpdateRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-name", message.Name);
                Assert.Equal("my-description", message.UpdatedInfoDescription);
                Assert.Equal("joe@bloe.com", message.UpdatedInfoOwnerEmail);
                Assert.True(message.ConfigurationEmitMetrics);
                Assert.Equal(7, message.ConfigurationRetentionDays);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-name", message.Name);
                Assert.Equal("my-description", message.UpdatedInfoDescription);
                Assert.Equal("joe@bloe.com", message.UpdatedInfoOwnerEmail);
                Assert.True(message.ConfigurationEmitMetrics);
                Assert.Equal(7, message.ConfigurationRetentionDays);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
        public void Test_DomainUpdateReply()
        {
            DomainUpdateReply message;

            using (var stream = new MemoryStream())
            {
                message = new DomainUpdateReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainUpdateReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Error = new CadenceError("MyError");

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainUpdateReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Clone()

                message = (DomainUpdateReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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
                message.Error = new CadenceError("MyError");

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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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
                message.Error = new CadenceError("MyError");

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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.TargetRequestId);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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
                message.Error = new CadenceError("MyError");
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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.True(message.WasCancelled);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Domain);
                Assert.Null(message.TaskList);
                Assert.Null(message.Options);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Domain = "my-domain";
                message.TaskList = "my-tasks";
                message.Options = new InternalWorkerOptions() { Identity = "my-identity", MaxConcurrentActivityExecutionSize = 1234 };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("my-tasks", message.TaskList);
                Assert.Equal("my-identity", message.Options.Identity);
                Assert.Equal(1234, message.Options.MaxConcurrentActivityExecutionSize);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NewWorkerRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("my-tasks", message.TaskList);
                Assert.Equal("my-identity", message.Options.Identity);
                Assert.Equal(1234, message.Options.MaxConcurrentActivityExecutionSize);

                // Clone()

                message = (NewWorkerRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("my-tasks", message.TaskList);
                Assert.Equal("my-identity", message.Options.Identity);
                Assert.Equal(1234, message.Options.MaxConcurrentActivityExecutionSize);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("my-tasks", message.TaskList);
                Assert.Equal("my-identity", message.Options.Identity);
                Assert.Equal(1234, message.Options.MaxConcurrentActivityExecutionSize);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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

                message.ClientId = 444;
                message.RequestId = 555;
                message.WorkerId = 666;

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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(20000, message.Size);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
        public void Test_DomainDeprecateRequest()
        {
            DomainDeprecateRequest message;

            using (var stream = new MemoryStream())
            {
                message = new DomainDeprecateRequest();

                Assert.Equal(InternalMessageTypes.DomainDeprecateReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainDeprecateRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Name);
                Assert.Null(message.SecurityToken);

                // Round-trip

                message.ClientId = 444;
                message.RequestId = 555;
                message.Name = "my-domain";
                message.SecurityToken = "my-token";

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);
                Assert.Equal("my-token", message.SecurityToken);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainDeprecateRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);
                Assert.Equal("my-token", message.SecurityToken);

                // Clone()

                message = (DomainDeprecateRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);
                Assert.Equal("my-token", message.SecurityToken);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);
                Assert.Equal("my-token", message.SecurityToken);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
        public void Test_DomainDeprecateReply()
        {
            DomainDeprecateReply message;

            using (var stream = new MemoryStream())
            {
                message = new DomainDeprecateReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainDeprecateReply>(stream);
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

                message = ProxyMessage.Deserialize<DomainDeprecateReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Clone()

                message = (DomainDeprecateReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
            }
        }
    
        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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
                Assert.False(message.FromCadence);
                Assert.Null(message.LogMessage);

                // Round-trip

                message.ClientId    = 444;
                message.RequestId   = 555;
                message.TimeUtc     = new DateTime(2019, 8, 27);
                message.FromCadence = true;
                message.LogLevel    = Neon.Diagnostics.LogLevel.Info;
                message.LogMessage  = "Hello World!";

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(new DateTime(2019, 8, 27), message.TimeUtc);
                Assert.True(message.FromCadence);
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
                Assert.True(message.FromCadence);
                Assert.Equal(Neon.Diagnostics.LogLevel.Info, message.LogLevel);
                Assert.Equal("Hello World!", message.LogMessage);

                // Clone()

                message = (LogRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(new DateTime(2019, 8, 27), message.TimeUtc);
                Assert.True(message.FromCadence);
                Assert.Equal(Neon.Diagnostics.LogLevel.Info, message.LogLevel);
                Assert.Equal("Hello World!", message.LogMessage);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(new DateTime(2019, 8, 27), message.TimeUtc);
                Assert.True(message.FromCadence);
                Assert.Equal(Neon.Diagnostics.LogLevel.Info, message.LogLevel);
                Assert.Equal("Hello World!", message.LogMessage);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
        public void Test_DomainListRequest()
        {
            DomainListRequest message;

            using (var stream = new MemoryStream())
            {
                message = new DomainListRequest();

                Assert.Equal(InternalMessageTypes.DomainListReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainListRequest>(stream);
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

                message = ProxyMessage.Deserialize<DomainListRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.PageSize);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.NextPageToken);

                // Clone()

                message = (DomainListRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.PageSize);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.NextPageToken);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.PageSize);
                Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, message.NextPageToken);
            }
        }

        /// <summary>
        /// Returns an <see cref="InternalDomainInfo"/> instance for testing purposes.
        /// </summary>
        /// <returns>The test info.</returns>
        private InternalDomainInfo GetTestDomainInfo()
        {
            var data = new Dictionary<string, byte[]>();

            data.Add("test", new byte[] { 0, 1, 2, 3, 4 });

            return new InternalDomainInfo()
            {
                Name         = "my-domain",
                DomainStatus = DomainStatus.Deprecated,
                Description  = "Test domain",
                OwnerEmail   = "jeff@lilltek.com",
                Data         = data,
                Uuid         = "1111-2222-3333-4444"
            };
        }

        /// <summary>
        /// Validates an <see cref="InternalDomainInfo"/> instance for testing purposes.
        /// </summary>
        /// <param name="info">The domain info.</param>
        private void ValidateTestDomainInfo(InternalDomainInfo info)
        {
            Assert.NotNull(info);
            Assert.Equal("my-domain", info.Name);
            Assert.Equal(DomainStatus.Deprecated, info.DomainStatus);
            Assert.Equal("jeff@lilltek.com", info.OwnerEmail);
            Assert.Single(info.Data);
            Assert.Equal("test", info.Data.First().Key);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, info.Data.First().Value);
            Assert.Equal("1111-2222-3333-4444", info.Uuid);
        }

        /// <summary>
        /// Returns an <see cref="InternalDomainConfiguration"/> for testing purposes.
        /// </summary>
        /// <returns></returns>
        private InternalDomainConfiguration GetTestDomainConfiguration()
        {
            var badBinariesMap = new Dictionary<string, InternalBadBinaryInfo>();

            badBinariesMap.Add("bad",
                new InternalBadBinaryInfo()
                {
                    Reason          = "foo",
                    Operator        = "bar",
                    CreatedTimeNano = 5000000000L
                });

            var badBinaries = new InternalBadBinaries()
            {
                Binaries = badBinariesMap
            };

            return new InternalDomainConfiguration()
            {
                WorkflowExecutionRetentionPeriodInDays = 30,
                EmitMetric                             = true,
                BadBinaries                            = badBinaries,
                HistoryArchivalStatus                  = ArchivalStatus.Enabled,
                HistoryArchivalUri                     = "http://history",
                VisibilityArchivalStatus               = ArchivalStatus.Disabled,
                VisibilityArchivalUri                  = "http://visibility"
            };
        }

        /// <summary>
        /// Validates a test <see cref="InternalDomainConfiguration"/>.
        /// </summary>
        /// <param name="config">The domain config.</param>
        private void ValidateTestDomainConfiguration(InternalDomainConfiguration config)
        {
            Assert.NotNull(config);
            Assert.Equal(30, config.WorkflowExecutionRetentionPeriodInDays);
            Assert.True(config.EmitMetric);
            Assert.NotNull(config.BadBinaries);
            Assert.Single(config.BadBinaries.Binaries);
            Assert.Equal("bad", config.BadBinaries.Binaries.First().Key);
            Assert.Equal("foo", config.BadBinaries.Binaries.First().Value.Reason);
            Assert.Equal("bar", config.BadBinaries.Binaries.First().Value.Operator);
            Assert.Equal(5000000000L, config.BadBinaries.Binaries.First().Value.CreatedTimeNano);
            Assert.Equal(ArchivalStatus.Enabled, config.HistoryArchivalStatus);
            Assert.Equal("http://history", config.HistoryArchivalUri);
            Assert.Equal(ArchivalStatus.Disabled, config.VisibilityArchivalStatus);
            Assert.Equal("http://visibility", config.VisibilityArchivalUri);
        }

        /// <summary>
        /// Returns a list of test domain information.
        /// </summary>
        private List<InternalDescribeDomainResponse> GetTestDomains()
        {
            var list = new List<InternalDescribeDomainResponse>();

            list.Add(
                new InternalDescribeDomainResponse()
                {
                    IsGlobalDomain = true,
                    DomainConfiguration = new InternalDomainConfiguration()
                    {
                        EmitMetric                             = true,
                        WorkflowExecutionRetentionPeriodInDays = 30
                    },
                    DomainInfo = new InternalDomainInfo()
                    {
                        Name         = "my-domain",
                        Description  = "This is my domain",
                        DomainStatus = DomainStatus.Deprecated,
                        OwnerEmail   = "jeff@lilltek.com"

                        // $todo(jefflill): Currently ignoring
                        //
                        //  Data
                        //  Uuid
                    }
                });

            return list;
        }

        /// <summary>
        /// Verifies that a test domain list is valid.
        /// </summary>
        /// <param name="domains">The test domains.</param>
        private void ValidateTestDomains(List<InternalDescribeDomainResponse> domains)
        {
            Assert.NotNull(domains);
            Assert.Single(domains);

            var domain = domains.First();

            Assert.True(domain.IsGlobalDomain);
            
            Assert.NotNull(domain.DomainConfiguration);
            Assert.True(domain.DomainConfiguration.EmitMetric);
            Assert.Equal(30, domain.DomainConfiguration.WorkflowExecutionRetentionPeriodInDays);

            Assert.NotNull(domain.DomainInfo);
            Assert.Equal("my-domain", domain.DomainInfo.Name);
            Assert.Equal("This is my domain", domain.DomainInfo.Description);
            Assert.Equal(DomainStatus.Deprecated, domain.DomainInfo.DomainStatus);
            Assert.Equal("jeff@lilltek.com", domain.DomainInfo.OwnerEmail);
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
        public void Test_DomainListReply()
        {
            DomainListReply message;

            using (var stream = new MemoryStream())
            {
                message = new DomainListReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainListReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Domains);
                Assert.Null(message.NextPageToken);

                // Round-trip

                message.ClientId      = 444;
                message.RequestId     = 555;
                message.NextPageToken = new byte[] { 5, 6, 7, 8, 9 };
                message.Domains       = GetTestDomains();

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                ValidateTestDomains(message.Domains);
                Assert.Equal(message.NextPageToken, new byte[] { 5, 6, 7, 8, 9 });

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainListReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                ValidateTestDomains(message.Domains);
                Assert.Equal(message.NextPageToken, new byte[] { 5, 6, 7, 8, 9 });

                // Clone()

                message = (DomainListReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                ValidateTestDomains(message.Domains);
                Assert.Equal(message.NextPageToken, new byte[] { 5, 6, 7, 8, 9 });

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                ValidateTestDomains(message.Domains);
                Assert.Equal(message.NextPageToken, new byte[] { 5, 6, 7, 8, 9 });
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
        public void Test_DescribeTaskListRequest()
        {
            DescribeTaskListRequest message;

            using (var stream = new MemoryStream())
            {
                message = new DescribeTaskListRequest();

                Assert.Equal(InternalMessageTypes.DescribeTaskListReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DescribeTaskListRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Name);
                Assert.Equal(default, message.TaskListType);

                // Round-trip

                message.ClientId     = 444;
                message.RequestId    = 555;
                message.Name         = "my-tasklist";
                message.TaskListType = TaskListType.Activity;

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-tasklist", message.Name);
                Assert.Equal(TaskListType.Activity, message.TaskListType);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DescribeTaskListRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-tasklist", message.Name);
                Assert.Equal(TaskListType.Activity, message.TaskListType);

                // Clone()

                message = (DescribeTaskListRequest)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-tasklist", message.Name);
                Assert.Equal(TaskListType.Activity, message.TaskListType);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-tasklist", message.Name);
                Assert.Equal(TaskListType.Activity, message.TaskListType);
            }
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCadence)]
        public void Test_DescribeTaskListReply()
        {
            DescribeTaskListReply message;

            using (var stream = new MemoryStream())
            {
                message = new DescribeTaskListReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DescribeTaskListReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(0, message.ClientId);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Null(message.Result);

                // Round-trip

                message.ClientId  = 444;
                message.RequestId = 555;
                message.Result =
                    new InternalDescribeTaskListResponse()
                    {
                        Pollers = new InternalPollerInfo[]
                        {
                             new InternalPollerInfo()
                             {
                                 Identity       = "my-poller",
                                 LastAccessTime = 5000000000L,
                                 RatePerSecond  = 666
                             }
                        }
                    };

                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.NotNull(message.Result);
                Assert.Single(message.Result.Pollers);
                Assert.Equal("my-poller", message.Result.Pollers.First().Identity);
                Assert.Equal(5000000000L, message.Result.Pollers.First().LastAccessTime);
                Assert.Equal(666, message.Result.Pollers.First().RatePerSecond);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DescribeTaskListReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.NotNull(message.Result);
                Assert.Single(message.Result.Pollers);
                Assert.Equal("my-poller", message.Result.Pollers.First().Identity);
                Assert.Equal(5000000000L, message.Result.Pollers.First().LastAccessTime);
                Assert.Equal(666, message.Result.Pollers.First().RatePerSecond);

                // Clone()

                message = (DescribeTaskListReply)message.Clone();
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.NotNull(message.Result);
                Assert.Single(message.Result.Pollers);
                Assert.Equal("my-poller", message.Result.Pollers.First().Identity);
                Assert.Equal(5000000000L, message.Result.Pollers.First().LastAccessTime);
                Assert.Equal(666, message.Result.Pollers.First().RatePerSecond);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(444, message.ClientId);
                Assert.Equal(555, message.RequestId);
                Assert.NotNull(message.Result);
                Assert.Single(message.Result.Pollers);
                Assert.Equal("my-poller", message.Result.Pollers.First().Identity);
                Assert.Equal(5000000000L, message.Result.Pollers.First().LastAccessTime);
                Assert.Equal(666, message.Result.Pollers.First().RatePerSecond);
            }
        }
    }
}
