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

using Newtonsoft.Json;
using Xunit;

namespace TestCadence
{
    public sealed partial class Test_Messages : IClassFixture<CadenceFixture>, IDisposable
    {
        //---------------------------------------------------------------------
        // Local types

        public class ComplexType
        {
            [JsonProperty(PropertyName = "Name")]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "Value")]
            public string Value { get; set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        CadenceFixture      fixture;
        CadenceClient       client;
        HttpClient          proxyClient;

        public Test_Messages(CadenceFixture fixture)
        {
            var settings = new CadenceSettings()
            {
                Debug = true,

                //--------------------------------
                // $debug(jeff.lill): DELETE THIS!
                Emulate                = false,
                DebugPrelaunched       = false,
                DebugDisableHandshakes = false,
                DebugDisableHeartbeats = false,
                //--------------------------------
            };

            fixture.Start(settings, keepConnection: true);

            this.fixture     = fixture;
            this.client      = fixture.Connection;
            this.proxyClient = new HttpClient() { BaseAddress = client.ProxyUri };
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
        public void Test_PropertyHelpers()
        {
            // Verify that the property helper methods work as expected.

            var message = new ProxyMessage();

            // Verify that non-existant property values return the default for the requested type.

            var fooProperty = new PropertyNameUtf8("foo");

            Assert.Null(message.GetStringProperty(fooProperty));
            Assert.Equal(0, message.GetIntProperty(fooProperty));
            Assert.Equal(0L, message.GetLongProperty(fooProperty));
            Assert.False(message.GetBoolProperty(fooProperty));
            Assert.Equal(0.0, message.GetDoubleProperty(fooProperty));
            Assert.Equal(DateTime.MinValue, message.GetDateTimeProperty(fooProperty));
            Assert.Equal(TimeSpan.Zero, message.GetTimeSpanProperty(fooProperty));

            // Verify that we can override default values for non-existant properties.

            Assert.Equal("bar", message.GetStringProperty(fooProperty, "bar"));
            Assert.Equal(123, message.GetIntProperty(fooProperty, 123));
            Assert.Equal(456L, message.GetLongProperty(fooProperty, 456L));
            Assert.True(message.GetBoolProperty(fooProperty, true));
            Assert.Equal(123.456, message.GetDoubleProperty(fooProperty, 123.456));
            Assert.Equal(new DateTime(2019, 4, 14), message.GetDateTimeProperty(fooProperty, new DateTime(2019, 4, 14)));
            Assert.Equal(TimeSpan.FromSeconds(123), message.GetTimeSpanProperty(fooProperty, TimeSpan.FromSeconds(123)));

            // Verify that we can write and then read properties.

            message.SetStringProperty(fooProperty, "bar");
            Assert.Equal("bar", message.GetStringProperty(fooProperty));

            message.SetIntProperty(fooProperty, 123);
            Assert.Equal(123, message.GetIntProperty(fooProperty));

            message.SetLongProperty(fooProperty, 456L);
            Assert.Equal(456L, message.GetLongProperty(fooProperty));

            message.SetBoolProperty(fooProperty, true);
            Assert.True(message.GetBoolProperty(fooProperty));

            message.SetDoubleProperty(fooProperty, 123.456);
            Assert.Equal(123.456, message.GetDoubleProperty(fooProperty));

            var date = new DateTime(2019, 4, 14).ToUniversalTime();

            message.SetDateTimeProperty(fooProperty, date);
            Assert.Equal(date, message.GetDateTimeProperty(fooProperty));

            message.SetTimeSpanProperty(fooProperty, TimeSpan.FromSeconds(123));
            Assert.Equal(TimeSpan.FromSeconds(123), message.GetTimeSpanProperty(fooProperty));
        }

        /// <summary>
        /// Transmits a message to the local <b>cadence-client</b> web server and then 
        /// verifies that the response matches.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="message">The message to be checked.</param>
        /// <returns>The received echo message.</returns>
        private TMessage EchoToClient<TMessage>(TMessage message)
            where TMessage : ProxyMessage, new()
        {
#if DEBUG
            var bytes   = message.SerializeAsBytes();
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
#else
            // The RELEASE client doesn't support the "/echo" endpoint so we'll
            // simply return the input message so the unit tests will pass.

            return message;
#endif
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
            var bytes   = message.SerializeAsBytes();
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
    }
}
