//-----------------------------------------------------------------------------
// FILE:	    Test_Proxy.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Http;
using Microsoft.Net.Http.Server;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Kube;

using Xunit;
using System.Net;

namespace Test.NShell
{
    /// <summary>
    /// Tests the <b>nshell proxy</b> command.
    /// </summary>
    public class Test_Proxy
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Method signature for an optional hook passed to <see cref="Send"/>.
        /// This will be called just before the send method sets the response
        /// status, reason phrase and data.  The hook can modify the response
        /// if desired and disable any other default processing by returning 
        /// <c>true</c>.
        /// </summary>
        /// <param name="request">The request received by the server.</param>
        /// <param name="response">The response that will be returned by the server.</param>
        /// <returns>
        /// <c>true</c> if the server <b>should not</b> perform any default request
        /// processing and just return the reponse as modified by the hook.
        /// </returns>
        private delegate bool TestHook(Request request, Response response);

        /// <summary>
        /// Holds information about a request and response processed by
        /// <see cref="ProxyTestFixture"/>.
        /// </summary>
        private class OperationInfo
        {
            /// <summary>
            /// The operation ID.
            /// </summary>
            public int Id { get; set; }

            /// <summary>
            /// The status code to be returned by the server.
            /// </summary>
            public int StatusCode { get; set; } = 200;

            /// <summary>
            /// The reason phrase to be returned by the server.
            /// </summary>
            public string ReasonPhrase { get; set; } = "OK";

            /// <summary>
            /// The optional response text to be returned by the server.
            /// </summary>
            public string ResponseText { get; set; }

            /// <summary>
            /// Returns the request as received by the server via the proxy.
            /// </summary>
            public Request ServerRequest { get; set; }

            /// <summary>
            /// Returns the request response as returned by the proxy.
            /// </summary>
            public Response ProxyResponse { get; set; }

            /// <summary>
            /// The test hook for this operation or <c>null</c>.
            /// </summary>
            public TestHook Hook { get; set; }
        }

        /// <summary>
        /// Creates a mock HTTP server and a wrapped [nshell proxy] instance
        /// into a form that supports easy unit testing.
        /// </summary>
        private sealed class ProxyTestFixture : IDisposable
        {
            private object                              syncLock   = new object();
            private ProgramRunner                       runner;
            private MockHttpServer                      server;
            private HttpClient                          client;
            private Dictionary<int, OperationInfo>      operations = new Dictionary<int, OperationInfo>();
            private int                                 nextOpID   = 0;

            public ProxyTestFixture()
            {
                server = new MockHttpServer($"http://{remoteEndpoint}/", OnRequestAsync);
                client = new HttpClient()
                {
                    BaseAddress = new Uri($"http://{localEndpoint}/"),
                    Timeout     = TimeSpan.FromSeconds(2)
                };

                // Start the nshell proxy.

                runner = new ProgramRunner();
                runner.Fork(global::NShell.Program.Main, $"proxy", "unit-test", $"{localEndpoint}", $"{remoteEndpoint}");
            }

            public void Dispose()
            {
                if (server != null)
                {
                    server.Dispose();
                    server = null;
                }

                if (client != null)
                {
                    client.Dispose();
                    client = null;
                }

                if (runner != null)
                {
                    runner.Dispose();
                    runner = null;
                }

                runner.TerminateFork();
            }

            /// <summary>
            /// Handles requests received by the server.
            /// </summary>
            /// <param name="context">The request context.</param>
            private async Task OnRequestAsync(RequestContext context)
            {
                OperationInfo   opInfo;
                string          idHeader;

                var request  = context.Request;
                var response = context.Response;

                lock (syncLock)
                {
                    idHeader = request.Headers["X-NEON-TEST-ID"].FirstOrDefault();

                    if (idHeader == null)
                    {
                        response.Body.Write(Encoding.UTF8.GetBytes("TEST-SERVER"));
                        return;
                    }
                }

                var opId = int.Parse(idHeader);

                if (!operations.TryGetValue(opId, out opInfo))
                {
                    response.StatusCode   = 503;
                    response.ReasonPhrase = $"Operation [ID={opId}] not found.";

                    await response.Body.WriteAsync(Encoding.UTF8.GetBytes(response.ReasonPhrase));
                    return;
                }

                if (opInfo.Hook != null && opInfo.Hook(request, response))
                {
                    // The hook disabled any further processing.

                    return;
                }

                response.StatusCode   = opInfo.StatusCode;
                response.ReasonPhrase = opInfo.ReasonPhrase;

                if (!string.IsNullOrEmpty(opInfo.ResponseText))
                {
                    await response.Body.WriteAsync(Encoding.UTF8.GetBytes(opInfo.ResponseText));
                }
            }

            /// <summary>
            /// Transmits a request to the mocked server via the proxy, specifying the
            /// response text, status, and reason phrase to be returned by the server.
            /// The method then waits for the response to be returned by the proxy.
            /// </summary>
            /// <param name="request">YThe request to be submitted to the proxy.</param>
            /// <param name="responseText">The text to be returned by the server.</param>
            /// <param name="statusCode">The HTTP status to be returned by the server.</param>
            /// <param name="reasonPhrase">The HTTP reason phrase to be returned by the server.</param>
            /// <param name="hook">The optional test hook.</param>
            /// <returns>An <see cref="OperationInfo"/> instance describing what happened.</returns>
            public async Task<OperationInfo> SendAsync(
                HttpRequestMessage  request, 
                string              responseText, 
                int                 statusCode   = 200, 
                string              reasonPhrase = "OK",
                TestHook            hook         = null)
            {
                OperationInfo opInfo;

                lock (syncLock)
                {
                    opInfo = new OperationInfo()
                    {
                        Id           = Interlocked.Increment(ref nextOpID),
                        StatusCode   = statusCode,
                        ReasonPhrase = reasonPhrase,
                        ResponseText = responseText
                    };

                    operations.Add(opInfo.Id, opInfo);
                }

                // OnRequest() uses this to correlate requests received by the
                // server with the operation info.

                request.Headers.Add("X-NEON-TEST-ID", opInfo.Id.ToString());

                await client.SendAsync(request);

                return opInfo;
            }
        }

        //---------------------------------------------------------------------
        // Static members

        // Select endpoints that are unlikely to be already in use and
        // run the proxy command asynchronously.

        private static IPEndPoint localEndpoint  = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 61422);
        private static IPEndPoint remoteEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 61423);

        //---------------------------------------------------------------------
        // Instance members

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonShell)]
        public async Task ProxyBasics()
        {
            OperationInfo opInfo;

            using (var fixture = new ProxyTestFixture())
            {
                Thread.Sleep(100000000);

                opInfo = await fixture.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/"), "Hello World!");

                Assert.Equal(200, opInfo.StatusCode);
                Assert.Equal("OK", opInfo.ReasonPhrase);
                Assert.Equal("Hello World!", opInfo.ResponseText);
            }
        }
    }
}
