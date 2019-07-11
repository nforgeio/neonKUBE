//-----------------------------------------------------------------------------
// FILE:        Test_Messages.Cluster.cs
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.LibraryAddress);
                Assert.Equal(0, message.LibraryPort);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.LibraryAddress = "1.2.3.4";
                Assert.Equal("1.2.3.4", message.LibraryAddress);
                message.LibraryPort = 666;
                Assert.Equal(666, message.LibraryPort);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<InitializeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.2.3.4", message.LibraryAddress);
                Assert.Equal(666, message.LibraryPort);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.2.3.4", message.LibraryAddress);
                Assert.Equal(666, message.LibraryPort);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.2.3.4", message.LibraryAddress);
                Assert.Equal(666, message.LibraryPort);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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

                message = ProxyMessage.Deserialize<InitializeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Endpoints);
                Assert.Null(message.Identity);
                Assert.Null(message.Domain);
                Assert.False(message.CreateDomain);
                Assert.Equal(TimeSpan.Zero, message.ClientTimeout);
                Assert.Equal(0, message.Retries);
                Assert.Equal(TimeSpan.Zero, message.RetryDelay);

                // Round-trip

                message.RequestId = 555;
                message.Endpoints = "1.1.1.1:555,2.2.2.2:5555";
                message.Identity = "my-identity";
                message.ClientTimeout = TimeSpan.FromSeconds(30);
                message.Domain = "my-domain";
                message.CreateDomain = true;
                message.Retries = 3;
                message.RetryDelay = TimeSpan.FromSeconds(2);
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
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.1.1.1:555,2.2.2.2:5555", message.Endpoints);
                Assert.Equal("my-identity", message.Identity);
                Assert.Equal(TimeSpan.FromSeconds(30), message.ClientTimeout);
                Assert.Equal("my-domain", message.Domain);
                Assert.True(message.CreateDomain);
                Assert.Equal(3, message.Retries);
                Assert.Equal(TimeSpan.FromSeconds(2), message.RetryDelay);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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

                message = ProxyMessage.Deserialize<ConnectReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Name);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Name = "my-domain";
                Assert.Equal("my-domain", message.Name);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainDescribeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.False(message.ConfigurationEmitMetrics);
                Assert.Equal(0, message.ConfigurationRetentionDays);
                Assert.Null(message.DomainInfoName);
                Assert.Null(message.DomainInfoDescription);
                Assert.Equal(DomainStatus.Unspecified, message.DomainInfoStatus);
                Assert.Null(message.DomainInfoOwnerEmail);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);
                message.ConfigurationEmitMetrics = true;
                Assert.True(message.ConfigurationEmitMetrics);
                message.ConfigurationRetentionDays = 7;
                Assert.Equal(7, message.ConfigurationRetentionDays);
                message.DomainInfoName = "my-name";
                Assert.Equal("my-name", message.DomainInfoName);
                message.DomainInfoDescription = "my-description";
                Assert.Equal("my-description", message.DomainInfoDescription);
                message.DomainInfoStatus = DomainStatus.Deprecated;
                Assert.Equal(DomainStatus.Deprecated, message.DomainInfoStatus);
                message.DomainInfoOwnerEmail = "joe@bloe.com";
                Assert.Equal("joe@bloe.com", message.DomainInfoOwnerEmail);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainDescribeReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.ConfigurationEmitMetrics);
                Assert.Equal(7, message.ConfigurationRetentionDays);
                Assert.Equal("my-name", message.DomainInfoName);
                Assert.Equal("my-description", message.DomainInfoDescription);
                Assert.Equal(DomainStatus.Deprecated, message.DomainInfoStatus);
                Assert.Equal("joe@bloe.com", message.DomainInfoOwnerEmail);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Name);
                Assert.Null(message.Description);
                Assert.Null(message.OwnerEmail);
                Assert.False(message.EmitMetrics);
                Assert.Equal(0, message.RetentionDays);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Name = "my-domain";
                Assert.Equal("my-domain", message.Name);
                message.Description = "my-description";
                Assert.Equal("my-description", message.Description);
                message.OwnerEmail = "my-email";
                Assert.Equal("my-email", message.OwnerEmail);
                message.EmitMetrics = true;
                Assert.True(message.EmitMetrics);
                message.RetentionDays = 14;
                Assert.Equal(14, message.RetentionDays);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainRegisterRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);
                Assert.Equal("my-description", message.Description);
                Assert.Equal("my-email", message.OwnerEmail);
                Assert.True(message.EmitMetrics);
                Assert.Equal(14, message.RetentionDays);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);
                Assert.Equal("my-description", message.Description);
                Assert.Equal("my-email", message.OwnerEmail);
                Assert.True(message.EmitMetrics);
                Assert.Equal(14, message.RetentionDays);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);
                Assert.Equal("my-description", message.Description);
                Assert.Equal("my-email", message.OwnerEmail);
                Assert.True(message.EmitMetrics);
                Assert.Equal(14, message.RetentionDays);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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

                message = ProxyMessage.Deserialize<DomainRegisterReply>(stream);
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Name);
                Assert.Null(message.UpdatedInfoDescription);
                Assert.Null(message.UpdatedInfoOwnerEmail);
                Assert.False(message.ConfigurationEmitMetrics);
                Assert.Equal(0, message.ConfigurationRetentionDays);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Name = "my-name";
                Assert.Equal("my-name", message.Name);
                message.UpdatedInfoDescription = "my-description";
                Assert.Equal("my-description", message.UpdatedInfoDescription);
                message.UpdatedInfoOwnerEmail = "joe@bloe.com";
                Assert.Equal("joe@bloe.com", message.UpdatedInfoOwnerEmail);
                message.ConfigurationEmitMetrics = true;
                Assert.True(message.ConfigurationEmitMetrics);
                message.ConfigurationRetentionDays = 7;
                Assert.Equal(7, message.ConfigurationRetentionDays);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainUpdateRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-name", message.Name);
                Assert.Equal("my-description", message.UpdatedInfoDescription);
                Assert.Equal("joe@bloe.com", message.UpdatedInfoOwnerEmail);
                Assert.True(message.ConfigurationEmitMetrics);
                Assert.Equal(7, message.ConfigurationRetentionDays);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-name", message.Name);
                Assert.Equal("my-description", message.UpdatedInfoDescription);
                Assert.Equal("joe@bloe.com", message.UpdatedInfoOwnerEmail);
                Assert.True(message.ConfigurationEmitMetrics);
                Assert.Equal(7, message.ConfigurationRetentionDays);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-name", message.Name);
                Assert.Equal("my-description", message.UpdatedInfoDescription);
                Assert.Equal("joe@bloe.com", message.UpdatedInfoOwnerEmail);
                Assert.True(message.ConfigurationEmitMetrics);
                Assert.Equal(7, message.ConfigurationRetentionDays);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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

                message = ProxyMessage.Deserialize<DomainUpdateReply>(stream);
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
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<TerminateRequest>(stream);
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

                message = ProxyMessage.Deserialize<TerminateReply>(stream);
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
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<HeartbeatRequest>(stream);
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

                message = ProxyMessage.Deserialize<HeartbeatReply>(stream);
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
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.RequestId = 555;
                message.TargetRequestId = 666;
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<CancelRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.TargetRequestId);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.TargetRequestId);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.TargetRequestId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);
                message.WasCancelled = true;
                Assert.True(message.WasCancelled);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<CancelReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.WasCancelled);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.True(message.WasCancelled);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.True(message.WasCancelled);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Domain);
                Assert.Null(message.TaskList);
                Assert.Null(message.Options);

                // Round-trip

                message.RequestId = 555;
                message.Domain = "my-domain";
                message.TaskList = "my-tasks";
                message.Options = new InternalWorkerOptions() { Identity = "my-identity", MaxConcurrentActivityExecutionSize = 1234 };
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
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("my-tasks", message.TaskList);
                Assert.Equal("my-identity", message.Options.Identity);
                Assert.Equal(1234, message.Options.MaxConcurrentActivityExecutionSize);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("my-tasks", message.TaskList);
                Assert.Equal("my-identity", message.Options.Identity);
                Assert.Equal(1234, message.Options.MaxConcurrentActivityExecutionSize);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Domain);
                Assert.Equal("my-tasks", message.TaskList);
                Assert.Equal("my-identity", message.Options.Identity);
                Assert.Equal(1234, message.Options.MaxConcurrentActivityExecutionSize);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Equal(0, message.WorkerId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.WorkerId = 666;
                Assert.Equal(666, message.WorkerId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<NewWorkerReply>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.WorkerId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.WorkerId = 666;
                Assert.Equal(666, message.WorkerId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<StopWorkerRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkerId);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<StopWorkerReply>(stream);
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
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<PingRequest>(stream);
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<PingReply>(stream);
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
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.Size);

                // Round-trip

                message.RequestId = 555;
                message.Size = 20000;
                Assert.Equal(555, message.RequestId);
                Assert.Equal(20000, message.Size);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSetCacheSizeRequest>(stream);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(20000, message.Size);

                // Echo the message via the connection's web server and verify.

                message = EchoToClient(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(20000, message.Size);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(20000, message.Size);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
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
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowSetCacheSizeReply>(stream);
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
    }
}
