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
        /// into a form that easily supports easy unit testing.
        /// </summary>
        private sealed class ProxyTestFixture : IDisposable
        {
            private object                              syncLock   = new object();
            private MockHttpServer                      server;
            private HttpClient                          client;
            private Dictionary<int, OperationInfo>      operations = new Dictionary<int, OperationInfo>();
            private int                                 nextOpID   = 0;

            public ProxyTestFixture()
            {
                server = new MockHttpServer($"http://{remoteEndpoint}/", OnRequest);
                client = new HttpClient()
                {
                    BaseAddress = new Uri($"http://{localEndpoint}/"),
                    Timeout     = TimeSpan.FromSeconds(2)
                };

                // Start the nshell proxy.

                NShellAsync($"proxy unit-test {localEndpoint.Port} {remoteEndpoint.Port}");
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

                NShellTerminateAsync().Wait();
            }

            /// <summary>
            /// Handles requests received by the server.
            /// </summary>
            /// <param name="context">The request context.</param>
            private void OnRequest(RequestContext context)
            {
                OperationInfo opInfo;

                var request  = context.Request;
                var response = context.Response;

                lock (syncLock)
                {
                    var idHeader = request.Headers["X-NEON-TEST-ID"].FirstOrDefault();

                    if (idHeader == null)
                    {
                        response.Body.Write(Encoding.UTF8.GetBytes("TEST-SERVER"));
                        return;
                    }

                    var opId = int.Parse(idHeader);

                    if (!operations.TryGetValue(opId, out opInfo))
                    {
                        response.StatusCode   = 503;
                        response.ReasonPhrase = $"Operation [ID={opId}] not found.";
                        return;
                    }
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
                    response.Body.Write(Encoding.UTF8.GetBytes(opInfo.ResponseText));
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
                        Id           = nextOpID++,
                        StatusCode   = statusCode,
                        ReasonPhrase = reasonPhrase,
                        ResponseText = responseText
                    };

                    operations.Add(opInfo.Id, opInfo);
                }

                // OnRequest() uses this to correlate requests receievd by the
                // server with the operation info.

                request.Headers.Add("X-NEON-TEST-ID", opInfo.Id.ToString());

                await client.SendAsync(request);

                return opInfo;
            }
        }

        //---------------------------------------------------------------------
        // Static members

        private static string   nshellPath;
        private static Process  nshellProcess;

        // Select endpoints that are unlikely to be already in use and
        // run the proxy command asynchronously.

        private static IPEndPoint localEndpoint  = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 61422);
        private static IPEndPoint remoteEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 61423);

        static Test_Proxy()
        {
            nshellPath = Path.Combine(Environment.GetEnvironmentVariable("NF_BUILD_NSHELL"), NeonHelper.IsWindows ? "nshell.exe" : "nshell");
        }

        /// <summary>
        /// Executes <b>nshell</b> synchronously, passing arguments and returning the result.
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns>The <see cref="ExecuteResult"/>.</returns>
        private static ExecuteResult NShell(params object[] args)
        {
            return NeonHelper.ExecuteCapture(nshellPath, args);
        }

        /// <summary>
        /// Executes <b>nshell</b> asynchronously, without waiting for the command to complete.
        /// This is useful for commands that don't terminate by themselves (like <b>nshell proxy</b>.
        /// Call <see cref="NShellTerminateAsync()"/> to kill the running nshell process.
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns>The tracking task.</returns>
        private static void NShellAsync(params object[] args)
        {
            NShellAsync(NeonHelper.NormalizeExecArgs(args));
        }

        /// <summary>
        /// Executes <b>nshell</b>with arguments formatted as a single string asynchronously, without
        /// waiting for the command to complete.  This is useful for commands that don't terminate by 
        /// themselves (like <b>nshell proxy</b>.  Call <see cref="NShellTerminateAsync()"/> to kill
        /// the running nshell process.
        /// </summary>
        /// <param name="args">The arguments.</param>
        private static void NShellAsync(string args)
        {
            if (nshellProcess != null)
            {
                throw new InvalidOperationException("Only one [nshell] process can run at a time.");
            }

            var processInfo = new ProcessStartInfo(nshellPath, args ?? string.Empty);

            processInfo.UseShellExecute        = false;
            processInfo.RedirectStandardError  = false;
            processInfo.RedirectStandardOutput = false;
            processInfo.CreateNoWindow         = true;

            var process = new Process();

            process.StartInfo           = processInfo;
            process.EnableRaisingEvents = false;

            process.Start();

            nshellProcess = process;
        }

        /// <summary>
        /// Terminates the <b>nshell</b> process if one is running.
        /// </summary>
        private static async Task NShellTerminateAsync()
        {
            if (nshellProcess != null)
            {
                nshellProcess.Kill();
                await NeonHelper.WaitForAsync(async () => await Task.FromResult(nshellProcess == null), TimeSpan.FromSeconds(60));
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonShell)]
        public async Task ProxyBasics()
        {
            OperationInfo opInfo;

            using (var fixture = new ProxyTestFixture())
            {
                opInfo = await fixture.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/"), "Hello World!");

                await Task.Delay(100000000);

                Assert.Equal(200, opInfo.StatusCode);
                Assert.Equal("OK", opInfo.ReasonPhrase);
                Assert.Equal("Hello World!", opInfo.ResponseText);
            }
        }
    }
}
