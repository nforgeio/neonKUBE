//-----------------------------------------------------------------------------
// FILE:	    CadenceExtensions.cs
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
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Tasks;

using Neon.Cadence.Internal;

namespace Neon.Cadence
{
    /// <summary>
    /// Implements handy extension methods.
    /// </summary>
    internal static class CadenceExtensions
    {
        /// <summary>
        /// Extends <see cref="HttpClient"/> by adding a method that can serialize and
        /// transmit a <b>cadence-proxy</b> proxy request message.
        /// </summary>
        /// <typeparam name="TRequest">The request message type.</typeparam>
        /// <param name="client">The HTTP client.</param>
        /// <param name="request">The message to be sent.</param>
        /// <returns>The <see cref="HttpResponse"/>.</returns>
        public async static Task<HttpResponseMessage> SendRequestAsync<TRequest>(this HttpClient client, TRequest request)
            where TRequest : ProxyRequest
        {
            Covenant.Requires<ArgumentNullException>(request != null);

            var bytes   = request.Serialize();
            var content = new ByteArrayContent(bytes);

            content.Headers.ContentType = new MediaTypeHeaderValue(ProxyMessage.ContentType);

            var httpRequest = new HttpRequestMessage(HttpMethod.Put, "/")
            {
                Content = content
            };

            return await client.SendAsync(httpRequest);
        }

        /// <summary>
        /// <para>
        /// Extends <see cref="HttpClient"/> by adding a method that can serialize and
        /// transmit a <b>cadence-proxy</b> reply to a proxy request message.
        /// </para>
        /// <note>
        /// This method ensures that the reply message's <see cref="ProxyReply.RequestId"/>
        /// matches the request's <see cref="ProxyRequest.RequestId"/> before sending the
        /// reply.
        /// </note>
        /// </summary>
        /// <typeparam name="TRequest">The request message type.</typeparam>
        /// <typeparam name="TReply">The reply message type.</typeparam>
        /// <param name="client">The HTTP client.</param>
        /// <param name="request">The request being responsed to.</param>
        /// <param name="reply">The reply message.</param>
        /// <returns>The <see cref="HttpResponse"/>.</returns>
        public async static Task<HttpResponseMessage> SendReplyAsync<TRequest, TReply>(this HttpClient client, TRequest request, TReply reply)
            where TRequest : ProxyRequest
            where TReply : ProxyReply
        {
            Covenant.Requires<ArgumentNullException>(request != null);
            Covenant.Requires<ArgumentNullException>(reply != null);
            Covenant.Requires<ArgumentException>(reply.Type == request.ReplyType, $"Reply message type [{reply.Type}] is not a suitable response for a [{request.Type}] request.");

            reply.RequestId = request.RequestId;

            var bytes   = reply.Serialize();
            var content = new ByteArrayContent(bytes);

            content.Headers.ContentType = new MediaTypeHeaderValue(ProxyMessage.ContentType);

            var httpRequest = new HttpRequestMessage(HttpMethod.Put, "/")
            {
                Content = content
            };

            return await client.SendAsync(httpRequest);
        }
    }
}
