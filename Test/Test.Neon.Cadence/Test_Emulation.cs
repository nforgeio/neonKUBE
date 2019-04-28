//-----------------------------------------------------------------------------
// FILE:        Test_Emulation.cs
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
using Neon.Common;
using Neon.Cryptography;
using Neon.Data;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Cadence;

using Xunit;

namespace TestCadence
{
    /// <summary>
    /// Tests low-level <see cref="CadenceConnection"/> functionality against a
    /// partially implemented <b>cadence-proxy</b> emulation.
    /// </summary>
    public sealed class Test_Emulation : IClassFixture<CadenceFixture>, IDisposable
    {
        CadenceFixture      fixture;
        CadenceConnection   connection;
        HttpClient          proxyClient;

        public Test_Emulation(CadenceFixture fixture)
        {
            var settings = new CadenceSettings()
            {
                Mode              = ConnectionMode.ListenOnly,
                Debug             = true,
                EmulateProxy      = true,
                //ProxyTimeout      = TimeSpan.FromMinutes(5),
                //DisableHeartbeats = true,
                //IgnoreTimeouts    = true
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
        public void Echo()
        {
            // Verify that the emulated [/echo] answers (for completeness).

            InitializeRequest message;

            using (var stream = new MemoryStream())
            {
                // Empty message.

                message = new InitializeRequest();

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

                // Echo the message via the associated [cadence-proxy] and verify.

                message = EchoToProxy(message);
                Assert.NotNull(message);
                Assert.Equal(555, message.RequestId);
                Assert.Equal("1.2.3.4", message.LibraryAddress);
                Assert.Equal(666, message.LibraryPort);
            }
        }
    }
}
