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
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Xunit;

using Xunit;

namespace TestCryptography
{
    public class Test_Messages
    {
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
                Assert.Equal(MessageType.Unspecified, message.Type);
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
                Assert.Equal(MessageType.Unspecified, message.Type);
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
        public void PropertyHelpers()
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

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ProxyReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
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
                Assert.Equal(0, message.ActivityContextId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.ActivityContextId = 666;
                Assert.Equal(666, message.ActivityContextId);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ActivityReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
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
                Assert.Equal(0, message.WorkflowContextId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.WorkflowContextId = 666;
                Assert.Equal(666, message.WorkflowContextId);

                stream.SetLength(0);
                stream.Write(message.Serialize(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.WorkflowContextId);
            }
        }
    }
}
