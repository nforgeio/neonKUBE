//-----------------------------------------------------------------------------
// FILE:	    Test_ReverseProxy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Net.Http.Server;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Xunit;
using Neon.Xunit.Kube;

using Xunit;

namespace TestKube
{
    public class Test_ReverseProxy
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Method signature for an optional hook passed to <see cref="Send"/>.
        /// This will be called just before the emulated remote endpoint
        /// returns the response status, reason phrase, headers and data.  
        /// The hook  an modify the response if desired and disable any 
        /// other default processing by returning <c>true</c>.
        /// </summary>
        /// <param name="request">The request received by the server.</param>
        /// <param name="response">The response that will be returned by the server.</param>
        /// <returns>
        /// <c>true</c> if the senbd method <b>should not</b> perform any default
        /// request processing and just return the reponse as modified by the hook.
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
            /// Returns any headers to be be returned by the server.
            /// </summary>
            public Dictionary<string, string> Headers { get; set; }

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
            public HttpResponseMessage ProxyResponse { get; set; }

            /// <summary>
            /// The test hook for this operation or <c>null</c>.
            /// </summary>
            public TestHook Hook { get; set; }
        }

        /// <summary>
        /// Creates a mock HTTP server and a wrapped <see cref="ReverseProxy"/>
        /// instance into a form that supports easy unit testing.
        /// </summary>
        private sealed class ProxyTestFixture : IDisposable
        {
            private object                              syncLock   = new object();
            private ReverseProxy                        proxy;
            private MockHttpServer                      server;
            private HttpClient                          client;
            private Dictionary<int, OperationInfo>      operations = new Dictionary<int, OperationInfo>();
            private int                                 nextOpID   = 0;

            public ProxyTestFixture()
            {
                server = new MockHttpServer($"http://localhost:{remotePort}/", OnRequestAsync);
                client = new HttpClient()
                {
                    BaseAddress = new Uri($"http://localhost:{localPort}/"),
                    Timeout     = TimeSpan.FromSeconds(2)
                };

                // Start the proxy.

                proxy = new ReverseProxy(localPort, remotePort);
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

                if (proxy != null)
                {
                    proxy.Dispose();
                    proxy = null;
                }
            }

            /// <summary>
            /// Handles requests received by the server.
            /// </summary>
            /// <param name="context">The request context.</param>
            private async Task OnRequestAsync(RequestContext context)
            {
                OperationInfo opInfo;
                string idHeader;

                var request  = context.Request;
                var response = context.Response;

                lock (syncLock)
                {
                    idHeader = request.Headers["X-NEON-TEST-ID"].FirstOrDefault();
                }

                if (idHeader == null)
                {
                    response.StatusCode = 503;
                    response.ReasonPhrase = $"[X-NEON-TEST-ID] header is missing.";

                    await response.Body.WriteAsync(Encoding.UTF8.GetBytes(response.ReasonPhrase));
                    return;
                }

                if (!int.TryParse(idHeader, out var opId))
                {
                    response.StatusCode = 503;
                    response.ReasonPhrase = $"[X-NEON-TEST-ID={idHeader}] is not an integer.";

                    await response.Body.WriteAsync(Encoding.UTF8.GetBytes(response.ReasonPhrase));
                    return;
                }

                if (!operations.TryGetValue(opId, out opInfo))
                {
                    response.StatusCode = 503;
                    response.ReasonPhrase = $"Operation [X-NEON-TEST-ID={opId}] not found.";

                    await response.Body.WriteAsync(Encoding.UTF8.GetBytes(response.ReasonPhrase));
                    return;
                }

                if (opInfo.Hook != null && opInfo.Hook(request, response))
                {
                    // The hook disabled any further processing.

                    return;
                }

                response.StatusCode = opInfo.StatusCode;
                response.ReasonPhrase = opInfo.ReasonPhrase;

                if (opInfo.Headers != null)
                {
                    foreach (var item in opInfo.Headers)
                    {
                        response.Headers[item.Key] = item.Value;
                    }
                }

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
            /// <param name="headers">Any headers to be included in the server response.</param>
            /// <param name="hook">The optional test hook.</param>
            /// <returns>An <see cref="OperationInfo"/> instance describing what happened.</returns>
            public async Task<OperationInfo> SendAsync(
                HttpRequestMessage                  request, 
                string                              responseText, 
                int                                 statusCode   = 200, 
                string                              reasonPhrase = "OK",
                List<KeyValuePair<string, string>>  headers      = null,
                TestHook                            hook         = null)
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

                    if (headers != null && headers.Count > 0)
                    {
                        opInfo.Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

                        foreach (var item in headers)
                        {
                            opInfo.Headers[item.Key] = item.Value;
                        }
                    }

                    operations.Add(opInfo.Id, opInfo);
                }

                // OnRequest() uses this to correlate requests received by the
                // server with the operation info.

                request.Headers.Add("X-NEON-TEST-ID", opInfo.Id.ToString());

                opInfo.ProxyResponse = await client.SendAsync(request);

                return opInfo;
            }
        }

        //---------------------------------------------------------------------
        // Static members

        // Select endpoints that are unlikely to be already in use and
        // run the proxy command asynchronously.

        private static int localPort  = 61422;
        private static int remotePort = 61423;

        //---------------------------------------------------------------------
        // Instance members

        private static string largeContent;

        /// <summary>
        /// Constructor.
        /// </summary>
        public Test_ReverseProxy()
        {
            var sb = new StringBuilder();

            for (int i = 0; i < 256 * 1024; i++)
            {
                sb.Append('a' + (char)(i % 26));
            }

            largeContent = sb.ToString();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task Basics()
        {
            OperationInfo       opInfo;
            HttpResponseMessage response;

            using (var fixture = new ProxyTestFixture())
            {
                // Verify that receiving a small response works.

                opInfo   = await fixture.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/"), "Hello World!");
                response = opInfo.ProxyResponse;

                Assert.Equal(200, (int)response.StatusCode);
                Assert.Equal("OK", response.ReasonPhrase);
                Assert.Equal("Hello World!", response.Content.ReadAsStringAsync().Result);

                // Verify that receiving a large response works.

                opInfo   = await fixture.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/"), largeContent);
                response = opInfo.ProxyResponse;

                Assert.Equal(200, (int)response.StatusCode);
                Assert.Equal("OK", response.ReasonPhrase);
                Assert.Equal(largeContent, response.Content.ReadAsStringAsync().Result);

                // Verify that custom server headers are returned.

                opInfo = await fixture.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/"), "Hello World!",
                    headers: new List<KeyValuePair<string, string>>()
                    {
                        new KeyValuePair<string, string>("X-Foo", "BAR"),
                        new KeyValuePair<string, string>("X-Hello", "WORLD!")
                    });

                response = opInfo.ProxyResponse;

                Assert.Equal(200, (int)response.StatusCode);
                Assert.Equal("OK", response.ReasonPhrase);
                Assert.Equal("Hello World!", response.Content.ReadAsStringAsync().Result);
                Assert.Equal("BAR", response.Headers.GetValues("X-Foo").First());
                Assert.Equal("WORLD!", response.Headers.GetValues("X-Hello").First());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public async Task Load()
        {
            // Perform some load testing with parallel requests.

            using (var fixture = new ProxyTestFixture())
            {
                var tasks     = new List<Task>();
                var timeLimit = DateTime.UtcNow + TimeSpan.FromSeconds(60);

                // Small requests task.

                tasks.Add(
                    Task.Run(
                        async () =>
                        {
                            while (DateTime.UtcNow < timeLimit)
                            {
                                var opInfo   = await fixture.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/"), "Hello World!");
                                var response = opInfo.ProxyResponse;

                                Assert.Equal(200, (int)response.StatusCode);
                                Assert.Equal("OK", response.ReasonPhrase);
                                Assert.Equal("Hello World!", response.Content.ReadAsStringAsync().Result);
                            }
                        }));

                // Large requests task.

                tasks.Add(
                    Task.Run(
                        async () =>
                        {
                            var opInfo   = await fixture.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/"), largeContent);
                            var response = opInfo.ProxyResponse;

                            Assert.Equal(200, (int)response.StatusCode);
                            Assert.Equal("OK", response.ReasonPhrase);
                            Assert.Equal(largeContent, response.Content.ReadAsStringAsync().Result);
                        }));

                await Task.WhenAll(tasks);
            }
        }
    }
}
