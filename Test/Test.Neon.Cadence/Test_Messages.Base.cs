//-----------------------------------------------------------------------------
// FILE:        Test_Messages.Base.cs
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
        public void Test_ProxyMessage()
        {
            // Ensures that we can serialize and deserialize base messages.

            ProxyMessage message;

            using (var stream = new MemoryStream())
            {
                // Empty message.

                message = new ProxyMessage();

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ProxyMessage>(stream, ignoreTypeCode: true);
                Assert.Equal(InternalMessageTypes.Unspecified, message.Type);
                Assert.Empty(message.Properties);
                Assert.Empty(message.Attachments);

                // Message with args and attachments.

                message = new ProxyMessage();

                message.Properties.Add(PropertyNames.TestOne, "1");
                message.Properties.Add(PropertyNames.TestTwo, "2");
                message.Properties.Add(PropertyNames.TestEmpty, string.Empty);
                message.Properties.Add(PropertyNames.TestNull, null);
                
                message.SetJsonProperty(PropertyNames.TestComplex, new ComplexType() { Name = "foo", Value = "bar" });
                message.SetJsonProperty(PropertyNames.TestPerson, new Person() { Name = "Jack", Age = 10 });

                message.Attachments.Add(new byte[] { 0, 1, 2, 3, 4 });
                message.Attachments.Add(new byte[0]);
                message.Attachments.Add(null);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ProxyMessage>(stream, ignoreTypeCode: true);
                Assert.Equal(InternalMessageTypes.Unspecified, message.Type);
                Assert.Equal(6, message.Properties.Count);
                Assert.Equal("1", message.Properties[PropertyNames.TestOne]);
                Assert.Equal("2", message.Properties[PropertyNames.TestTwo]);
                Assert.Empty(message.Properties[PropertyNames.TestEmpty]);
                Assert.Null(message.Properties[PropertyNames.TestNull]);

                var complex = message.GetJsonProperty<ComplexType>(PropertyNames.TestComplex);
                Assert.Equal("foo", complex.Name);
                Assert.Equal("bar", complex.Value);

                var person = message.GetJsonProperty<Person>(PropertyNames.TestPerson);
                Assert.Equal("Jack", person.Name);
                Assert.Equal(10, person.Age);

                Assert.Equal(3, message.Attachments.Count);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Attachments[0]);
                Assert.Empty(message.Attachments[1]);
                Assert.Null(message.Attachments[2]);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Test_ProxyRequest()
        {
            // Ensures that we can serialize and deserialize request messages.

            ProxyRequest message;

            using (var stream = new MemoryStream())
            {
                // Empty message.

                message = new ProxyRequest();

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ProxyRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.False(message.IsCancellable);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.IsCancellable = true;

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ProxyRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.True(message.IsCancellable);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Test_ProxyReply()
        {
            // Ensures that we can serialize and deserialize reply messages.

            ProxyReply message;

            using (var stream = new MemoryStream())
            {
                // Empty message.

                message = new ProxyReply();

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ProxyReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);

                // Round-trip

                message.RequestId = 555;
                message.Error = new CadenceError("MyError");

                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ProxyReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Test_ActivityRequest()
        {
            // Ensures that we can serialize and deserialize activity request messages.

            ActivityRequest message;

            using (var stream = new MemoryStream())
            {
                // Empty message.

                message = new ActivityRequest();

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ActivityRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ContextId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.ContextId = 666;
                Assert.Equal(666, message.ContextId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ActivityRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Test_ActivityReply()
        {
            // Ensures that we can serialize and deserialize activity reply messages.

            ActivityReply message;

            using (var stream = new MemoryStream())
            {
                // Empty message.

                message = new ActivityReply();

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ActivityReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Equal(0, message.ActivityContextId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.Error = new CadenceError("MyError");
                Assert.Equal("MyError", message.Error.String);
                message.ActivityContextId = 666;
                Assert.Equal(666, message.ActivityContextId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<ActivityReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(666, message.ActivityContextId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Test_WorkflowRequest()
        {
            // Ensures that we can serialize and deserialize workflow request messages.

            WorkflowRequest message;

            using (var stream = new MemoryStream())
            {
                // Empty message.

                message = new WorkflowRequest();

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Equal(0, message.ContextId);

                // Round-trip

                message.RequestId = 555;
                Assert.Equal(555, message.RequestId);
                message.ContextId = 666;
                Assert.Equal(666, message.ContextId);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowRequest>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal(666, message.ContextId);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void Test_WorkflowReply()
        {
            // Ensures that we can serialize and deserialize workflow reply messages.

            WorkflowReply message;

            using (var stream = new MemoryStream())
            {
                // Empty message.

                message = new WorkflowReply();

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(0, message.RequestId);
                Assert.Null(message.Error);
                Assert.Equal(0, message.ContextId);
                Assert.Equal(InternalReplayStatus.Unspecified, message.ReplayStatus);

                // Round-trip

                message.RequestId = 555;
                message.Error = new CadenceError("MyError");
                message.ContextId = 666;
                message.ReplayStatus = InternalReplayStatus.Replaying;
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(InternalReplayStatus.Replaying, message.ReplayStatus);

                stream.SetLength(0);
                stream.Write(message.SerializeAsBytes(ignoreTypeCode: true));
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize<WorkflowReply>(stream, ignoreTypeCode: true);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("MyError", message.Error.String);
                Assert.Equal(666, message.ContextId);
                Assert.Equal(InternalReplayStatus.Replaying, message.ReplayStatus);
            }
        }
    }
}
