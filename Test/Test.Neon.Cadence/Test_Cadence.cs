//-----------------------------------------------------------------------------
// FILE:        Test_Cadence.cs
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
    public class Test_Cadence
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void BaseMessage()
        {
            // Ensures that we can serialize and deserialize base messages.

            ProxyMessage message;

            using (var stream = new MemoryStream())
            {
                // Empty message.

                message = new ProxyMessage();

                stream.SetLength(0);
                stream.Write(message.Serialize());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize(stream);
                Assert.Equal(MessageType.Unknown, message.Type);
                Assert.Empty(message.Arguments);
                Assert.Empty(message.Attachments);

                // Message with args and attachments.

                message = new ProxyMessage();

                message.Arguments.Add("One", "1");
                message.Arguments.Add("Two", "2");
                message.Arguments.Add("Empty", string.Empty);
                message.Arguments.Add("Null", null);

                message.Attachments.Add(new byte[] { 0, 1, 2, 3, 4 });
                message.Attachments.Add(new byte[0]);
                message.Attachments.Add(null);

                stream.SetLength(0);
                stream.Write(message.Serialize());
                stream.Seek(0, SeekOrigin.Begin);

                message = ProxyMessage.Deserialize(stream);
                Assert.Equal(MessageType.Unknown, message.Type);
                Assert.Equal(4, message.Arguments.Count);
                Assert.Equal("1", message.Arguments["One"]);
                Assert.Equal("2", message.Arguments["Two"]);
                Assert.Empty(message.Arguments["Empty"]);
                Assert.Null(message.Arguments["Null"]);

                Assert.Equal(3, message.Attachments.Count);
                Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, message.Attachments[0]);
                Assert.Empty(message.Attachments[1]);
                Assert.Null(message.Attachments[2]);
            }
        }
    }
}
