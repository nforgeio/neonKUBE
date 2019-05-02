//-----------------------------------------------------------------------------
// FILE:        Test_Messages.cs
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

using Xunit;

namespace TestCadence
{
    public sealed class Test_Messages : IClassFixture<CadenceFixture>, IDisposable
    {
        CadenceFixture      fixture;
        CadenceConnection   connection;
        HttpClient          proxyClient;

        public Test_Messages(CadenceFixture fixture)
        {
            var settings = new CadenceSettings()
            {
                Mode  = ConnectionMode.ListenOnly,
                Debug = true,

                //--------------------------------
                // $debug(jeff.lill): DELETE THIS!
                DebugPrelaunched       = false,
                DebugDisableHandshakes = false,
                DebugDisableHeartbeats = true,
                //--------------------------------
            };

            fixture.Start(settings);

            this.fixture     = fixture;
            this.connection  = fixture.Connection;
            this.proxyClient = new HttpClient() { BaseAddress = connection.ProxyUri };
        }

        public void Dispose()
        {
            if (proxyClient != null)
            {
                proxyClient.Dispose();
                proxyClient = null;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void TestProxyMessage()
        {
            // Ensures that we can serialize and deserialize base messages.

            ProxyMessage message;

            using (var stream = new MemoryStream())
            {
                // Empty message.

                message = new ProxyMessage();

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ProxyMessage>(stream, ignoreTypeCode: true);
                Assert.Equal(MessageTypes.Unspecified, message.Type);
                Assert.Empty(message.Properties);
                Assert.Empty(message.Attachments);

                // Message with args and attachments.

                message = new ProxyMessage();

                message.Properties.Add("One", "1");
                message.Properties.Add("Two", "2");
                message.Properties.Add("Empty", string.Empty);
                message.Properties.Add("Null", null);

                message.Attachments.Add(new byte[] { 0, 1, 2, 3, 4 });
                message.Attachments.Add(new byte[0]);
                message.Attachments.Add(null);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ProxyMessage>(stream, ignoreTypeCode: true);
                Assert.Equal(MessageTypes.Unspecified, message.Type);
                Assert.Equal(4, message.Properties.Count);
                Assert.Equal("1", message.Properties["One"]);
                Assert.Equal("2", message.Properties["Two"]);
                Assert.Empty(message.Properties["Empty"]);
                Assert.Null(message.Properties["Null"]);

                Assert.Equal(3, message.Attachments.Count);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Attachments[0]);
                Assert.Empty(message.Attachments[1]);
                Assert.Null(message.Attachments[2]);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void TestPropertyHelpers()
        {
            // Verify that the property helper methods work as expected.

            var message = new ProxyMessage();

            // Verify that non-existant property values return the default for the requested type.

            Assert.Null(message.GetStringProperty("foo"));
            Assert.Equal(0, message.GetIntProperty("foo"));
            Assert.Equal(0L, message.GetLongProperty("foo"));
            Assert.False(message.GetBoolProperty("foo"));
            Assert.Equal(0.0, message.GetDoubleProperty("foo"));
            Assert.Equal(DateTime.MinValue, message.GetDateTimeProperty("foo"));
            Assert.Equal(TimeSpan.Zero, message.GetTimeSpanProperty("foo"));

            // Verify that we can override default values for non-existant properties.

            Assert.Equal("bar", message.GetStringProperty("foo", "bar"));
            Assert.Equal(123, message.GetIntProperty("foo", 123));
            Assert.Equal(456L, message.GetLongProperty("foo", 456L));
            Assert.True(message.GetBoolProperty("foo", true));
            Assert.Equal(123.456, message.GetDoubleProperty("foo", 123.456));
            Assert.Equal(new DateTime(2019, 4, 14), message.GetDateTimeProperty("foo", new DateTime(2019, 4, 14)));
            Assert.Equal(TimeSpan.FromSeconds(123), message.GetTimeSpanProperty("foo", TimeSpan.FromSeconds(123)));

            // Verify that we can write and then read properties.

            message.SetStringProperty("foo", "bar");
            Assert.Equal("bar", message.GetStringProperty("foo"));

            message.SetIntProperty("foo", 123);
            Assert.Equal(123, message.GetIntProperty("foo"));

            message.SetLongProperty("foo", 456L);
            Assert.Equal(456L, message.GetLongProperty("foo"));

            message.SetBoolProperty("foo", true);
            Assert.True(message.GetBoolProperty("foo"));

            message.SetDoubleProperty("foo", 123.456);
            Assert.Equal(123.456, message.GetDoubleProperty("foo"));

            var date = new DateTime(2019, 4, 14).ToUniversalTime();

            message.SetDateTimeProperty("foo", date);
            Assert.Equal(date, message.GetDateTimeProperty("foo"));

            message.SetTimeSpanProperty("foo", TimeSpan.FromSeconds(123));
            Assert.Equal(TimeSpan.FromSeconds(123), message.GetTimeSpanProperty("foo"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void TestProxyRequest()
        {
            // Ensures that we can serialize and deserialize request messages.

            ProxyRequest message;

            using (var stream = new MemoryStream())
            {
                // Empty message.

                message = new ProxyRequest();

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ProxyRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ProxyRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void TestProxyReply()
        {
            // Ensures that we can serialize and deserialize reply messages.

            ProxyReply message;

            using (var stream = new MemoryStream())
            {
                // Empty message.

                message = new ProxyReply();

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ProxyReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(CadenceErrorTypes.None, message.ErrorType);
                Assert.Null(message.Error);
                Assert.Null(message.ErrorDetails);

                // Round-trip

                message.RequestId = 555;
                message.ErrorType = CadenceErrorTypes.Custom;
                message.Error = "MyError";
                message.ErrorDetails = "MyError Details";

                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ProxyReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void TestActivityRequest()
        {
            // Ensures that we can serialize and deserialize activity request messages.

            ActivityRequest message;

            using (var stream = new MemoryStream())
            {
                // Empty message.

                message = new ActivityRequest();

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ActivityRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ActivityContextId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.ActivityContextId = 666;
                Assert.Equal(666, message.ActivityContextId);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ActivityRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ActivityContextId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void TestActivityReply()
        {
            // Ensures that we can serialize and deserialize activity reply messages.

            ActivityReply message;

            using (var stream = new MemoryStream())
            {
                // Empty message.

                message = new ActivityReply();

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ActivityReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(CadenceErrorTypes.None, message.ErrorType);
                Assert.Null(message.Error);
                Assert.Null(message.ErrorDetails);
                Assert.Equal(0, message.ActivityContextId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.ErrorType = CadenceErrorTypes.Custom;
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                message.Error = "MyError";
                Assert.Equal("MyError", message.Error);
                message.ErrorDetails = "MyError Details";
                Assert.Equal("MyError Details", message.ErrorDetails);
                message.ActivityContextId = 666;
                Assert.Equal(666, message.ActivityContextId);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ActivityReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);
                Assert.Equal(666, message.ActivityContextId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void TestWorkflowRequest()
        {
            // Ensures that we can serialize and deserialize workflow request messages.

            WorkflowRequest message;

            using (var stream = new MemoryStream())
            {
                // Empty message.

                message = new WorkflowRequest();

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.WorkflowContextId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.WorkflowContextId = 666;
                Assert.Equal(666, message.WorkflowContextId);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkflowContextId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void TestWorkflowReply()
        {
            // Ensures that we can serialize and deserialize workflow reply messages.

            WorkflowReply message;

            using (var stream = new MemoryStream())
            {
                // Empty message.

                message = new WorkflowReply();

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(CadenceErrorTypes.None, message.ErrorType);
                Assert.Null(message.Error);
                Assert.Null(message.ErrorDetails);
                Assert.Equal(0, message.WorkflowContextId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.ErrorType = CadenceErrorTypes.Custom;
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                message.Error = "MyError";
                Assert.Equal("MyError", message.Error);
                message.ErrorDetails = "MyError Details";
                Assert.Equal("MyError Details", message.ErrorDetails);
                message.WorkflowContextId = 666;
                Assert.Equal(666, message.WorkflowContextId);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);
                Assert.Equal(666, message.WorkflowContextId);
            }
        }

        /// <summary>
        /// Transmits a message to the local <b>cadence-client</b> web server and then 
        /// verifies that the response matches.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="message">The message to be checked.</param>
        /// <returns>The received echo message.</returns>
        private TMessage EchoToConnection<TMessage>(TMessage message)
            where TMessage : ProxyMessage, new()
        {
            var bytes   = message.Serialize();
            var content = new ByteArrayContent(bytes);

            content.Headers.ContentType = new MediaTypeHeaderValue(ProxyMessage.ContentType);

            var request = new HttpRequestMessage(HttpMethod.Put, "/echo")
            {
                Content = content
            };

            var response = fixture.ConnectionClient.SendAsync(request).Result;

            response.EnsureSuccessStatusCode();

            bytes = response.Content.ReadAsByteArrayAsync().Result;

            return ProxyMessage.Deserialize<TMessage>(response.Content.ReadAsStreamAsync().Result);
        }

        /// <summary>
        /// Transmits a message to the connection's associated <b>cadence-proxy</b> 
        /// and then verifies that the response matches.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="message">The message to be checked.</param>
        /// <returns>The received echo message.</returns>
        private TMessage EchoToProxy<TMessage>(TMessage message)
            where TMessage : ProxyMessage, new()
        {
            var bytes   = message.Serialize();
            var content = new ByteArrayContent(bytes);

            content.Headers.ContentType = new MediaTypeHeaderValue(ProxyMessage.ContentType);

            var request = new HttpRequestMessage(HttpMethod.Put, "/echo")
            {
                Content = content
            };

            var response = proxyClient.SendAsync(request).Result;

            response.EnsureSuccessStatusCode();

            bytes = response.Content.ReadAsByteArrayAsync().Result;

            return ProxyMessage.Deserialize<TMessage>(response.Content.ReadAsStreamAsync().Result);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void TestInitializeRequest()
        {
            InitializeRequest message;

            using (var stream = new MemoryStream())
            {
                message = new InitializeRequest();

                Assert.Equal(MessageTypes.InitializeReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<InitializeRequest>(stream, ignoreTypeCode: true);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<InitializeRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.2.3.4", message.LibraryAddress);
                Assert.Equal(666, message.LibraryPort);

                // Echo the message via the connection's web server and verify.

                message = EchoToConnection(message);
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
        public void TestInitializeReply()
        {
            InitializeReply message;

            using (var stream = new MemoryStream())
            {
                message = new InitializeReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<InitializeReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(CadenceErrorTypes.None, message.ErrorType);
                Assert.Null(message.Error);
                Assert.Null(message.ErrorDetails);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.ErrorType = CadenceErrorTypes.Custom;
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                message.Error = "MyError";
                Assert.Equal("MyError", message.Error);
                message.ErrorDetails = "MyError Details";
                Assert.Equal("MyError Details", message.ErrorDetails);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<InitializeReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);

                // Echo the message via the connection's web server and verify.

                message = EchoToConnection(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void TestConnectRequest()
        {
            ConnectRequest message;

            using (var stream = new MemoryStream())
            {
                message = new ConnectRequest();

                Assert.Equal(MessageTypes.ConnectReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ConnectRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Endpoints);
                Assert.Null(message.Identity);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Endpoints = "1.1.1.1:555,2.2.2.2:5555";
                Assert.Equal("1.1.1.1:555,2.2.2.2:5555", message.Endpoints);
                message.Identity = "my-identity";
                Assert.Equal("my-identity", message.Identity);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ConnectRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.1.1.1:555,2.2.2.2:5555", message.Endpoints);
                Assert.Equal("my-identity", message.Identity);

                // Echo the message via the connection's web server and verify.

                message = EchoToConnection(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.1.1.1:555,2.2.2.2:5555", message.Endpoints);
                Assert.Equal("my-identity", message.Identity);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.1.1.1:555,2.2.2.2:5555", message.Endpoints);
                Assert.Equal("my-identity", message.Identity);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void TestConnectReply()
        {
            ConnectReply message;

            using (var stream = new MemoryStream())
            {
                message = new ConnectReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ConnectReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(CadenceErrorTypes.None, message.ErrorType);
                Assert.Null(message.Error);
                Assert.Null(message.ErrorDetails);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.ErrorType = CadenceErrorTypes.Custom;
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                message.Error = "MyError";
                Assert.Equal("MyError", message.Error);
                message.ErrorDetails = "MyError Details";
                Assert.Equal("MyError Details", message.ErrorDetails);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ConnectReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the connection's web server and verify.

                message = EchoToConnection(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);

                // Echo the message via the connection's web server and verify.

                message = EchoToConnection(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void TestDomainDescribeRequest()
        {
            DomainDescribeRequest message;

            using (var stream = new MemoryStream())
            {
                message = new DomainDescribeRequest();

                Assert.Equal(MessageTypes.DomainDescribeReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainDescribeRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Name);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Name = "my-domain";
                Assert.Equal("my-domain", message.Name);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainDescribeRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);

                // Echo the message via the connection's web server and verify.

                message = EchoToConnection(message);
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
        public void TestDomainDescribeReply()
        {
            DomainDescribeReply message;

            using (var stream = new MemoryStream())
            {
                message = new DomainDescribeReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainDescribeReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(CadenceErrorTypes.None, message.ErrorType);
                Assert.Null(message.Error);
                Assert.Null(message.ErrorDetails);
                Assert.False(message.ConfigurationEmitMetrics);
                Assert.Equal(0, message.ConfigurationRetentionDays);
                Assert.Null(message.DomainInfoName);
                Assert.Null(message.DomainInfoDescription);
                Assert.Equal(DomainStatus.Unspecified, message.DomainInfoStatus);
                Assert.Null(message.DomainInfoOwnerEmail);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.ErrorType = CadenceErrorTypes.Custom;
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                message.Error = "MyError";
                Assert.Equal("MyError", message.Error);
                message.ErrorDetails = "MyError Details";
                Assert.Equal("MyError Details", message.ErrorDetails);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainDescribeReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);
                Assert.True(message.ConfigurationEmitMetrics);
                Assert.Equal(7, message.ConfigurationRetentionDays);
                Assert.Equal("my-name", message.DomainInfoName);
                Assert.Equal("my-description", message.DomainInfoDescription);
                Assert.Equal(DomainStatus.Deprecated, message.DomainInfoStatus);
                Assert.Equal("joe@bloe.com", message.DomainInfoOwnerEmail);

                // Echo the message via the connection's web server and verify.

                message = EchoToConnection(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);
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
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);
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
        public void TestDomainRegisterRequest()
        {
            DomainRegisterRequest message;

            using (var stream = new MemoryStream())
            {
                message = new DomainRegisterRequest();

                Assert.Equal(MessageTypes.DomainRegisterReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainRegisterRequest>(stream, ignoreTypeCode: true);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainRegisterRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-domain", message.Name);
                Assert.Equal("my-description", message.Description);
                Assert.Equal("my-email", message.OwnerEmail);
                Assert.True(message.EmitMetrics);
                Assert.Equal(14, message.RetentionDays);

                // Echo the message via the connection's web server and verify.

                message = EchoToConnection(message);
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
        public void TestDomainRegisterReply()
        {
            DomainRegisterReply message;

            using (var stream = new MemoryStream())
            {
                message = new DomainRegisterReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainRegisterReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(CadenceErrorTypes.None, message.ErrorType);
                Assert.Null(message.Error);
                Assert.Null(message.ErrorDetails);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.ErrorType = CadenceErrorTypes.Custom;
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                message.Error = "MyError";
                Assert.Equal("MyError", message.Error);
                message.ErrorDetails = "MyError Details";
                Assert.Equal("MyError Details", message.ErrorDetails);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainRegisterReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);

                // Echo the message via the connection's web server and verify.

                message = EchoToConnection(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void TestDomainUpdateRequest()
        {
            DomainUpdateRequest message;

            using (var stream = new MemoryStream())
            {
                message = new DomainUpdateRequest();

                Assert.Equal(MessageTypes.DomainUpdateReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainUpdateRequest>(stream, ignoreTypeCode: true);
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
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainUpdateRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("my-name", message.Name);
                Assert.Equal("my-description", message.UpdatedInfoDescription);
                Assert.Equal("joe@bloe.com", message.UpdatedInfoOwnerEmail);
                Assert.True(message.ConfigurationEmitMetrics);
                Assert.Equal(7, message.ConfigurationRetentionDays);

                // Echo the message via the connection's web server and verify.

                message = EchoToConnection(message);
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
        public void TestDomainUpdateReply()
        {
            DomainUpdateReply message;

            using (var stream = new MemoryStream())
            {
                message = new DomainUpdateReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainUpdateReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(CadenceErrorTypes.None, message.ErrorType);
                Assert.Null(message.Error);
                Assert.Null(message.ErrorDetails);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.ErrorType = CadenceErrorTypes.Custom;
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                message.Error = "MyError";
                Assert.Equal("MyError", message.Error);
                message.ErrorDetails = "MyError Details";
                Assert.Equal("MyError Details", message.ErrorDetails);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<DomainUpdateReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);

                // Echo the message via the connection's web server and verify.

                message = EchoToConnection(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void TestTerminateRequest()
        {
            TerminateRequest message;

            using (var stream = new MemoryStream())
            {
                message = new TerminateRequest();

                Assert.Equal(MessageTypes.TerminateReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<TerminateRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<TerminateRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the connection's web server and verify.

                message = EchoToConnection(message);
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
        public void TestTerminateReply()
        {
            TerminateReply message;

            using (var stream = new MemoryStream())
            {
                message = new TerminateReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<TerminateReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(CadenceErrorTypes.None, message.ErrorType);
                Assert.Null(message.Error);
                Assert.Null(message.ErrorDetails);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.ErrorType = CadenceErrorTypes.Custom;
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                message.Error = "MyError";
                Assert.Equal("MyError", message.Error);
                message.ErrorDetails = "MyError Details";
                Assert.Equal("MyError Details", message.ErrorDetails);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<TerminateReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);

                // Echo the message via the connection's web server and verify.

                message = EchoToConnection(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void TestHeartbeatRequest()
        {
            HeartbeatRequest message;

            using (var stream = new MemoryStream())
            {
                message = new HeartbeatRequest();

                Assert.Equal(MessageTypes.HeartbeatReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<HeartbeatRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<HeartbeatRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);

                // Echo the message via the connection's web server and verify.

                message = EchoToConnection(message);
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
        public void TestHeartbeatReply()
        {
            HeartbeatReply message;

            using (var stream = new MemoryStream())
            {
                message = new HeartbeatReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<HeartbeatReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(CadenceErrorTypes.None, message.ErrorType);
                Assert.Null(message.Error);
                Assert.Null(message.ErrorDetails);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.ErrorType = CadenceErrorTypes.Custom;
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                message.Error = "MyError";
                Assert.Equal("MyError", message.Error);
                message.ErrorDetails = "MyError Details";
                Assert.Equal("MyError Details", message.ErrorDetails);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<HeartbeatReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);

                // Echo the message via the connection's web server and verify.

                message = EchoToConnection(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void TestCancelRequest()
        {
            CancelRequest message;

            using (var stream = new MemoryStream())
            {
                message = new CancelRequest();

                Assert.Equal(MessageTypes.CancelReply, message.ReplyType);

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<CancelRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);

                // Round-trip

                message.RequestId = 555;
                message.TargetRequestId = 666;
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<CancelRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.TargetRequestId);

                // Echo the message via the connection's web server and verify.

                message = EchoToConnection(message);
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
        public void TestCancelReply()
        {
            CancelReply message;

            using (var stream = new MemoryStream())
            {
                message = new CancelReply();

                // Empty message.

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<CancelReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(CadenceErrorTypes.None, message.ErrorType);
                Assert.Null(message.Error);
                Assert.Null(message.ErrorDetails);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.ErrorType = CadenceErrorTypes.Custom;
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                message.Error = "MyError";
                Assert.Equal("MyError", message.Error);
                message.ErrorDetails = "MyError Details";
                Assert.Equal("MyError Details", message.ErrorDetails);
                message.WasCancelled = true;
                Assert.True(message.WasCancelled);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<CancelReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);
                Assert.True(message.WasCancelled);

                // Echo the message via the connection's web server and verify.

                message = EchoToConnection(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);
                Assert.True(message.WasCancelled);

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.True(message.WasCancelled);
                Assert.Equal(CadenceErrorTypes.Custom, message.ErrorType);
                Assert.Equal("MyError", message.Error);
                Assert.Equal("MyError Details", message.ErrorDetails);
            }
        }
    }
}
